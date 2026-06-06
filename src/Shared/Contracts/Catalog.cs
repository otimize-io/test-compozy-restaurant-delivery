namespace RestaurantDelivery.Contracts.Catalog;

// Catalog-domain integration event. Unlike order-lifecycle messages it is not order-scoped, so it
// does not implement IOrderMessage. Published by the Catalog service when a restaurant becomes
// available; consumed by Search to index it (tasks 04 and 05).

public sealed record RestaurantPublished(
    Guid RestaurantId,
    string Name,
    string Cuisine,
    GeoPoint Location);
