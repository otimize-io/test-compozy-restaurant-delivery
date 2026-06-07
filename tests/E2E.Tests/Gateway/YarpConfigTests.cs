using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;

namespace E2E.Tests.Gateway;

/// <summary>
/// Tests that the gateway's YARP configuration (task_14.1) parses and contains the expected routes/clusters,
/// fanning the client-facing paths out to Search, Catalog, Order, Payment, and Tracking (TechSpec API table).
/// The config is loaded the same way the gateway loads it — from the <c>ReverseProxy</c> section — and bound
/// through YARP's own config provider, so this fails if the route table or a cluster is missing or malformed.
/// </summary>
public class YarpConfigTests
{
    private static IProxyConfig LoadProxyConfig()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "gateway-appsettings.json"))
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"));
        using var provider = services.BuildServiceProvider();

        var configProvider = provider.GetRequiredService<IProxyConfigProvider>();
        return configProvider.GetConfig();
    }

    [Fact]
    public void The_route_table_maps_every_client_facing_path_to_the_right_cluster()
    {
        var config = LoadProxyConfig();
        var byRoute = config.Routes.ToDictionary(r => r.RouteId);

        // Search vs Catalog disambiguation under /api/restaurants (exact → Search; with id → Catalog).
        Assert.Equal("search", byRoute["search"].ClusterId);
        Assert.Equal("/api/restaurants", byRoute["search"].Match.Path);
        Assert.Equal("catalog", byRoute["catalog-detail"].ClusterId);
        Assert.Equal("/api/restaurants/{id}", byRoute["catalog-detail"].Match.Path);
        Assert.Equal("catalog", byRoute["catalog-menu"].ClusterId);

        // Order flow.
        Assert.Equal("order", byRoute["orders-place"].ClusterId);
        Assert.Equal("order", byRoute["orders-get"].ClusterId);
        Assert.Equal("order", byRoute["orders-accept"].ClusterId);
        Assert.Equal("order", byRoute["orders-ready"].ClusterId);
        Assert.Equal("order", byRoute["orders-pickup"].ClusterId);
        Assert.Equal("order", byRoute["orders-deliver"].ClusterId);
        Assert.Equal("order", byRoute["restaurant-orders"].ClusterId);
        Assert.Equal("order", byRoute["driver-assignments"].ClusterId);

        // Payment + Tracking.
        Assert.Equal("payment", byRoute["payments-callback"].ClusterId);
        Assert.Equal("tracking", byRoute["orders-status"].ClusterId);
        Assert.Equal("/api/orders/{id}/status", byRoute["orders-status"].Match.Path);
    }

    [Fact]
    public void Every_referenced_cluster_is_defined_with_a_destination()
    {
        var config = LoadProxyConfig();
        var clusterIds = config.Clusters.Select(c => c.ClusterId).ToHashSet();

        Assert.Equal(
            new HashSet<string> { "search", "catalog", "order", "payment", "tracking" },
            clusterIds);

        Assert.All(config.Clusters, cluster =>
        {
            Assert.NotNull(cluster.Destinations);
            Assert.NotEmpty(cluster.Destinations!);
            Assert.All(cluster.Destinations!, d => Assert.False(string.IsNullOrWhiteSpace(d.Value.Address)));
        });

        // Every route points at a defined cluster (no dangling cluster references).
        Assert.All(config.Routes, route => Assert.Contains(route.ClusterId!, clusterIds));
    }
}
