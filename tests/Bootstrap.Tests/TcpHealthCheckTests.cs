using Bootstrap;

namespace Bootstrap.Tests;

public class TcpHealthCheckTests
{
    [Fact]
    public async Task IsReachableAsync_returns_false_for_unreachable_port()
    {
        var reachable = await TcpHealthCheck.IsReachableAsync(
            "127.0.0.1", port: 1, timeout: TimeSpan.FromMilliseconds(300));

        Assert.False(reachable);
    }

    [Fact]
    public async Task IsReachableAsync_returns_false_for_unknown_host()
    {
        var reachable = await TcpHealthCheck.IsReachableAsync(
            "no-such-host.invalid", port: 5672, timeout: TimeSpan.FromMilliseconds(500));

        Assert.False(reachable);
    }
}
