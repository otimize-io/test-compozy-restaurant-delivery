using Bootstrap;

namespace Bootstrap.Tests;

public class ComposeManifestTests
{
    [Fact]
    public void Load_declares_the_five_infrastructure_services()
    {
        var path = Path.Combine(RepoRoot.Path(), "docker-compose.yml");

        var manifest = ComposeManifest.Load(path);

        Assert.Equal(
            new[] { "elasticsearch", "mongo", "postgres", "rabbitmq", "redis" },
            manifest.ServiceNames.OrderBy(name => name).ToArray());
    }
}
