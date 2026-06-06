using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Search.Restaurants;

/// <summary>
/// The restaurant projection Search keeps in Elasticsearch (TechSpec "Data Models": the indexed copy
/// of a restaurant). It carries only what discovery needs — name, cuisine, and location — and is
/// populated purely from the Catalog <c>RestaurantPublished</c> event (ADR-004), never by reading
/// Catalog's database.
/// </summary>
/// <param name="Id">The restaurant's id (also the Elasticsearch document id).</param>
/// <param name="Name">The restaurant's display name; searched by the discovery query.</param>
/// <param name="Cuisine">The cuisine; searched by the discovery query.</param>
/// <param name="Location">The restaurant's location, carried for optional location-aware discovery.</param>
public sealed record IndexedRestaurant(
    Guid Id,
    string Name,
    string Cuisine,
    GeoPoint Location);
