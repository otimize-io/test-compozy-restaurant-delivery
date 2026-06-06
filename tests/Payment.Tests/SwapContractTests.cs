using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Contracts.Commands;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Payment.Callbacks;
using RestaurantDelivery.Payment.Consumers;
using RestaurantDelivery.Payment.Payments;
using RestaurantDelivery.Payment.Ports;
using RestaurantDelivery.Platform;
using Testcontainers.PostgreSql;

namespace Payment.Tests;

/// <summary>
/// The swap-contract test (task_07 subtask 7.5; ADR-001 Phase-2 gate). It runs the identical capture →
/// settlement-callback flow twice — once with <see cref="MockPaymentAdapter"/>, once with
/// <see cref="StubRealPaymentAdapter"/> — over the SAME consumers, settlement service, contract messages,
/// and PostgreSQL store. The ONLY line that differs between the two runs is the single
/// <c>IPaymentPort</c> DI registration, proving the seam is swappable with zero changes outside
/// <c>src/Services/Payment/</c>: no consumer, no callback, no contract, and no neighbour service is touched.
/// Requires Docker (image <c>postgres:16</c>).
/// </summary>
[Trait("Category", "Integration")]
public class SwapContractTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    public static TheoryData<Type> Adapters => new()
    {
        typeof(MockPaymentAdapter),
        typeof(StubRealPaymentAdapter),
    };

    [Theory]
    [MemberData(nameof(Adapters))]
    public async Task Capture_to_settlement_flow_works_for_each_swappable_adapter(Type adapterType)
    {
        // The single swappable seam line — everything else below is identical for both adapters.
        var provider = BuildProvider(adapterType);
        await using var _ = provider;

        // Migrate the per-test schema (unique database name) before the harness starts consuming.
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.EnsureCreatedAsync();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // 1) The saga's CapturePayment command flows in unchanged.
        var orderId = Guid.NewGuid();
        await harness.Bus.Publish(new CapturePayment(orderId, "corr-swap", 49m, $"idem-{orderId:N}"));

        // 2) The async acknowledgement is published — same contract event regardless of adapter.
        Assert.True(await harness.Consumed.Any<CapturePayment>());
        Assert.True(await harness.Published.Any<PaymentAccepted>(p =>
            p.Context!.Message.OrderId == orderId && p.Context.Message.CorrelationId == "corr-swap"));

        // 3) The settlement callback resolves the payment — same SettlementService, same PaymentSettled.
        using (var scope = provider.CreateScope())
        {
            var settlement = scope.ServiceProvider.GetRequiredService<SettlementService>();
            var result = await settlement.SettleAsync(new SettlementCallbackRequest(orderId));
            Assert.Equal(SettlementResult.Settled, result);
        }

        Assert.True(await harness.Published.Any<PaymentSettled>(p => p.Context!.Message.OrderId == orderId));

        // 4) The same adapter that ran the flow is the one that was wired in (the seam was actually swapped).
        using (var scope = provider.CreateScope())
        {
            Assert.IsType(adapterType, scope.ServiceProvider.GetRequiredService<IPaymentPort>());
        }

        // 5) Persistence works identically through the same EF store.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var record = await db.Payments.SingleAsync(r => r.OrderId == orderId);
            Assert.Equal(PaymentStatus.Settled, record.Status);
        }
    }

    private ServiceProvider BuildProvider(Type adapterType)
    {
        var dbName = $"swap_{Guid.NewGuid():N}";
        var connectionString = new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = dbName,
        }.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(new PaymentOptions()));
        services.AddDbContext<PaymentDbContext>(db => db.UseNpgsql(connectionString));
        services.AddScoped<IPaymentStore, EfPaymentStore>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddScoped<SettlementService>();

        // === The one and only swap point ===
        services.AddScoped(typeof(IPaymentPort), adapterType);

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<CapturePaymentConsumer>();
            cfg.AddConsumer<RefundPaymentConsumer>();
        });

        return services.BuildServiceProvider(validateScopes: true);
    }
}
