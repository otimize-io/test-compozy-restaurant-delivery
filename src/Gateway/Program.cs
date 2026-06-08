using Microsoft.AspNetCore.Http;
using RestaurantDelivery.Gateway.Hubs;
using RestaurantDelivery.Gateway.Realtime;
using RestaurantDelivery.Gateway.Roles;
using RestaurantDelivery.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformSerilog();

// YARP reverse proxy (ADR-005): the single client entry point. Routes/clusters are loaded from the
// "ReverseProxy" configuration section (appsettings) so the client-facing paths fan out to Search, Catalog,
// Order, Payment, and Tracking. Service base URLs come from configuration (the cluster destinations).
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// SignalR hub (ADR-007): the gateway hosts the hub and fans out live status to per-order groups.
builder.Services.AddSignalR();
builder.Services.AddSingleton<OrderStatusBroadcaster>();

// The gateway consumes the order-lifecycle events off the bus and pushes OrderStatusChanged to the hub
// (task_14.3). It derives the stage locally from the event type — no cross-service status message.
builder.Services.AddPlatformMessaging(
    builder.Configuration.GetConnectionString("RabbitMq") ?? "rabbitmq://localhost",
    bus =>
    {
        bus.AddConsumer<OrderPlacedHubConsumer>();
        bus.AddConsumer<PaymentSettledHubConsumer>();
        bus.AddConsumer<OrderAcceptedHubConsumer>();
        bus.AddConsumer<OrderReadyHubConsumer>();
        bus.AddConsumer<DriverAssignedHubConsumer>();
        bus.AddConsumer<OrderPickedUpHubConsumer>();
        bus.AddConsumer<OrderDeliveredHubConsumer>();
        bus.AddConsumer<OrderRefundedHubConsumer>();
    });

builder.Services.AddPlatformCore();
builder.Services.AddPlatformHealthChecks();

// CORS for the browser SPA, which is served from a different origin (e.g. http://localhost:4200) than the
// gateway (http://localhost:8080). The SignalR hub needs AllowCredentials, which forbids a wildcard origin,
// so the allowed origins are explicit (configurable via Cors:AllowedOrigins).
const string SpaCorsPolicy = "spa";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];
builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UsePlatform();

// Apply CORS before the endpoints (minimal APIs, the SignalR hub, and the YARP-proxied routes) so the
// browser's preflight is answered at the gateway and every response carries the CORS headers.
app.UseCors(SpaCorsPolicy);

// The role switcher runs before routing so the demo identity is attached to the request and forwarded to
// upstream services (ADR-002). It never fails the request when the role is missing/unknown.
app.UseMiddleware<RoleSwitcherMiddleware>();

// Expose the pre-seeded demo identities the role switcher can select between (ADR-002).
app.MapGet("/api/demo/roles", () => Results.Ok(DemoIdentities.All));

// The current acting demo identity (from X-Demo-Role), or 204 when anonymous.
app.MapGet("/api/demo/whoami", (HttpContext context) =>
    context.Items.TryGetValue(RoleSwitcherMiddleware.IdentityItemKey, out var identity) && identity is not null
        ? Results.Ok(identity)
        : Results.NoContent());

app.MapHub<OrdersHub>("/hubs/orders");
app.MapReverseProxy();

app.Run();

/// <summary>
/// Exposed so the gateway can be hosted in tests via <c>WebApplicationFactory&lt;Program&gt;</c>
/// (a standard, minimal test-visibility hook). No effect on runtime behaviour.
/// </summary>
public partial class Program;
