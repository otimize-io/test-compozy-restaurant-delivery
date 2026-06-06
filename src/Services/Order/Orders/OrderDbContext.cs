using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Order.Saga;

namespace RestaurantDelivery.Order.Orders;

/// <summary>
/// EF Core context for the Order service's own PostgreSQL database (ADR-006). It is a MassTransit
/// <see cref="SagaDbContext"/> so it hosts both:
/// <list type="bullet">
///   <item>the order aggregate (<see cref="Orders"/>, table <c>orders</c>),</item>
///   <item>the saga instance store (via <see cref="OrderStateMap"/>), and</item>
///   <item>the transactional outbox tables (inbox/outbox state + outbox messages), added in
///   <see cref="OnModelCreating"/> so saved business state and published events commit atomically
///   (ADR-004: reliable publishing).</item>
/// </list>
/// </summary>
public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : SagaDbContext(options)
{
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    /// <summary>The saga maps MassTransit's EF saga repository persists through this context.</summary>
    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new OrderStateMap(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Transactional outbox + inbox tables (MassTransit). The outbox stages published messages in the
        // same DB transaction as the saga/order state and a delivery service ships them after commit.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        var order = modelBuilder.Entity<OrderEntity>();
        order.ToTable("orders");
        order.HasKey(o => o.Id);
        order.Property(o => o.CorrelationId).IsRequired();
        order.Property(o => o.Total).HasColumnType("numeric(18,2)");
        order.Property(o => o.Status).HasConversion<string>();
        order.Property(o => o.Items)
            .HasConversion(OrderLineJson.Converter)
            .Metadata.SetValueComparer(OrderLineJson.Comparer);
    }
}
