using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantDelivery.Order.Orders;

namespace RestaurantDelivery.Order.Saga;

/// <summary>
/// EF Core mapping for the <see cref="OrderState"/> saga instance (MassTransit EF saga repository,
/// ADR-004). <see cref="OrderState.CorrelationId"/> is the primary key; <see cref="OrderState.RowVersion"/>
/// backs PostgreSQL optimistic concurrency (an <c>xmin</c> system column). <c>Items</c> is persisted as a
/// JSON column so the captured cart travels with the instance without an extra table.
/// </summary>
public sealed class OrderStateMap : SagaClassMap<OrderState>
{
    protected override void Configure(EntityTypeBuilder<OrderState> entity, ModelBuilder model)
    {
        entity.ToTable("order_sagas");

        entity.Property(x => x.CurrentState).HasMaxLength(64);
        entity.Property(x => x.OrderCorrelationId);
        entity.Property(x => x.Total).HasColumnType("numeric(18,2)");
        entity.Property(x => x.RowVersion).IsRowVersion();

        // The cart lines travel with the saga instance as a JSON string column.
        entity.Property(x => x.Items)
            .HasConversion(OrderLineJson.Converter)
            .Metadata.SetValueComparer(OrderLineJson.Comparer);

        // GeoPoint is a value object flattened into two columns.
        entity.ComplexProperty(x => x.RestaurantLocation, geo =>
        {
            geo.Property(p => p.Lat).HasColumnName("RestaurantLat");
            geo.Property(p => p.Lng).HasColumnName("RestaurantLng");
        });
    }
}
