using MassTransit;
using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Order.Orders;
using RestaurantDelivery.Order.Saga;
using RestaurantDelivery.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformSerilog();

// Order owns its PostgreSQL database (ADR-006): it backs the order aggregate, the MassTransit EF saga
// repository, and the transactional outbox — all in the one OrderDbContext (a SagaDbContext).
builder.Services.AddDbContext<OrderDbContext>(db =>
    db.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=localhost;Database=order;Username=postgres;Password=postgres"));

builder.Services.AddScoped<OrderService>();

// MassTransit hosts the orchestration saga (ADR-004). The bus callback registers, in order: the EF
// transactional outbox (atomic save + publish), and the saga state machine backed by the EF saga
// repository with pessimistic (row-lock) concurrency on the saga instance.
builder.Services.AddPlatformMessaging(
    builder.Configuration.GetConnectionString("RabbitMq")
        ?? "rabbitmq://localhost",
    bus =>
    {
        bus.AddEntityFrameworkOutbox<OrderDbContext>(outbox =>
        {
            outbox.UsePostgres();
            outbox.UseBusOutbox();
        });

        bus.AddSagaStateMachine<OrderStateMachine, OrderState>()
            .EntityFrameworkRepository(r =>
            {
                // Optimistic concurrency backed by the PostgreSQL xmin row-version (OrderState.RowVersion).
                // Pessimistic mode emits a SQL-Server-style row-lock hint that Npgsql rejects; optimistic +
                // the platform's immediate message retry handles the rare concurrent-update contention.
                r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                r.ExistingDbContext<OrderDbContext>();
            });
    });

builder.Services.AddPlatformCore();
builder.Services.AddPlatformHealthChecks();

var app = builder.Build();

// Create the schema at startup. EnsureCreated (not migrations) is sufficient for the mocked PoC: the
// Order schema (order aggregate + saga + outbox tables) is owned solely by this service and there is no
// migration history to preserve. Mirrors the Payment service.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UsePlatform();
app.MapOrderEndpoints();

app.Run();
