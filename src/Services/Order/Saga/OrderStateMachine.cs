using MassTransit;
using RestaurantDelivery.Contracts.Commands;
using RestaurantDelivery.Contracts.Events;

namespace RestaurantDelivery.Order.Saga;

/// <summary>
/// The Order orchestration saga (ADR-004). It owns the happy-path order lifecycle and reacts purely to
/// integration events, emitting commands at the two outbound seams:
/// <list type="bullet">
///   <item><c>OrderPlaced</c> (Initial) → publish <see cref="CapturePayment"/>, go to <c>AwaitingPayment</c>.</item>
///   <item><c>PaymentSettled</c> → <c>Paid</c>; <c>PaymentDeclined</c> → <c>Faulted</c> (terminal).</item>
///   <item><c>OrderAccepted</c> → <c>Preparing</c> (restaurant leg, triggered by task_08).</item>
///   <item><c>OrderReady</c> → publish <see cref="DriverRequested"/>, go to <c>AwaitingDriver</c> (task_08).</item>
///   <item><c>DriverAssigned</c> → <c>DriverAssigned</c> (dispatch leg, triggered by task_10/dispatch).</item>
///   <item><c>OrderPickedUp</c> → <c>PickedUp</c>; <c>OrderDelivered</c> → <c>Delivered</c> (task_10).</item>
/// </list>
/// Idempotency on <c>(OrderId, CorrelationId)</c> is structural: every event correlates to the single
/// saga instance by <c>OrderId</c>, and a redelivered event whose transition has already fired finds the
/// instance in a later state where that event is not accepted, so MassTransit discards it rather than
/// re-running the side effect.
/// <para>
/// EXTENSION POINTS for later tasks (do not require touching the placement/payment legs):
/// task_08 adds the restaurant endpoints that publish <c>OrderAccepted</c>/<c>OrderReady</c>;
/// task_10 adds the driver endpoints that publish <c>OrderPickedUp</c>/<c>OrderDelivered</c>;
/// task_11 adds the compensation branch by handling <see cref="DriverUnavailable"/> in
/// <see cref="AwaitingDriver"/> (left intentionally unhandled here) — issuing <c>RefundPayment</c> and
/// transitioning to a <c>NoDriverRefunded</c> terminal state.
/// </para>
/// </summary>
public sealed class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderPlaced, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentSettled, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentDeclined, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderAccepted, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderReady, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => DriverAssigned, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderPickedUp, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderDelivered, x => x.CorrelateById(m => m.Message.OrderId));

        // Idempotency on (OrderId, CorrelationId): a redelivered or out-of-order event whose transition has
        // already fired arrives in a state that does not accept it. Ignoring (rather than faulting on) the
        // unhandled event makes every consumer safely idempotent — the duplicate is discarded, not retried.
        OnUnhandledEvent(x => x.Ignore());

        Initially(
            When(OrderPlaced)
                .Then(context =>
                {
                    var msg = context.Message;
                    var saga = context.Saga;
                    saga.OrderCorrelationId = msg.CorrelationId;
                    saga.ConsumerId = msg.ConsumerId;
                    saga.RestaurantId = msg.RestaurantId;
                    saga.Total = msg.Total;
                    saga.Items = msg.Items;
                })
                .Publish(
                    context => new CapturePayment(
                        context.Saga.CorrelationId,
                        context.Saga.OrderCorrelationId,
                        context.Saga.Total,
                        context.Saga.CorrelationId.ToString("N")))
                .TransitionTo(AwaitingPayment));

        During(AwaitingPayment,
            When(PaymentSettled)
                .TransitionTo(Paid),
            When(PaymentDeclined)
                .TransitionTo(Faulted));

        During(Paid,
            When(OrderAccepted)
                .TransitionTo(Preparing));

        During(Preparing,
            When(OrderReady)
                // Publish the DriverRequested EVENT (what the Dispatch service consumes). The Contracts
                // library also defines a RequestDriver command, but Dispatch (task_09) listens for the
                // event, so the saga emits DriverRequested to connect the two services.
                .Publish(
                    context => new DriverRequested(
                        context.Saga.CorrelationId,
                        context.Saga.OrderCorrelationId,
                        context.Saga.RestaurantLocation))
                .TransitionTo(AwaitingDriver));

        During(AwaitingDriver,
            When(DriverAssigned)
                .Then(context =>
                {
                    var msg = context.Message;
                    var saga = context.Saga;
                    saga.DriverId = msg.DriverId;
                    saga.DriverName = msg.DriverName;
                    saga.EtaMinutes = msg.EtaMinutes;
                })
                .TransitionTo(DriverAssignedState));

        During(DriverAssignedState,
            When(OrderPickedUp)
                .TransitionTo(PickedUp));

        During(PickedUp,
            When(OrderDelivered)
                .TransitionTo(Delivered));

        // The saga is intentionally NOT finalized/removed on Delivered: the terminal instance is kept in
        // the saga store so GET /api/orders/{id} can still report the final status. (Faulted and the
        // task_11 NoDriverRefunded terminal states are likewise retained.)
    }

    // States (the CurrentState is persisted by name; see OrderStatusMap for the OrderStatus mapping).
    public State AwaitingPayment { get; private set; } = null!;
    public State Paid { get; private set; } = null!;
    public State Faulted { get; private set; } = null!;
    public State Preparing { get; private set; } = null!;
    public State AwaitingDriver { get; private set; } = null!;

    /// <summary>Named <c>DriverAssignedState</c> so it does not collide with the event of the same name.</summary>
    public State DriverAssignedState { get; private set; } = null!;
    public State PickedUp { get; private set; } = null!;
    public State Delivered { get; private set; } = null!;

    // Events the saga reacts to (all are shared Contracts messages — no new cross-service messages).
    public Event<OrderPlaced> OrderPlaced { get; private set; } = null!;
    public Event<PaymentSettled> PaymentSettled { get; private set; } = null!;
    public Event<PaymentDeclined> PaymentDeclined { get; private set; } = null!;
    public Event<OrderAccepted> OrderAccepted { get; private set; } = null!;
    public Event<OrderReady> OrderReady { get; private set; } = null!;
    public Event<DriverAssigned> DriverAssigned { get; private set; } = null!;
    public Event<OrderPickedUp> OrderPickedUp { get; private set; } = null!;
    public Event<OrderDelivered> OrderDelivered { get; private set; } = null!;
}
