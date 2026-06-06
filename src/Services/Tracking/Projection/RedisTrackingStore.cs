using System.Globalization;
using StackExchange.Redis;

namespace RestaurantDelivery.Tracking.Projection;

/// <summary>
/// Redis-backed <see cref="ITrackingStore"/> (ADR-006: Tracking uses Redis for fast status reads and
/// fan-out to the SignalR hub). Each order's view is a hash at <c>tracking:order:{id}</c> holding the
/// numeric stage and the last-updated timestamp. The state is a disposable projection — it can be
/// rebuilt purely from the event stream (task_12 requirement).
/// </summary>
public sealed class RedisTrackingStore(IConnectionMultiplexer connection) : ITrackingStore
{
    // Atomic monotonic write: only overwrites the stored stage when the new stage is strictly greater,
    // so concurrent consumers (events arrive on the broker in no guaranteed order and are processed in
    // parallel) can never produce a lost update that moves the bar backwards. The numeric stage ordering
    // makes the Refunded terminal value (99) outrank every forward stage. Returns the effective stage.
    private const string AdvanceScript =
        "local cur = tonumber(redis.call('HGET', KEYS[1], 'stage')) or 0 " +
        "local nxt = tonumber(ARGV[1]) " +
        "if nxt > cur then " +
        "  redis.call('HSET', KEYS[1], 'stage', nxt, 'updatedAt', ARGV[2]) " +
        "  return nxt " +
        "end " +
        "return cur";

    private static string ViewKey(Guid orderId) => $"tracking:order:{orderId:N}";

    public async Task<TrackingView?> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var db = connection.GetDatabase();
        var entries = await db.HashGetAllAsync(ViewKey(orderId));
        if (entries.Length == 0)
        {
            return null;
        }

        var fields = entries.ToDictionary(e => e.Name.ToString(), e => e.Value);
        var stage = ParseStage(fields);
        var updatedAt = ParseTimestamp(fields);
        return new TrackingView(orderId, stage, updatedAt);
    }

    public async Task SaveAsync(TrackingView view, CancellationToken cancellationToken = default)
    {
        var db = connection.GetDatabase();
        await db.ScriptEvaluateAsync(
            AdvanceScript,
            [ViewKey(view.OrderId)],
            [(int)view.Stage, view.UpdatedAt.ToUnixTimeMilliseconds()]);
    }

    private static TrackingStage ParseStage(IReadOnlyDictionary<string, RedisValue> fields) =>
        fields.TryGetValue("stage", out var value) && value.TryParse(out int stage)
            ? (TrackingStage)stage
            : TrackingStage.Unknown;

    private static DateTimeOffset ParseTimestamp(IReadOnlyDictionary<string, RedisValue> fields) =>
        fields.TryGetValue("updatedAt", out var value)
        && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixMs)
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixMs)
            : DateTimeOffset.UnixEpoch;
}
