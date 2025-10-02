using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FIAP.CloudGames.Catalog.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static FIAP.CloudGames.Catalog.API.Data.Search.SearchContracts;

namespace FIAP.CloudGames.Catalog.API.Data.Search;

public interface IProductSearchService
{
    Task IndexAsync(Product p, CancellationToken ct = default);

    Task BulkIndexAllFromDatabase(CatalogContext db, CancellationToken ct = default);

    Task<PagedResult<Product>> SearchAsync(string q, string? platform, string? genre, string[]? tags, int page = 1, int size = 20, SortBy sort = SortBy.Popularity, CancellationToken ct = default);

    Task<IReadOnlyCollection<Product>> RecommendForUserAsync(string userId, IEnumerable<string> likedGenres, IEnumerable<string> likedTags, string? platform, int size = 20, CancellationToken ct = default);

    Task<PopularMetricsResult> PopularMetricsAsync(CancellationToken ct = default);
}

public record PopularMetricsResult(IEnumerable<(string Genre, long Count)> TopByGenre, IEnumerable<(string Platform, long Count)> TopByPlatform, IEnumerable<(string Game, long Popularity)> TopGamesOverall);

public class ElasticsearchProductSearchService : IProductSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly string _index;

    public ElasticsearchProductSearchService(ElasticsearchClient client, IOptions<Configuration.ElasticsearchConfig.ElasticsearchOptions> opts)
    {
        _client = client;
        _index = opts.Value.IndexName;
    }

    public async Task IndexAsync(Product p, CancellationToken ct = default)
    {
        var resp = await _client.IndexAsync(p, i => i.Index(_index).Id(p.Id.ToString()), ct);

        if (!resp.IsValidResponse)
            throw new InvalidOperationException($"Index failed: {resp.ElasticsearchServerError?.Error?.Reason}");
    }

    public async Task BulkIndexAllFromDatabase(CatalogContext db, CancellationToken ct = default)
    {
        await _client.DeleteByQueryAsync<Product>(
            _index,
            d => d.Conflicts(Conflicts.Proceed).Slices(new Slices(SlicesCalculation.Auto)).Refresh(true).Query(q => q.MatchAll(new MatchAllQuery())),
            ct);

        var active = await db.Products.AsNoTracking().Where(p => p.Active).ToListAsync(ct);

        var bulkResp = await _client.BulkAsync(b => b
            .Index(_index)
            .Refresh(Refresh.True)
            .IndexMany(active, (op, p) => op.Id(p.Id.ToString())),
            ct);

        if (!bulkResp.IsValidResponse || bulkResp.Errors)
        {
            var firstErr = bulkResp.Items?.FirstOrDefault(i => i.Error is not null)?.Error?.Reason;
            throw new InvalidOperationException("Bulk indexing failed: " + firstErr);
        }

        await _client.Indices.RefreshAsync(_index, ct);
    }

    public async Task<PagedResult<Product>> SearchAsync(string q, string? platform, string? genre, string[]? tags, int page = 1, int size = 20, SortBy sort = SortBy.Popularity, CancellationToken ct = default)
    {
        var must = new List<Query>();
        var should = new List<Query>();
        var filter = new List<Query> { new TermQuery(new Field("active")) { Value = true } };

        if (!string.IsNullOrWhiteSpace(q))
        {
            should.Add(new MultiMatchQuery
            {
                Query = q,
                Fields = new[] { "name^3", "tags^2", "description" },
                Fuzziness = new Fuzziness(1)
            });
            should.Add(new MatchPhrasePrefixQuery(new Field("name")) { Query = q.ToLowerInvariant() });
        }

        if (!string.IsNullOrWhiteSpace(platform))
            filter.Add(new TermQuery(new Field("platform")) { Value = platform });

        if (!string.IsNullOrWhiteSpace(genre))
            filter.Add(new TermQuery(new Field("genre")) { Value = genre });

        if (tags is { Length: > 0 })
        {
            filter.Add(new TermsQuery
            {
                Field = new Field("tags"),
                Term = new TermsQueryField(tags.Select(t => (FieldValue)t).ToArray())
            });
        }

        var sortList = new List<SortOptions>();
        switch (sort)
        {
            case SortBy.Metacritic:
                sortList.Add(SortOptions.Field(new Field("metacritic"), new FieldSort { Order = SortOrder.Desc }));
                sortList.Add(SortOptions.Field(new Field("popularityScore"), new FieldSort { Order = SortOrder.Desc }));
                sortList.Add(SortOptions.Field(new Field("name.keyword"), new FieldSort { Order = SortOrder.Asc }));
                break;

            case SortBy.Recent:
                sortList.Add(SortOptions.Field(new Field("releaseDate"), new FieldSort { Order = SortOrder.Desc }));
                sortList.Add(SortOptions.Field(new Field("popularityScore"), new FieldSort { Order = SortOrder.Desc }));
                sortList.Add(SortOptions.Field(new Field("name.keyword"), new FieldSort { Order = SortOrder.Asc }));
                break;

            default: // Popularity
                sortList.Add(SortOptions.Field(new Field("popularityScore"), new FieldSort { Order = SortOrder.Desc }));
                sortList.Add(SortOptions.Field(new Field("metacritic"), new FieldSort { Order = SortOrder.Desc }));
                sortList.Add(SortOptions.Field(new Field("name.keyword"), new FieldSort { Order = SortOrder.Asc }));
                break;
        }

        var req = new SearchRequest<Product>(_index)
        {
            Query = new BoolQuery
            {
                Must = must,
                Should = should,
                MinimumShouldMatch = should.Count > 0 ? 1 : 0,
                Filter = filter
            },
            From = (page - 1) * size,
            Size = size,
            Sort = sortList,
            TrackTotalHits = new TrackHits(10_000)
        };

        var resp = await _client.SearchAsync<Product>(req, ct);
        if (!resp.IsValidResponse)
            throw new InvalidOperationException($"Search failed: {resp.ElasticsearchServerError?.Error?.Reason}");

        var total = ReadTotal(resp);
        return new PagedResult<Product>(resp.Documents, page, size, total);
    }

    public async Task<IReadOnlyCollection<Product>> RecommendForUserAsync(string userId, IEnumerable<string> likedGenres, IEnumerable<string> likedTags, string? platform, int size = 20, CancellationToken ct = default)
    {
        var should = new List<Query>();
        var filter = new List<Query> { new TermQuery(new Field("active")) { Value = true } };

        var genresArr = likedGenres?.Where(g => !string.IsNullOrWhiteSpace(g))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToArray() ?? Array.Empty<string>();

        var tagsArr = likedTags?.Where(t => !string.IsNullOrWhiteSpace(t))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToArray() ?? Array.Empty<string>();

        if (genresArr.Length > 0)
        {
            should.Add(new TermsQuery
            {
                Field = new Field("genre"),
                Term = new TermsQueryField(genresArr.Select(g => (FieldValue)g).ToArray())
            });
        }

        if (tagsArr.Length > 0)
        {
            should.Add(new TermsQuery
            {
                Field = new Field("tags"),
                Term = new TermsQueryField(tagsArr.Select(t => (FieldValue)t).ToArray())
            });
        }

        if (!string.IsNullOrWhiteSpace(platform))
            filter.Add(new TermQuery(new Field("platform")) { Value = platform });

        // 1) tentativa baseada nos gostos
        var req = new SearchRequest<Product>(_index)
        {
            Query = new BoolQuery
            {
                Should = should,
                MinimumShouldMatch = should.Count > 0 ? 1 : 0,
                Filter = filter
            },
            Size = size,
            Sort = new List<SortOptions>
        {
            SortOptions.Field(new Field("popularityScore"), new FieldSort { Order = SortOrder.Desc })
        }
        };

        var resp = await _client.SearchAsync<Product>(req, ct);
        if (!resp.IsValidResponse)
            throw new InvalidOperationException($"Recommend failed: {resp.ElasticsearchServerError?.Error?.Reason}");

        // materializa para permitir merge com fallback
        var docs = resp.Documents.ToList();

        // 2) fallback: completa com mais populares da plataforma
        if (docs.Count < size)
        {
            var fallbackReq = new SearchRequest<Product>(_index)
            {
                Query = new BoolQuery { Filter = filter }, // active + (platform?)
                Size = size,
                Sort = new List<SortOptions>
            {
                SortOptions.Field(new Field("popularityScore"), new FieldSort { Order = SortOrder.Desc })
            }
            };

            var fallbackResp = await _client.SearchAsync<Product>(fallbackReq, ct);
            if (!fallbackResp.IsValidResponse)
                throw new InvalidOperationException($"Fallback failed: {fallbackResp.ElasticsearchServerError?.Error?.Reason}");

            var seen = new HashSet<Guid>(docs.Select(d => d.Id));
            foreach (var p in fallbackResp.Documents)
            {
                if (docs.Count >= size) break;
                if (seen.Add(p.Id)) docs.Add(p);
            }
        }

        return docs;
    }

    public async Task<PopularMetricsResult> PopularMetricsAsync(CancellationToken ct = default)
    {
        var aggs = new Dictionary<string, Aggregation>
        {
            ["by_genre"] = Aggregation.Terms(new TermsAggregation { Field = new Field("genre"), Size = 20 }),
            ["by_platform"] = Aggregation.Terms(new TermsAggregation { Field = new Field("platform"), Size = 20 })
        };

        var aggReq = new SearchRequest<Product>(_index)
        {
            Size = 0,
            Aggregations = aggs
        };

        var aggResp = await _client.SearchAsync<Product>(aggReq, ct);
        if (!aggResp.IsValidResponse)
            throw new InvalidOperationException($"Aggs failed: {aggResp.ElasticsearchServerError?.Error?.Reason}");

        var aggsDict = aggResp.Aggregations;
        var byGenreAgg = aggsDict?.GetStringTerms("by_genre");
        var byPlatformAgg = aggsDict?.GetStringTerms("by_platform");

        var byGenre = (byGenreAgg?.Buckets ?? Array.Empty<StringTermsBucket>())
            .Select(b => (b.Key.ToString()!, b.DocCount))
            .ToList();

        var byPlatform = (byPlatformAgg?.Buckets ?? Array.Empty<StringTermsBucket>())
            .Select(b => (b.Key.ToString()!, b.DocCount))
            .ToList();

        // top por popularidade (já ok)
        var topReq = new SearchRequest<Product>(_index)
        {
            Query = new TermQuery(new Field("active")) { Value = true },
            Sort = new List<SortOptions> { SortOptions.Field(new Field("popularityScore"), new FieldSort { Order = SortOrder.Desc }) },
            Size = 10
        };

        var topResp = await _client.SearchAsync<Product>(topReq, ct);
        if (!topResp.IsValidResponse)
            throw new InvalidOperationException($"Top search failed: {topResp.ElasticsearchServerError?.Error?.Reason}");

        var top = topResp.Documents.Select(d => (d.Name, (long)d.PopularityScore)).ToList();

        return new PopularMetricsResult(byGenre, byPlatform, top);
    }

    private static long ReadTotal<T>(SearchResponse<T> resp)
    {
        var totalOpt = resp.HitsMetadata?.Total;

        if (totalOpt is null)
            return resp.Documents.Count;

        if (totalOpt is { } u)
        {
            return u.Match(
                (TotalHits th) => th.Value,
                (long l) => l
            );
        }

        return resp.Documents.Count;
    }
}