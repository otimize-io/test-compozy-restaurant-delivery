using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Events;
using RestaurantDelivery.Tracking.Projection;

namespace Tracking.Tests;

/// <summary>
/// Unit tests (task_12 Tests) for the 5-stage projection against an in-memory store: each lifecycle
/// event maps to its stage, the terminal refunded stage is set, the full stream replays to the same
/// view from empty, and duplicate/out-of-order events never move the stage backwards.
/// </summary>
public class TrackingProjectorTests
{
    private const string Corr = "corr-1";

    private static TrackingProjector NewProjector(out InMemoryTrackingStore store)
    {
        store = new InMemoryTrackingStore();
        return new TrackingProjector(store);
    }

    [Fact]
    public async Task OrderPlaced_sets_stage_1()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();

        var view = await projector.ApplyAsync(NewPlaced(orderId));

        Assert.Equal(TrackingStage.OrderPlaced, view.Stage);
        Assert.Equal(1, (int)view.Stage);
        Assert.Equal(orderId, view.OrderId);
    }

    [Fact]
    public async Task OrderAccepted_advances_to_stage_2_preparing()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();
        await projector.ApplyAsync(NewPlaced(orderId));

        var view = await projector.ApplyAsync(new OrderAccepted(orderId, Corr));

        Assert.Equal(TrackingStage.Preparing, view.Stage);
        Assert.Equal(2, (int)view.Stage);
    }

    [Fact]
    public async Task OrderReady_stays_at_stage_2()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();
        await projector.ApplyAsync(NewPlaced(orderId));
        await projector.ApplyAsync(new OrderAccepted(orderId, Corr));

        var view = await projector.ApplyAsync(new OrderReady(orderId, Corr));

        Assert.Equal(TrackingStage.Preparing, view.Stage);
    }

    [Fact]
    public async Task PaymentSettled_keeps_stage_1()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();
        await projector.ApplyAsync(NewPlaced(orderId));

        var view = await projector.ApplyAsync(new PaymentSettled(orderId, Corr));

        Assert.Equal(TrackingStage.OrderPlaced, view.Stage);
    }

    [Fact]
    public async Task DriverAssigned_advances_to_stage_3()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();
        await projector.ApplyAsync(NewPlaced(orderId));

        var view = await projector.ApplyAsync(new DriverAssigned(orderId, Corr, Guid.NewGuid(), "Alice", 5));

        Assert.Equal(TrackingStage.DriverAssigned, view.Stage);
        Assert.Equal(3, (int)view.Stage);
    }

    [Fact]
    public async Task OrderPickedUp_advances_to_stage_4()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();
        await projector.ApplyAsync(NewPlaced(orderId));

        var view = await projector.ApplyAsync(new OrderPickedUp(orderId, Corr));

        Assert.Equal(TrackingStage.OutForDelivery, view.Stage);
        Assert.Equal(4, (int)view.Stage);
    }

    [Fact]
    public async Task OrderDelivered_advances_to_stage_5()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();
        await projector.ApplyAsync(NewPlaced(orderId));

        var view = await projector.ApplyAsync(new OrderDelivered(orderId, Corr));

        Assert.Equal(TrackingStage.Delivered, view.Stage);
        Assert.Equal(5, (int)view.Stage);
    }

    [Fact]
    public async Task OrderRefunded_sets_refunded_terminal_stage()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();
        await projector.ApplyAsync(NewPlaced(orderId));

        var view = await projector.ApplyAsync(new OrderRefunded(orderId, Corr));

        Assert.Equal(TrackingStage.Refunded, view.Stage);
    }

    [Fact]
    public async Task Replaying_the_full_event_stream_from_empty_reconstructs_the_same_view()
    {
        var orderId = Guid.NewGuid();
        var stream = FullLifecycleStream(orderId);

        // First projection over a fresh store.
        var first = NewProjector(out _);
        TrackingView? a = null;
        foreach (var e in stream)
        {
            a = await first.ApplyAsync(e);
        }

        // Replay the same stream over a second fresh store (Redis state is disposable / rebuildable).
        var second = NewProjector(out _);
        TrackingView? b = null;
        foreach (var e in stream)
        {
            b = await second.ApplyAsync(e);
        }

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(TrackingStage.Delivered, a!.Stage);
        Assert.Equal(a.Stage, b!.Stage);
        Assert.Equal(a.OrderId, b.OrderId);
    }

    [Fact]
    public async Task Duplicate_events_do_not_move_the_stage_backwards()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();
        await projector.ApplyAsync(NewPlaced(orderId));
        await projector.ApplyAsync(new OrderDelivered(orderId, Corr));

        // Re-deliver an earlier-stage event: stage must stay at Delivered.
        var view = await projector.ApplyAsync(new OrderAccepted(orderId, Corr));

        Assert.Equal(TrackingStage.Delivered, view.Stage);
    }

    [Fact]
    public async Task Out_of_order_events_do_not_move_the_stage_backwards()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();

        // Delivered arrives before accepted (out of order).
        await projector.ApplyAsync(new OrderDelivered(orderId, Corr));
        var view = await projector.ApplyAsync(new OrderAccepted(orderId, Corr));

        Assert.Equal(TrackingStage.Delivered, view.Stage);
    }

    [Fact]
    public async Task Refunded_is_terminal_and_not_overwritten_by_a_late_forward_event()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();
        await projector.ApplyAsync(NewPlaced(orderId));
        await projector.ApplyAsync(new OrderRefunded(orderId, Corr));

        var view = await projector.ApplyAsync(new OrderDelivered(orderId, Corr));

        Assert.Equal(TrackingStage.Refunded, view.Stage);
    }

    [Fact]
    public async Task Unmapped_event_leaves_the_view_unchanged()
    {
        var projector = NewProjector(out _);
        var orderId = Guid.NewGuid();
        await projector.ApplyAsync(NewPlaced(orderId));

        // DriverRequested carries no tracking stage; it must not change the view.
        var view = await projector.ApplyAsync(new DriverRequested(orderId, Corr, new GeoPoint(0, 0)));

        Assert.Equal(TrackingStage.OrderPlaced, view.Stage);
    }

    private static OrderPlaced NewPlaced(Guid orderId) => new(
        orderId, Corr, Guid.NewGuid(), Guid.NewGuid(), 42m,
        [new OrderLine(Guid.NewGuid(), "Pizza", 1, 42m)]);

    private static IReadOnlyList<IIntegrationEvent> FullLifecycleStream(Guid orderId) =>
    [
        NewPlaced(orderId),
        new PaymentSettled(orderId, Corr),
        new OrderAccepted(orderId, Corr),
        new OrderReady(orderId, Corr),
        new DriverAssigned(orderId, Corr, Guid.NewGuid(), "Alice", 5),
        new OrderPickedUp(orderId, Corr),
        new OrderDelivered(orderId, Corr),
    ];
}
