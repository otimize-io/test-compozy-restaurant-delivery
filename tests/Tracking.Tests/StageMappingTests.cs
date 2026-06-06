using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Tracking.Projection;

namespace Tracking.Tests;

/// <summary>Unit tests for the pure event→stage mapping and the monotonic advancement rule.</summary>
public class StageMappingTests
{
    private const string Corr = "corr";

    public static TheoryData<IIntegrationEvent, TrackingStage> Mappings() => new()
    {
        { new OrderPlaced(Guid.NewGuid(), Corr, Guid.NewGuid(), Guid.NewGuid(), 1m, []), TrackingStage.OrderPlaced },
        { new PaymentSettled(Guid.NewGuid(), Corr), TrackingStage.OrderPlaced },
        { new OrderAccepted(Guid.NewGuid(), Corr), TrackingStage.Preparing },
        { new OrderReady(Guid.NewGuid(), Corr), TrackingStage.Preparing },
        { new DriverAssigned(Guid.NewGuid(), Corr, Guid.NewGuid(), "A", 1), TrackingStage.DriverAssigned },
        { new OrderPickedUp(Guid.NewGuid(), Corr), TrackingStage.OutForDelivery },
        { new OrderDelivered(Guid.NewGuid(), Corr), TrackingStage.Delivered },
        { new OrderRefunded(Guid.NewGuid(), Corr), TrackingStage.Refunded },
        { new DriverRequested(Guid.NewGuid(), Corr, new GeoPoint(0, 0)), TrackingStage.Unknown },
        { new DriverUnavailable(Guid.NewGuid(), Corr), TrackingStage.Unknown },
    };

    [Theory]
    [MemberData(nameof(Mappings))]
    public void ToStage_maps_each_event_to_its_stage(IIntegrationEvent @event, TrackingStage expected) =>
        Assert.Equal(expected, StageMapping.ToStage(@event));

    [Fact]
    public void AdvanceTo_moves_forward() =>
        Assert.Equal(TrackingStage.Preparing, TrackingStage.OrderPlaced.AdvanceTo(TrackingStage.Preparing));

    [Fact]
    public void AdvanceTo_does_not_move_backwards() =>
        Assert.Equal(TrackingStage.Delivered, TrackingStage.Delivered.AdvanceTo(TrackingStage.Preparing));

    [Fact]
    public void AdvanceTo_ignores_unknown() =>
        Assert.Equal(TrackingStage.Preparing, TrackingStage.Preparing.AdvanceTo(TrackingStage.Unknown));

    [Fact]
    public void AdvanceTo_keeps_refunded_terminal_over_forward_stages() =>
        Assert.Equal(TrackingStage.Refunded, TrackingStage.Refunded.AdvanceTo(TrackingStage.Delivered));
}
