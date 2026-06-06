using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Order.Orders;

/// <summary>
/// Stores an <see cref="OrderLine"/> list as a JSON string column. The cart is immutable reference data
/// captured at placement, so a serialise-on-write / deserialise-on-read conversion is simpler and more
/// robust here than modelling each line as an EF owned entity. Shared by the order aggregate and the saga
/// instance so both persist the cart identically.
/// </summary>
public static class OrderLineJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Converts a line list to/from its JSON representation for EF Core.</summary>
    public static readonly ValueConverter<IReadOnlyList<OrderLine>, string> Converter = new(
        lines => JsonSerializer.Serialize(lines, Options),
        json => Deserialize(json));

    /// <summary>Compares line lists by value so EF change-tracking detects edits to the JSON column.</summary>
    public static readonly ValueComparer<IReadOnlyList<OrderLine>> Comparer = new(
        (a, b) => Equals(a, b),
        v => v.Aggregate(0, (hash, line) => HashCode.Combine(hash, line.GetHashCode())),
        v => v.ToList());

    private static IReadOnlyList<OrderLine> Deserialize(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<OrderLine>>(json, Options) ?? [];

    private static bool Equals(IReadOnlyList<OrderLine>? a, IReadOnlyList<OrderLine>? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null || a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }

        return true;
    }
}
