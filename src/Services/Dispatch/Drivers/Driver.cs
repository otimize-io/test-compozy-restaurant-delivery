using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Dispatch.Drivers;

/// <summary>
/// A delivery driver and current availability (TechSpec "Data Models → Driver"). Stored in Redis and
/// seeded at startup (ADR-006). Only available drivers are candidates for matching.
/// </summary>
/// <param name="Id">The driver's id.</param>
/// <param name="Name">The driver's display name.</param>
/// <param name="Location">The driver's current location, used for nearest-available matching.</param>
/// <param name="Available">Whether the driver can currently take an assignment.</param>
public sealed record Driver(Guid Id, string Name, GeoPoint Location, bool Available);
