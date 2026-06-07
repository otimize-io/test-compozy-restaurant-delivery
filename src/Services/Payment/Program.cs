using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Payment.Callbacks;
using RestaurantDelivery.Payment.Consumers;
using RestaurantDelivery.Payment.Payments;
using RestaurantDelivery.Payment.Ports;
using RestaurantDelivery.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformSerilog();

builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.SectionName));

// Payment owns its PostgreSQL database (ADR-006). Persistence sits behind IPaymentStore and the capture
// itself behind IPaymentPort, so the mock adapter can be swapped for the stub-real adapter (ADR-001).
builder.Services.AddDbContext<PaymentDbContext>(db =>
    db.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=localhost;Database=payment;Username=postgres;Password=postgres"));
builder.Services.AddScoped<IPaymentStore, EfPaymentStore>();

// The active payment seam. Swap this single registration to StubRealPaymentAdapter to exercise the
// stub-real provider — no consumer, callback, or neighbouring service changes (ADR-001 Phase-2 gate).
builder.Services.AddScoped<IPaymentPort, MockPaymentAdapter>();
builder.Services.AddScoped<SettlementService>();

builder.Services.AddPlatformMessaging(
    builder.Configuration.GetConnectionString("RabbitMq")
        ?? "rabbitmq://localhost",
    bus =>
    {
        bus.AddConsumer<CapturePaymentConsumer>();
        bus.AddConsumer<RefundPaymentConsumer>();
    });

builder.Services.AddPlatformCore();
builder.Services.AddPlatformHealthChecks();

var app = builder.Build();

// Create the schema at startup. EnsureCreated (not migrations) is sufficient for the mocked PoC: the
// Payment schema is a single table owned by this service and there is no migration history to preserve.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UsePlatform();
app.MapPaymentEndpoints();

app.Run();

/// <summary>
/// Exposed so the service can be hosted in integration/E2E tests via
/// <c>WebApplicationFactory&lt;Program&gt;</c> (a standard, minimal test-visibility hook; task_14 E2E).
/// </summary>
public partial class Program;
