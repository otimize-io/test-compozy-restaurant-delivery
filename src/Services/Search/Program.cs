using Elastic.Clients.Elasticsearch;
using RestaurantDelivery.Platform;
using RestaurantDelivery.Search.Restaurants;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformSerilog();

// Elasticsearch-backed restaurant discovery index (ADR-006). The index sits behind IRestaurantIndex so
// the consumer and endpoint do not depend on the Elasticsearch client directly and tests can substitute
// an in-memory index (ADR-001).
builder.Services.AddSingleton(_ =>
    new ElasticsearchClient(
        new Uri(builder.Configuration.GetConnectionString("Elasticsearch") ?? "http://localhost:9200")));
builder.Services.AddSingleton<IRestaurantIndex, ElasticRestaurantIndex>();

// Indexing is fed only by Catalog's RestaurantPublished event (ADR-004); Search never reads Catalog's
// database. The consumer is idempotent on the restaurant id via the Platform IIdempotencyStore.
builder.Services.AddPlatformMessaging(
    builder.Configuration.GetConnectionString("RabbitMq")
        ?? "rabbitmq://localhost",
    bus =>
    {
        bus.AddConsumer<RestaurantPublishedConsumer>();
    });

builder.Services.AddPlatformCore();
builder.Services.AddPlatformHealthChecks();

var app = builder.Build();

app.UsePlatform();

app.MapSearchEndpoints();

app.Run();
