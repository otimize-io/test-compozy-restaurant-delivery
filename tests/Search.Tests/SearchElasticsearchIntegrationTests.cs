using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using RestaurantDelivery.Contracts;
using RestaurantDelivery.Contracts.Catalog;
using RestaurantDelivery.Platform;
using RestaurantDelivery.Search.Restaurants;
using Testcontainers.Elasticsearch;

namespace Search.Tests;

/// <summary>
/// Integration (task_05 Tests): a real Elasticsearch (Testcontainers, image
/// <c>docker.elastic.co/elasticsearch/elasticsearch:8.13.4</c>) backs the index; the consumer is driven
/// through MassTransit's in-memory harness. Asserts that consuming a <see cref="RestaurantPublished"/>
/// indexes the restaurant and it becomes searchable by name and by cuisine through the real ES client,
/// while a no-match query returns an empty result set. Requires Docker; ES can take ~30-60s to start.
/// </summary>
[Trait("Category", "Integration")]
public class SearchElasticsearchIntegrationTests : IAsyncLifetime
{
    private readonly ElasticsearchContainer _elasticsearch =
        new ElasticsearchBuilder("docker.elastic.co/elasticsearch/elasticsearch:8.13.4").Build();

    private ElasticsearchClient _client = null!;

    public async Task InitializeAsync()
    {
        await _elasticsearch.StartAsync();

        // The 8.x container exposes HTTPS with a self-signed cert and credentials baked into the
        // connection string; allow the cert for the test client.
        var settings = new ElasticsearchClientSettings(new Uri(_elasticsearch.GetConnectionString()))
            .ServerCertificateValidationCallback(CertificateValidations.AllowAll);
        _client = new ElasticsearchClient(settings);
    }

    public Task DisposeAsync() => _elasticsearch.DisposeAsync().AsTask();

    private async Task<(ITestHarness Harness, ServiceProvider Provider)> StartHarnessAsync()
    {
        var provider = new ServiceCollection()
            .AddSingleton(_client)
            .AddSingleton<IRestaurantIndex, ElasticRestaurantIndex>()
            .AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>()
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<RestaurantPublishedConsumer>())
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider);
    }

    [Fact]
    public async Task Consuming_RestaurantPublished_indexes_into_ES_and_it_is_searchable()
    {
        var (harness, provider) = await StartHarnessAsync();
        await using var _ = provider;

        var restaurantId = Guid.NewGuid();
        await harness.Bus.Publish(
            new RestaurantPublished(restaurantId, "Sakura Sushi", "Japanese", new GeoPoint(-23.600, -46.700)));

        Assert.True(await harness.Consumed.Any<RestaurantPublished>());

        var index = provider.GetRequiredService<IRestaurantIndex>();

        // Searchable by name (the consumer indexed with refresh, but allow a brief retry for safety).
        var byName = await SearchUntilFoundAsync(index, "Sakura", restaurantId);
        Assert.Equal(restaurantId, Assert.Single(byName).Id);

        // Searchable by cuisine.
        var byCuisine = await SearchUntilFoundAsync(index, "Japanese", restaurantId);
        Assert.Equal(restaurantId, Assert.Single(byCuisine).Id);

        // No match → empty result set (never an error).
        Assert.Empty(await index.SearchAsync("Vegan"));
    }

    [Fact]
    public async Task Searching_before_anything_is_indexed_returns_empty_not_an_error()
    {
        // Reproduces the reported gateway 500: before any restaurant is published the `restaurants` index
        // does not exist, so ES answers with an invalid response (index_not_found). The index must treat that
        // as "no results" rather than dereferencing a null document set (which surfaced as a 500 / NRE).
        var index = new ElasticRestaurantIndex(_client);

        Assert.Empty(await index.SearchAsync("pizza"));  // term query against a missing index
        Assert.Empty(await index.SearchAsync(null));     // browse (match-all) against a missing index
    }

    private static async Task<IReadOnlyList<IndexedRestaurant>> SearchUntilFoundAsync(
        IRestaurantIndex index, string query, Guid expectedId)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var matches = await index.SearchAsync(query);
            if (matches.Any(r => r.Id == expectedId))
            {
                return matches;
            }

            await Task.Delay(250);
        }

        return await index.SearchAsync(query);
    }
}
