using System.Globalization;
using RestaurantDelivery.Contracts;
using StackExchange.Redis;

namespace RestaurantDelivery.Dispatch.Drivers;

/// <summary>
/// Redis-backed driver store (ADR-006). Each driver is a hash at <c>dispatch:driver:{id}</c>; the set
/// <c>dispatch:drivers</c> indexes all known driver ids so availability can be enumerated for matching.
/// </summary>
public sealed class RedisDriverStore(IConnectionMultiplexer connection) : IDriverStore
{
    private const string IndexKey = "dispatch:drivers";

    private static string DriverKey(Guid id) => $"dispatch:driver:{id:N}";

    public async Task UpsertAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        var db = connection.GetDatabase();
        var key = DriverKey(driver.Id);

        await db.HashSetAsync(key,
        [
            new HashEntry("name", driver.Name),
            new HashEntry("lat", driver.Location.Lat.ToString("R", CultureInfo.InvariantCulture)),
            new HashEntry("lng", driver.Location.Lng.ToString("R", CultureInfo.InvariantCulture)),
            new HashEntry("available", driver.Available ? "1" : "0"),
        ]);

        await db.SetAddAsync(IndexKey, driver.Id.ToString("N"));
    }

    public async Task<IReadOnlyList<Driver>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        var db = connection.GetDatabase();
        var ids = await db.SetMembersAsync(IndexKey);

        var available = new List<Driver>();
        foreach (var idValue in ids)
        {
            if (!Guid.TryParseExact(idValue.ToString(), "N", out var id))
            {
                continue;
            }

            var entries = await db.HashGetAllAsync(DriverKey(id));
            if (entries.Length == 0)
            {
                continue;
            }

            var driver = Map(id, entries);
            if (driver.Available)
            {
                available.Add(driver);
            }
        }

        return available;
    }

    private static Driver Map(Guid id, HashEntry[] entries)
    {
        var fields = entries.ToDictionary(e => e.Name.ToString(), e => e.Value);
        var name = fields.TryGetValue("name", out var n) ? n.ToString() : string.Empty;
        var lat = ParseDouble(fields, "lat");
        var lng = ParseDouble(fields, "lng");
        var available = fields.TryGetValue("available", out var a) && a == "1";

        return new Driver(id, name, new GeoPoint(lat, lng), available);
    }

    private static double ParseDouble(IReadOnlyDictionary<string, RedisValue> fields, string field) =>
        fields.TryGetValue(field, out var value)
        && double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0d;
}
