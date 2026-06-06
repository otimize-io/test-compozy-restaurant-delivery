using RestaurantDelivery.Notification.Notifications;
using RestaurantDelivery.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformSerilog();

// Stateless fire-and-forget notification seam (ADR-006). The mock adapter can be swapped for a real
// email/SMS/push channel without touching the event consumers (ADR-001).
builder.Services.AddSingleton<INotificationPort, MockNotificationAdapter>();

builder.Services.AddPlatformMessaging(
    builder.Configuration.GetConnectionString("RabbitMq")
        ?? "rabbitmq://localhost",
    bus =>
    {
        bus.AddConsumer<OrderPlacedNotificationConsumer>();
        bus.AddConsumer<OrderReadyNotificationConsumer>();
        bus.AddConsumer<DriverAssignedNotificationConsumer>();
        bus.AddConsumer<OrderDeliveredNotificationConsumer>();
        bus.AddConsumer<OrderRefundedNotificationConsumer>();
    });

builder.Services.AddPlatformCore();
builder.Services.AddPlatformHealthChecks();

var app = builder.Build();

app.UsePlatform();

app.Run();
