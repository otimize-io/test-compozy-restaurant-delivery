using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Dispatch.Drivers;

/// <summary>
/// Deterministic seed drivers for the mocked PoC (ADR-006: seeded availability). Fixed ids/locations so
/// the demo and integration tests are reproducible. Seeding is gated by configuration so the compensation
/// path ("no driver available") can be triggered deterministically (task_09 subtask 9.4).
/// </summary>
public static class DriverSeedData
{
    public static IReadOnlyList<Driver> Drivers { get; } =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alice", new GeoPoint(-23.561, -46.656), Available: true),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Bruno", new GeoPoint(-23.600, -46.700), Available: true),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Carla", new GeoPoint(-23.500, -46.600), Available: true),
        new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "Diego", new GeoPoint(-23.700, -46.800), Available: false),
    ];
}
