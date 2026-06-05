namespace Bootstrap;

/// <summary>A named infrastructure endpoint exposed by the local compose stack.</summary>
public readonly record struct InfraEndpoint(string Name, string Host, int Port);

/// <summary>
/// The infrastructure endpoints the compose stack publishes on the host.
/// Kept in sync with <c>docker-compose.yml</c> (ADR-004, ADR-006).
/// </summary>
public static class InfraEndpoints
{
    public static IReadOnlyList<InfraEndpoint> All { get; } =
    [
        new("rabbitmq", "127.0.0.1", 5682),
        new("postgres", "127.0.0.1", 5432),
        new("mongo", "127.0.0.1", 27017),
        new("elasticsearch", "127.0.0.1", 9200),
        new("redis", "127.0.0.1", 6379),
    ];
}
