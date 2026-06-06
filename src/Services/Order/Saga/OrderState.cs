using MassTransit;
using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Order.Saga;

/// <summary>
/// The persisted saga instance for one order (ADR-004: orchestration saga + EF saga repository in
/// PostgreSQL). MassTransit correlates events to this instance by <see cref="CorrelationId"/>, which
/// the Order saga sets to the order's id. <see cref="CurrentState"/> holds the state name; it is
/// mapped onto an <c>OrderStatus</c> for the status read (see <c>OrderStatusMap</c>).
/// </summary>
public sealed class OrderState : SagaStateMachineInstance
{
    /// <summary>MassTransit correlation key — equal to the order id for this saga.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>The current state name (MassTransit persists the state as its name).</summary>
    public string CurrentState { get; set; } = string.Empty;

    /// <summary>Optimistic-concurrency token for the EF saga repository.</summary>
    public uint RowVersion { get; set; }

    // --- Order context captured when the order is placed (OrderPlaced) ---

    /// <summary>The business correlation id propagated on every event/command (ADR-004).</summary>
    public string OrderCorrelationId { get; set; } = string.Empty;

    /// <summary>The consumer who placed the order.</summary>
    public Guid ConsumerId { get; set; }

    /// <summary>The restaurant the order is for.</summary>
    public Guid RestaurantId { get; set; }

    /// <summary>The order total, sent to Payment as the capture amount.</summary>
    public decimal Total { get; set; }

    /// <summary>The ordered lines (owned collection, stored as JSON).</summary>
    public IReadOnlyList<OrderLine> Items { get; set; } = [];

    /// <summary>The restaurant location, sent to Dispatch when a driver is requested.</summary>
    public GeoPoint RestaurantLocation { get; set; }

    // --- Driver assignment captured on DriverAssigned ---

    /// <summary>The assigned driver (set on <c>DriverAssigned</c>), if any.</summary>
    public Guid? DriverId { get; set; }

    /// <summary>The assigned driver's name (set on <c>DriverAssigned</c>), if any.</summary>
    public string? DriverName { get; set; }

    /// <summary>The assigned driver's ETA in minutes (set on <c>DriverAssigned</c>), if any.</summary>
    public int? EtaMinutes { get; set; }
}
