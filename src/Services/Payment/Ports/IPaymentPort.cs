namespace RestaurantDelivery.Payment.Ports;

/// <summary>
/// The async-shaped payment seam (TechSpec "Core Interfaces → IPaymentPort"; ADR-001). A capture is
/// <em>accepted</em> now and returns a correlation id; the terminal outcome is delivered later as a
/// <c>PaymentSettled</c> / <c>PaymentDeclined</c> integration event via the settlement callback. This is
/// the flagship swappable seam — a mock adapter and a stub-real adapter both satisfy it, and the
/// swap-contract test proves either can be wired in without changing any neighbour service (ADR-001
/// Phase-2 gate).
/// </summary>
public interface IPaymentPort
{
    /// <summary>
    /// Accepts a capture for <paramref name="orderId"/>. Idempotent on <paramref name="idempotencyKey"/>:
    /// the same key returns the same <see cref="PaymentCaptureAccepted"/> and records exactly one charge.
    /// Never returns a terminal outcome inline — settlement arrives asynchronously via the callback.
    /// </summary>
    Task<PaymentCaptureAccepted> CaptureAsync(
        Guid orderId, decimal amount, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Refunds a previously captured payment for an order (compensation path, ADR-004).</summary>
    Task RefundAsync(Guid orderId, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The seam's local return type for an accepted (not yet settled) capture. Distinct from the
/// <c>RestaurantDelivery.Contracts.Events.PaymentAccepted</c> wire event so the port's shape is not coupled
/// to the broker message; the consumer translates this into the published <c>PaymentAccepted</c> event.
/// </summary>
public sealed record PaymentCaptureAccepted(string CorrelationId);
