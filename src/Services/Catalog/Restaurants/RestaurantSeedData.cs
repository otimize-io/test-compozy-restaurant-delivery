using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Catalog.Restaurants;

/// <summary>
/// Deterministic seed restaurants and menus for the mocked PoC (ADR-006: seeded data). Fixed
/// ids/locations/prices so the demo, Search indexing, and integration tests are reproducible.
/// </summary>
public static class RestaurantSeedData
{
    public static IReadOnlyList<Restaurant> Restaurants { get; } = BuildRestaurants();

    private static IReadOnlyList<Restaurant> BuildRestaurants()
    {
        var burgerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sushiId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var pizzaId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        return
        [
            new Restaurant(
                burgerId,
                "Burger Barn",
                "American",
                new GeoPoint(-23.561, -46.656),
                [
                    new MenuItem(
                        Guid.Parse("a1111111-1111-1111-1111-111111111111"),
                        burgerId, "Classic Cheeseburger", "Beef patty, cheddar, lettuce, tomato", 28.50m),
                    new MenuItem(
                        Guid.Parse("a2222222-2222-2222-2222-222222222222"),
                        burgerId, "Bacon Double", "Two patties, smoked bacon, special sauce", 36.00m),
                    new MenuItem(
                        Guid.Parse("a3333333-3333-3333-3333-333333333333"),
                        burgerId, "Fries", "Hand-cut salted fries", 12.00m),
                ]),
            new Restaurant(
                sushiId,
                "Sakura Sushi",
                "Japanese",
                new GeoPoint(-23.600, -46.700),
                [
                    new MenuItem(
                        Guid.Parse("b1111111-1111-1111-1111-111111111111"),
                        sushiId, "Salmon Nigiri (8 pcs)", "Fresh salmon over seasoned rice", 42.00m),
                    new MenuItem(
                        Guid.Parse("b2222222-2222-2222-2222-222222222222"),
                        sushiId, "California Roll", "Crab, avocado, cucumber", 30.00m),
                ]),
            new Restaurant(
                pizzaId,
                "Napoli Pizza",
                "Italian",
                new GeoPoint(-23.500, -46.600),
                [
                    new MenuItem(
                        Guid.Parse("c1111111-1111-1111-1111-111111111111"),
                        pizzaId, "Margherita", "San Marzano tomato, mozzarella, basil", 45.00m),
                    new MenuItem(
                        Guid.Parse("c2222222-2222-2222-2222-222222222222"),
                        pizzaId, "Pepperoni", "Mozzarella and spicy pepperoni", 52.00m),
                    new MenuItem(
                        Guid.Parse("c3333333-3333-3333-3333-333333333333"),
                        pizzaId, "Tiramisu", "Classic mascarpone dessert", 22.00m),
                ]),
        ];
    }
}
