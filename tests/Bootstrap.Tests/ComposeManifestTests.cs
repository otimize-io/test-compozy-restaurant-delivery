using Bootstrap;

namespace Bootstrap.Tests;

public class ComposeManifestTests
{
    [Fact]
    public void Load_includes_the_five_infrastructure_services()
    {
        var path = Path.Combine(RepoRoot.Path(), "docker-compose.yml");

        var manifest = ComposeManifest.Load(path);

        // The compose file also defines the application tier (services + gateway + web), so assert the
        // five infrastructure services are present rather than an exact set.
        string[] infrastructure = ["elasticsearch", "mongo", "postgres", "rabbitmq", "redis"];
        Assert.All(infrastructure, name => Assert.Contains(name, manifest.ServiceNames));
    }
}
