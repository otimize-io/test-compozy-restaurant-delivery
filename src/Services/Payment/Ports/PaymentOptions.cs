namespace RestaurantDelivery.Payment.Ports;

/// <summary>
/// Configuration for the mock payment seam (bound from the <c>Payment</c> configuration section). Lets the
/// PoC deterministically exercise the two non-happy shapes ADR-001 mandates the payment seam keep even in
/// V1: a declinable settlement and a settlement that never arrives (the saga-timeout path).
/// </summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>
    /// When set, a capture whose amount is greater than or equal to this value is flagged to resolve as
    /// <c>PaymentDeclined</c> at settlement time. <c>null</c> (default) never declines on amount.
    /// </summary>
    public decimal? DeclineAtOrAbove { get; init; }

    /// <summary>
    /// When set, a capture whose amount is greater than or equal to this value records the charge but is
    /// flagged so settlement never arrives — the "settlement never arrives" path the saga timeout covers
    /// (TechSpec "Integration Points → Payment"). <c>null</c> (default) always settles eventually.
    /// </summary>
    public decimal? NeverSettleAtOrAbove { get; init; }

    /// <summary>Reason text attached to a <c>PaymentDeclined</c> event.</summary>
    public string DeclineReason { get; init; } = "Declined by mock payment provider.";

    /// <summary>
    /// How long <c>POST /api/payments/callback</c> waits for an in-flight capture to be persisted before
    /// returning 404. The SPA settles immediately after placing the order, so the settlement can arrive
    /// before <c>CapturePayment</c> has been consumed and the record written (the capture race). Default 5s;
    /// <see cref="TimeSpan.Zero"/> disables the wait (returns 404 on the first miss).
    /// </summary>
    public TimeSpan CaptureWaitTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Re-check interval while the callback waits for the capture (see <see cref="CaptureWaitTimeout"/>).</summary>
    public TimeSpan CaptureWaitInterval { get; init; } = TimeSpan.FromMilliseconds(200);
}
