using System.Collections.Concurrent;

namespace RestaurantDelivery.Platform;

/// <summary>
/// Tracks whether a unit of work has already been processed, so event consumers can be made
/// idempotent on <c>(orderId, correlationId)</c> (ADR-004).
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Returns <c>true</c> the first time a key is seen, <c>false</c> on every later call.</summary>
    Task<bool> TryBeginAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a key claimed by <see cref="TryBeginAsync"/> so it can be claimed again. Used to undo a
    /// claim whose unit of work did not complete, so a redelivery reprocesses it instead of being
    /// permanently skipped.
    /// </summary>
    Task EndAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Process-local idempotency store. V1 default; a durable store can replace it later.</summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, byte> _seen = new();

    public Task<bool> TryBeginAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_seen.TryAdd(key, 1));

    public Task EndAsync(string key, CancellationToken cancellationToken = default)
    {
        _seen.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

/// <summary>Builds idempotency keys from the order and correlation identifiers.</summary>
public static class IdempotencyKey
{
    public static string For(Guid orderId, string correlationId) => $"{orderId:N}:{correlationId}";
}

public static class IdempotencyStoreExtensions
{
    /// <summary>Runs <paramref name="action"/> only the first time <paramref name="key"/> is seen.</summary>
    public static async Task<bool> RunOnceAsync(
        this IIdempotencyStore store,
        string key,
        Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        if (!await store.TryBeginAsync(key, cancellationToken))
        {
            return false;
        }

        try
        {
            await action();
        }
        catch
        {
            // The unit of work failed — release the claim so a redelivery reprocesses it rather than being
            // skipped forever (a claimed-but-never-run key would strand the order, e.g. Dispatch never
            // publishing DriverAssigned would leave the saga stuck at ReadyForPickup).
            await store.EndAsync(key, cancellationToken);
            throw;
        }

        return true;
    }
}
