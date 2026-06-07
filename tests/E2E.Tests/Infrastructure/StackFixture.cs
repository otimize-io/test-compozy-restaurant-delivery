using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace E2E.Tests.Infrastructure;

/// <summary>
/// The shared infrastructure backbone for the end-to-end happy-path test (task_14.5): ONE RabbitMQ broker
/// connecting all the order-flow services (so the flow runs over a REAL bus, not per-service mocks), plus the
/// datastore containers each service owns (ADR-006) — Postgres for Order and Payment, Redis for Dispatch and
/// Tracking. The services run as separate in-process hosts pointed at these containers, mirroring the real
/// deployment topology. Images match the cached set: <c>rabbitmq:3.13-management</c>, <c>postgres:16</c>,
/// <c>redis:7</c>. Requires Docker.
/// </summary>
public sealed class StackFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3.13-management").Build();

    // Order and Payment each own a PostgreSQL database; separate containers keep the bounded-context
    // ownership honest (no shared store across services, per ADR-006).
    private readonly PostgreSqlContainer _orderDb = new PostgreSqlBuilder("postgres:16").Build();
    private readonly PostgreSqlContainer _paymentDb = new PostgreSqlBuilder("postgres:16").Build();

    // Dispatch and Tracking each own a Redis instance; separate containers keep their projections isolated.
    private readonly RedisContainer _dispatchRedis = new RedisBuilder("redis:7").Build();
    private readonly RedisContainer _trackingRedis = new RedisBuilder("redis:7").Build();

    /// <summary>The single shared broker URI every service connects to (the real cross-service bus).</summary>
    public string RabbitMqConnectionString => _rabbitMq.GetConnectionString();

    public string OrderPostgresConnectionString => _orderDb.GetConnectionString();

    public string PaymentPostgresConnectionString => _paymentDb.GetConnectionString();

    public string DispatchRedisConnectionString => _dispatchRedis.GetConnectionString();

    public string TrackingRedisConnectionString => _trackingRedis.GetConnectionString();

    public async Task InitializeAsync()
    {
        // Start each container with a small retry: starting five containers at once can transiently fail under
        // Docker load (port/daemon contention), and an unretried fixture failure fails the whole collection.
        // RabbitMQ (the shared backbone) is started first, then the datastores in parallel.
        await StartWithRetryAsync(() => _rabbitMq.StartAsync());
        await Task.WhenAll(
            StartWithRetryAsync(() => _orderDb.StartAsync()),
            StartWithRetryAsync(() => _paymentDb.StartAsync()),
            StartWithRetryAsync(() => _dispatchRedis.StartAsync()),
            StartWithRetryAsync(() => _trackingRedis.StartAsync()));
    }

    private static async Task StartWithRetryAsync(Func<Task> start, int attempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await start();
                return;
            }
            catch when (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _rabbitMq.DisposeAsync().AsTask(),
            _orderDb.DisposeAsync().AsTask(),
            _paymentDb.DisposeAsync().AsTask(),
            _dispatchRedis.DisposeAsync().AsTask(),
            _trackingRedis.DisposeAsync().AsTask());
    }
}
