using RestaurantDelivery.Platform;
using RestaurantDelivery.Tracking.Consumers;
using RestaurantDelivery.Tracking.Projection;
using RestaurantDelivery.Tracking.Status;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformSerilog();

// Redis-backed 5-stage projection (ADR-006). The store sits behind ITrackingStore so tests can
// substitute an in-memory store; the Redis state is disposable and rebuildable purely from events.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));
builder.Services.AddSingleton<ITrackingStore, RedisTrackingStore>();
builder.Services.AddSingleton<TrackingProjector>();

// Consume every order-lifecycle event that maps to a tracking stage (ADR-007: Tracking is the
// projection feeding the SignalR hub). Each consumer is idempotent via the Platform IIdempotencyStore.
builder.Services.AddPlatformMessaging(
    builder.Configuration.GetConnectionString("RabbitMq")
        ?? "rabbitmq://localhost",
    bus =>
    {
        bus.AddConsumer<OrderPlacedConsumer>();
        bus.AddConsumer<PaymentSettledConsumer>();
        bus.AddConsumer<OrderAcceptedConsumer>();
        bus.AddConsumer<OrderReadyConsumer>();
        bus.AddConsumer<DriverAssignedConsumer>();
        bus.AddConsumer<OrderPickedUpConsumer>();
        bus.AddConsumer<OrderDeliveredConsumer>();
        bus.AddConsumer<OrderRefundedConsumer>();
    });

builder.Services.AddPlatformCore();
builder.Services.AddPlatformHealthChecks();

var app = builder.Build();

app.UsePlatform();

app.MapStatusEndpoints();

app.Run();

/// <summary>
/// Exposed so the service can be hosted in integration/E2E tests via
/// <c>WebApplicationFactory&lt;Program&gt;</c> (a standard, minimal test-visibility hook; task_14 E2E).
/// </summary>
public partial class Program;
