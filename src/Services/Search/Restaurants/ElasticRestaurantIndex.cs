using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace RestaurantDelivery.Search.Restaurants;

/// <summary>
/// Elasticsearch-backed restaurant index (ADR-006). Each <see cref="IndexedRestaurant"/> is one
/// document in the <c>restaurants</c> index keyed by its id, so re-indexing a restaurant (e.g. a
/// redelivered event) replaces the document rather than duplicating it. Discovery is a <c>multi_match</c>
/// over <c>name</c> and <c>cuisine</c>; a blank query matches everything, and a no-match query returns
/// an empty list (never an error), matching <see cref="IRestaurantIndex"/>.
/// </summary>
public sealed class ElasticRestaurantIndex : IRestaurantIndex
{
    public const string IndexName = "restaurants";

    private readonly ElasticsearchClient _client;

    public ElasticRestaurantIndex(ElasticsearchClient client) => _client = client;

    public async Task IndexAsync(IndexedRestaurant restaurant, CancellationToken cancellationToken = default)
    {
        // Index with refresh so a just-indexed restaurant is immediately searchable (the discovery
        // demo and integration tests read back right after a publish; the PoC volume is tiny).
        var request = new IndexRequest<IndexedRestaurant>(restaurant, IndexName, restaurant.Id.ToString())
        {
            Refresh = Refresh.True,
        };

        await _client.IndexAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<IndexedRestaurant>> SearchAsync(
        string? query, CancellationToken cancellationToken = default)
    {
        var term = query?.Trim();

        // Blank query → browse (match everything); otherwise match the term against name and cuisine
        // (PRD F1: search by name and/or cuisine).
        Query dsl = string.IsNullOrEmpty(term)
            ? new MatchAllQuery()
            : new MultiMatchQuery
            {
                Query = term,
                Fields = new[] { "name", "cuisine" },
            };

        var request = new SearchRequest(IndexName) { Query = dsl };

        var response = await _client.SearchAsync<IndexedRestaurant>(request, cancellationToken);

        // The index may not exist yet (nothing indexed before the first restaurant is published). That is
        // not an error — it just means no results. Guard the invalid response so a read never throws / 500
        // (response.Documents is null on a failed response, e.g. index_not_found).
        if (!response.IsValidResponse)
        {
            return [];
        }

        return response.Documents.ToList();
    }
}
