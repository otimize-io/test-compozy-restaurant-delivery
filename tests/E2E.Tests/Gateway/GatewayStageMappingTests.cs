using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Gateway.Realtime;

namespace E2E.Tests.Gateway;

/// <summary>
/// Unit tests for the gateway's local stage derivation (task_14.3): the hub computes the 5-stage value from
/// the lifecycle event type it consumes off the bus, mirroring Tracking's mapping, without a cross-service
/// status message.
/// </summary>
public class GatewayStageMappingTests
{
    private static readonly string Corr = Guid.NewGuid().ToString();
    private static readonly Guid OrderId = Guid.NewGuid();

    public static TheoryData<IIntegrationEvent, GatewayStage> MappedEvents() => new()
    {
        { new OrderPlaced(OrderId, Corr, Guid.NewGuid(), Guid.NewGuid(), 10m, []), GatewayStage.OrderPlaced },
        { new PaymentSettled(OrderId, Corr), GatewayStage.OrderPlaced },
        { new OrderAccepted(OrderId, Corr), GatewayStage.Preparing },
        { new OrderReady(OrderId, Corr), GatewayStage.Preparing },
        { new DriverAssigned(OrderId, Corr, Guid.NewGuid(), "Alice", 10), GatewayStage.DriverAssigned },
        { new OrderPickedUp(OrderId, Corr), GatewayStage.OutForDelivery },
        { new OrderDelivered(OrderId, Corr), GatewayStage.Delivered },
        { new OrderRefunded(OrderId, Corr), GatewayStage.Refunded },
    };

    [Theory]
    [MemberData(nameof(MappedEvents))]
    public void Each_lifecycle_event_maps_to_its_stage(IIntegrationEvent @event, GatewayStage expected)
    {
        Assert.Equal(expected, GatewayStageMapping.ToStage(@event));
    }

    [Fact]
    public void An_event_with_no_tracking_meaning_maps_to_Unknown()
    {
        // DriverRequested/PaymentAccepted carry no tracking-bar meaning, so the hub does not push for them.
        Assert.Equal(GatewayStage.Unknown, GatewayStageMapping.ToStage(new PaymentAccepted(OrderId, Corr)));
        Assert.Equal(
            GatewayStage.Unknown,
            GatewayStageMapping.ToStage(new DriverRequested(OrderId, Corr, new GeoPoint(0, 0))));
    }

    [Fact]
    public void The_five_forward_stages_keep_Tracking_aligned_numeric_values()
    {
        // The numbers must match Tracking's TrackingStage so a REST resync and the live push agree.
        Assert.Equal(1, (int)GatewayStage.OrderPlaced);
        Assert.Equal(2, (int)GatewayStage.Preparing);
        Assert.Equal(3, (int)GatewayStage.DriverAssigned);
        Assert.Equal(4, (int)GatewayStage.OutForDelivery);
        Assert.Equal(5, (int)GatewayStage.Delivered);
    }
}
