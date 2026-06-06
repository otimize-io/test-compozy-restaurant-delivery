namespace RestaurantDelivery.Payment.Callbacks;

/// <summary>
/// Body of <c>POST /api/payments/callback</c> — the mock settlement webhook (TechSpec API table). The
/// caller identifies the payment by <see cref="OrderId"/>. <see cref="Outcome"/> is optional: when omitted,
/// settlement resolves to the outcome the adapter planned at capture time (so a capture configured to
/// decline produces <c>PaymentDeclined</c>); when supplied (<c>"settle"</c> / <c>"decline"</c>) it forces
/// that outcome, letting the demo/operator drive settlement explicitly.
/// </summary>
public sealed record SettlementCallbackRequest(Guid OrderId, string? Outcome = null);
