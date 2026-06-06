namespace RestaurantDelivery.Payment.Payments;

/// <summary>
/// Lifecycle of a payment (TechSpec "Data Models → Payment"). Capture is async-shaped: a record starts
/// <see cref="Accepted"/> and only reaches a terminal state (<see cref="Settled"/> / <see cref="Declined"/>)
/// when settlement arrives via the <c>/api/payments/callback</c> webhook. <see cref="Refunded"/> is the
/// compensation outcome (ADR-004).
/// </summary>
public enum PaymentStatus
{
    Accepted,
    Settled,
    Declined,
    Refunded,
}

/// <summary>
/// The outcome the mock decided at capture time, so the asynchronous settlement is deterministic and
/// replayable. <see cref="Settle"/> and <see cref="Decline"/> resolve via the callback; <see cref="Never"/>
/// records the charge but is never settled — the "settlement never arrives" path the saga timeout covers
/// (ADR-001 / TechSpec "Integration Points → Payment").
/// </summary>
public enum PlannedSettlement
{
    Settle,
    Decline,
    Never,
}

/// <summary>
/// A persisted payment (TechSpec "Data Models → Payment"; ADR-006: Payment owns its PostgreSQL database).
/// One row per <see cref="IdempotencyKey"/> — a repeated capture with the same key reuses this record and
/// charges once. <see cref="Plan"/> captures the configured outcome decided at capture time so the
/// asynchronous settlement callback is deterministic and replayable.
/// </summary>
public sealed class PaymentRecord
{
    /// <summary>Surrogate key for the payment row.</summary>
    public Guid Id { get; set; }

    /// <summary>The order this payment belongs to.</summary>
    public Guid OrderId { get; set; }

    /// <summary>The captured amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Idempotency key — unique per logical charge; a repeat returns the same record.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Current lifecycle status.</summary>
    public PaymentStatus Status { get; set; }

    /// <summary>Correlation id propagated to the settlement events (ADR-004).</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// The settlement outcome the adapter decided once at capture time, so the callback is deterministic.
    /// </summary>
    public PlannedSettlement Plan { get; set; }
}
