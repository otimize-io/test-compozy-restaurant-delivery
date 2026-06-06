using RestaurantDelivery.Contracts;

namespace RestaurantDelivery.Dispatch.Matching;

/// <summary>
/// Mocked distance helpers for nearest-available matching (V1). Uses a planar squared-distance metric:
/// monotonic in true distance, so it ranks candidates correctly without the cost/precision of a real
/// geodesic. A real maps/ETA port (TechSpec "Integration Points → Maps/ETA") can replace this later.
/// </summary>
public static class GeoDistance
{
    /// <summary>Squared planar distance between two points. Smaller means nearer; used only for ranking.</summary>
    public static double SquaredBetween(GeoPoint a, GeoPoint b)
    {
        var dLat = a.Lat - b.Lat;
        var dLng = a.Lng - b.Lng;
        return (dLat * dLat) + (dLng * dLng);
    }

    /// <summary>Mocked ETA in minutes derived from distance: a fixed base plus a per-degree factor.</summary>
    public static int EtaMinutes(GeoPoint from, GeoPoint to)
    {
        const int baseMinutes = 5;
        const double minutesPerDegree = 60d;
        var distance = Math.Sqrt(SquaredBetween(from, to));
        return baseMinutes + (int)Math.Round(distance * minutesPerDegree);
    }
}
