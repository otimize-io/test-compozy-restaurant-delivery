using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Payment.Callbacks;
using RestaurantDelivery.Payment.Payments;
using RestaurantDelivery.Payment.Ports;
using Testcontainers.PostgreSql;

namespace Payment.Tests;

/// <summary>
/// Integration (task_07 Tests): a real PostgreSQL (Testcontainers, image <c>postgres:16</c>) backs the EF
/// Core store; persistence is exercised end to end and the settlement callback publishes
/// <see cref="PaymentSettled"/>, asserted via MassTransit's in-memory harness. Requires Docker.
/// </summary>
[Trait("Category", "Integration")]
public class PaymentPostgresIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    private PaymentDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new PaymentDbContext(options);
    }

    [Fact]
    public async Task Capture_persists_a_payment_record_to_postgres()
    {
        await using (var db = NewDbContext())
        {
            await db.Database.EnsureCreatedAsync();
            var adapter = new MockPaymentAdapter(new EfPaymentStore(db), Options.Create(new PaymentOptions()));
            await adapter.CaptureAsync(Guid.NewGuid(), 73.20m, "idem-persist", CancellationToken.None);
        }

        // A fresh context proves durability (not just an in-memory tracking graph).
        await using (var verify = NewDbContext())
        {
            var record = await verify.Payments.SingleAsync(p => p.IdempotencyKey == "idem-persist");
            Assert.Equal(73.20m, record.Amount);
            Assert.Equal(PaymentStatus.Accepted, record.Status);
        }
    }

    [Fact]
    public async Task Unique_idempotency_key_means_one_charge_across_two_captures()
    {
        var orderId = Guid.NewGuid();
        await using (var db = NewDbContext())
        {
            await db.Database.EnsureCreatedAsync();
            var adapter = new MockPaymentAdapter(new EfPaymentStore(db), Options.Create(new PaymentOptions()));
            var first = await adapter.CaptureAsync(orderId, 10m, "idem-one", CancellationToken.None);
            var second = await adapter.CaptureAsync(orderId, 10m, "idem-one", CancellationToken.None);
            Assert.Equal(first.CorrelationId, second.CorrelationId);
        }

        await using (var verify = NewDbContext())
        {
            Assert.Equal(1, await verify.Payments.CountAsync(p => p.OrderId == orderId));
        }
    }

    [Fact]
    public async Task Posting_settlement_callback_publishes_PaymentSettled_with_postgres_backed_store()
    {
        await using var db = NewDbContext();
        await db.Database.EnsureCreatedAsync();
        var store = new EfPaymentStore(db);
        var adapter = new MockPaymentAdapter(store, Options.Create(new PaymentOptions()));

        var orderId = Guid.NewGuid();
        var accepted = await adapter.CaptureAsync(orderId, 55m, "idem-settle", CancellationToken.None);

        var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(validateScopes: true);
        await using var _ = provider;
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var service = new SettlementService(store, harness.Bus);
        var result = await service.SettleAsync(new SettlementCallbackRequest(orderId));

        Assert.Equal(SettlementResult.Settled, result);
        Assert.True(await harness.Published.Any<PaymentSettled>(p =>
            p.Context!.Message.OrderId == orderId
            && p.Context.Message.CorrelationId == accepted.CorrelationId));

        await using var verify = NewDbContext();
        Assert.Equal(PaymentStatus.Settled,
            (await verify.Payments.SingleAsync(p => p.OrderId == orderId)).Status);
    }
}
