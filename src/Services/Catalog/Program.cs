using MongoDB.Driver;
using RestaurantDelivery.Catalog.Restaurants;
using RestaurantDelivery.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformSerilog();

builder.Services.Configure<CatalogOptions>(builder.Configuration.GetSection(CatalogOptions.SectionName));

// MongoDB-backed restaurants/menus (ADR-006). The store sits behind IRestaurantStore so the seeder and
// endpoints do not depend on MongoDB directly and tests can substitute an in-memory store (ADR-001).
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(
        builder.Configuration.GetConnectionString("MongoDb") ?? "mongodb://localhost:27017"));
builder.Services.AddSingleton<IRestaurantStore, MongoRestaurantStore>();

// Seeds mock restaurants/menus at startup and publishes one RestaurantPublished each for Search (ADR-004).
builder.Services.AddHostedService<RestaurantSeeder>();

// Re-publishes the seeded catalog a few times after startup so Search indexes it even if it bound its
// RestaurantPublished queue after the seeder's first publish (the consumer-bind race). Bounded; idempotent.
builder.Services.AddHostedService<RestaurantRepublisher>();

builder.Services.AddPlatformMessaging(
    builder.Configuration.GetConnectionString("RabbitMq")
        ?? "rabbitmq://localhost");

builder.Services.AddPlatformCore();
builder.Services.AddPlatformHealthChecks();

var app = builder.Build();

app.UsePlatform();

app.MapRestaurantEndpoints();

app.Run();
