using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Catalog.Restaurants;

/// <summary>
/// A restaurant and its menu (TechSpec "Data Models → Restaurant / MenuItem"). Persisted as a single
/// MongoDB document with the menu items embedded, because the document model fits nested menus/items
/// (ADR-006). The embedded copy is the source of truth Search indexes via <c>RestaurantPublished</c>.
/// </summary>
/// <param name="Id">The restaurant's id (document <c>_id</c>).</param>
/// <param name="Name">The restaurant's display name.</param>
/// <param name="Cuisine">The cuisine, used for discovery in Search.</param>
/// <param name="Location">The restaurant's location, used for driver matching/ETA.</param>
/// <param name="Menu">The restaurant's menu items, embedded in the same document.</param>
public sealed record Restaurant(
    Guid Id,
    string Name,
    string Cuisine,
    GeoPoint Location,
    IReadOnlyList<MenuItem> Menu);

/// <summary>
/// A single menu item (TechSpec "Data Models → MenuItem"). Embedded inside its owning
/// <see cref="Restaurant"/> document; <c>RestaurantId</c> mirrors the owner so an item is
/// self-describing when read out of the menu.
/// </summary>
/// <param name="Id">The item's id.</param>
/// <param name="RestaurantId">The owning restaurant's id.</param>
/// <param name="Name">The item's name.</param>
/// <param name="Description">A short description.</param>
/// <param name="Price">The item's price.</param>
public sealed record MenuItem(
    Guid Id,
    Guid RestaurantId,
    string Name,
    string Description,
    decimal Price);
