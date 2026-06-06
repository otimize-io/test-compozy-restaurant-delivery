using RestaurantDelivery.Dispatch.Drivers;
using RestaurantDelivery.Dispatch.Matching;
using RestaurantDelivery.Platform;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformSerilog();

builder.Services.Configure<DispatchOptions>(builder.Configuration.GetSection(DispatchOptions.SectionName));

// Redis-backed driver availability/location (ADR-006). The store sits behind IDriverStore and the
// nearest-available matcher behind IDriverMatcher, so a batched/ETA matcher can swap in later (ADR-001).
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));
builder.Services.AddSingleton<IDriverStore, RedisDriverStore>();
builder.Services.AddSingleton<IDriverMatcher, NearestAvailableDriverMatcher>();
builder.Services.AddHostedService<DriverSeeder>();

builder.Services.AddPlatformMessaging(
    builder.Configuration.GetConnectionString("RabbitMq")
        ?? "rabbitmq://localhost",
    bus =>
    {
        bus.AddConsumer<DriverRequestedConsumer>();
    });

builder.Services.AddPlatformCore();
builder.Services.AddPlatformHealthChecks();

var app = builder.Build();

app.UsePlatform();

app.Run();
