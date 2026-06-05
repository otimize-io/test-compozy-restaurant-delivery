using Bootstrap;

namespace Bootstrap.Tests;

/// <summary>
/// Integration smoke check: every infrastructure port must be reachable while the compose stack
/// is up. Tagged Integration so it is excluded from unit-only runs
/// (<c>dotnet test --filter "Category!=Integration"</c>).
/// Bring the stack up first with: <c>docker compose up -d --wait</c>.
/// </summary>
[Trait("Category", "Integration")]
public class InfraHealthIntegrationTests
{
    public static IEnumerable<object[]> Endpoints() =>
        InfraEndpoints.All.Select(e => new object[] { e.Name, e.Host, e.Port });

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Infra_port_is_reachable_when_stack_is_up(string name, string host, int port)
    {
        var reachable = await TcpHealthCheck.IsReachableAsync(host, port, TimeSpan.FromSeconds(5));

        Assert.True(reachable, $"{name} ({host}:{port}) is not reachable — is 'docker compose up -d --wait' running?");
    }
}
