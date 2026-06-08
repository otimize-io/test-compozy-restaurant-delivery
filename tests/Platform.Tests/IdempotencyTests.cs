using RestaurantDelivery.Platform;

namespace Platform.Tests;

public class IdempotencyTests
{
    [Fact]
    public async Task Runs_action_once_for_a_duplicate_key()
    {
        var store = new InMemoryIdempotencyStore();
        var key = IdempotencyKey.For(Guid.NewGuid(), "corr-1");
        var runs = 0;

        var first = await store.RunOnceAsync(key, () => { runs++; return Task.CompletedTask; });
        var second = await store.RunOnceAsync(key, () => { runs++; return Task.CompletedTask; });

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Admits_distinct_keys_but_blocks_repeats()
    {
        var store = new InMemoryIdempotencyStore();

        Assert.True(await store.TryBeginAsync("a"));
        Assert.True(await store.TryBeginAsync("b"));
        Assert.False(await store.TryBeginAsync("a"));
    }

    [Fact]
    public async Task Releases_the_key_when_the_action_fails_so_a_redelivery_can_reprocess()
    {
        var store = new InMemoryIdempotencyStore();
        var key = IdempotencyKey.For(Guid.NewGuid(), "corr-fail");
        var runs = 0;

        // First delivery: the action throws (e.g. a transient publish failure). The claim must be released.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.RunOnceAsync(key, () =>
            {
                runs++;
                throw new InvalidOperationException("boom");
            }));

        // Redelivery: because the failed claim was released, the action runs again instead of being skipped.
        var retried = await store.RunOnceAsync(key, () =>
        {
            runs++;
            return Task.CompletedTask;
        });

        Assert.True(retried);
        Assert.Equal(2, runs);
    }

    [Fact]
    public void Key_combines_order_and_correlation()
    {
        var orderId = Guid.NewGuid();

        var key = IdempotencyKey.For(orderId, "corr-9");

        Assert.Equal($"{orderId:N}:corr-9", key);
    }
}
