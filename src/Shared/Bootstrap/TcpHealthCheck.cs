using System.Net.Sockets;

namespace Bootstrap;

/// <summary>Minimal TCP reachability probe used by the bootstrap smoke check.</summary>
public static class TcpHealthCheck
{
    /// <summary>
    /// Returns <c>true</c> when a TCP connection to <paramref name="host"/>:<paramref name="port"/>
    /// succeeds within <paramref name="timeout"/>; otherwise <c>false</c> (never throws).
    /// </summary>
    public static async Task<bool> IsReachableAsync(
        string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            await client.ConnectAsync(host, port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
