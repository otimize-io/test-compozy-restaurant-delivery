using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Dispatch.Matching;

/// <summary>
/// Dispatch seam (TechSpec "Core Interfaces"). The V1 mock returns the nearest available driver for an
/// order, or <c>null</c> when none exists. The port keeps matching swappable: a batched/ETA matcher can
/// replace <see cref="NearestAvailableDriverMatcher"/> later with no caller changes (task_09 requirement,
/// ADR-001).
/// </summary>
public interface IDriverMatcher
{
    /// <summary>
    /// Finds the nearest available driver to <paramref name="restaurant"/> for
    /// <paramref name="orderId"/>, or <c>null</c> when no driver is available.
    /// </summary>
    Task<DriverAssignment?> FindDriverAsync(Guid orderId, GeoPoint restaurant, CancellationToken cancellationToken = default);
}

/// <summary>The driver chosen for an order plus the mocked ETA. Returned by <see cref="IDriverMatcher"/>.</summary>
/// <param name="DriverId">The assigned driver's id.</param>
/// <param name="DriverName">The assigned driver's display name.</param>
/// <param name="EtaMinutes">Mocked ETA in minutes from the driver to the restaurant.</param>
public sealed record DriverAssignment(Guid DriverId, string DriverName, int EtaMinutes);
