namespace RestaurantDelivery.Contracts;

/// <summary>A geographic point used for driver matching and ETA (mocked in V1).</summary>
public readonly record struct GeoPoint(double Lat, double Lng);

/// <summary>A single line of an order: the item, how many, and the unit price.</summary>
public sealed record OrderLine(Guid ItemId, string Name, int Quantity, decimal UnitPrice);
