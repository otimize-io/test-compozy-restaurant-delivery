namespace RestaurantDelivery.Gateway.Roles;

/// <summary>
/// A pre-seeded demo identity selected by the <c>X-Demo-Role</c> header (ADR-002: role switcher, no auth).
/// The three identities map to the consumer/restaurant/driver views of the "one order, three live views"
/// demo. There is no authentication in V1 — the identity is purely a demo convenience attached per request.
/// </summary>
/// <param name="Role">The demo role key (<c>consumer</c>, <c>restaurant</c>, or <c>driver</c>).</param>
/// <param name="UserId">A stable, deterministic id for the seeded user so demos/tests are reproducible.</param>
/// <param name="DisplayName">A human-friendly name for display in the role switcher.</param>
public sealed record DemoIdentity(string Role, Guid UserId, string DisplayName);

/// <summary>
/// The fixed set of demo identities the gateway can switch between (ADR-002). Deterministic ids keep the
/// demo and the integration tests reproducible. This is the single source of truth for the role switcher.
/// </summary>
public static class DemoIdentities
{
    /// <summary>The header that selects the active demo role on every request.</summary>
    public const string HeaderName = "X-Demo-Role";

    public const string Consumer = "consumer";
    public const string Restaurant = "restaurant";
    public const string Driver = "driver";

    private static readonly IReadOnlyDictionary<string, DemoIdentity> ByRole =
        new Dictionary<string, DemoIdentity>(StringComparer.OrdinalIgnoreCase)
        {
            [Consumer] = new(Consumer, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Demo Consumer"),
            [Restaurant] = new(Restaurant, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Demo Restaurant"),
            [Driver] = new(Driver, Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Demo Driver"),
        };

    /// <summary>The available demo identities, in switcher order (consumer, restaurant, driver).</summary>
    public static IReadOnlyList<DemoIdentity> All { get; } =
        [ByRole[Consumer], ByRole[Restaurant], ByRole[Driver]];

    /// <summary>
    /// Resolves the identity for a role key, or <c>null</c> when the role is missing/unknown. The lookup is
    /// case-insensitive so <c>Consumer</c> and <c>consumer</c> both resolve.
    /// </summary>
    public static DemoIdentity? Resolve(string? role) =>
        role is not null && ByRole.TryGetValue(role, out var identity) ? identity : null;
}
