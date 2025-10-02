using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FIAP.CloudGames.Catalog.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FIAP.CloudGames.Catalog.API.Data.Search;

public interface IProductSearchService
{
    Task IndexAsync(Product p, CancellationToken ct = default);

    Task BulkIndexAllFromDatabase(CatalogContext db, CancellationToken ct = default);

    Task<IReadOnlyCollection<Product>> SearchAsync(string q, string? platform, string? genre, string[]? tags, int page = 1, int size = 20, CancellationToken ct = default);

    Task<IReadOnlyCollection<Product>> RecommendForUserAsync(string userId, IEnumerable<string> likedGenres, IEnumerable<string> likedTags, string? platform, int size = 20, CancellationToken ct = default);

    Task<PopularMetricsResult> PopularMetricsAsync(CancellationToken ct = default);
}

public record PopularMetricsResult(
    IEnumerable<(string Genre, long Count)> TopByGenre,
    IEnumerable<(string Platform, long Count)> TopByPlatform,
    IEnumerable<(string Game, long Popularity)> TopGamesOverall
);

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
            d => d
                .Conflicts(Conflicts.Proceed)
                .Slices(new Slices(SlicesCalculation.Auto))
                .Refresh(true)
                .Query(q => q.MatchAll(new MatchAllQuery())),
            ct);

        var active = await db.Products.AsNoTracking()
                                      .Where(p => p.Active)
                                      .ToListAsync(ct);

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



    public async Task<IReadOnlyCollection<Product>> SearchAsync(
        string q, string? platform, string? genre, string[]? tags, int page = 1, int size = 20, CancellationToken ct = default)
    {
        var must = new List<Query>();
        var should = new List<Query>();
        var filter = new List<Query>();

        if (!string.IsNullOrWhiteSpace(q))
        {
            should.Add(new MultiMatchQuery
            {
                Query = q,
                Fields = new[] { "name^4", "tags^3", "genre^2", "description" },
                Type = TextQueryType.BestFields,
                Fuzziness = new Fuzziness(2)
            });
        }

        if (!string.IsNullOrWhiteSpace(platform))
            must.Add(new TermQuery(new Field("platform.keyword")) { Value = platform });

        if (!string.IsNullOrWhiteSpace(genre))
            must.Add(new TermQuery(new Field("genre.keyword")) { Value = genre });

        if (tags is { Length: > 0 })
        {
            var tagValues = tags.Select(t => (FieldValue)t).ToList();
            must.Add(new TermsQuery
            {
                Field = new Field("tags.keyword"),
                Term = new TermsQueryField(tagValues)
            });
        }

        filter.Add(new TermQuery(new Field("active")) { Value = true });

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
            Sort = new List<SortOptions>
                    {
                        SortOptions.Field(new Field("popularityScore"), new FieldSort { Order = SortOrder.Desc }),
                        SortOptions.Field(new Field("metacritic"),       new FieldSort { Order = SortOrder.Desc })
                    }
        };

        var resp = await _client.SearchAsync<Product>(req, ct);
        if (!resp.IsValidResponse)
            throw new InvalidOperationException($"Search failed: {resp.ElasticsearchServerError?.Error?.Reason}");

        return resp.Documents;
    }

    public async Task<IReadOnlyCollection<Product>> RecommendForUserAsync(
        string userId, IEnumerable<string> likedGenres, IEnumerable<string> likedTags, string? platform, int size = 20, CancellationToken ct = default)
    {
        var should = new List<Query>();
        var filter = new List<Query> { new TermQuery(new Field("active")) { Value = true } };

        var genresArr = likedGenres?.Where(g => !string.IsNullOrWhiteSpace(g)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        var tagsArr = likedTags?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();

        if (genresArr.Length > 0)
        {
            var genreValues = genresArr.Select(g => (FieldValue)g).ToList();
            should.Add(new TermsQuery
            {
                Field = new Field("genre.keyword"),
                Term = new TermsQueryField(genreValues)
            });
        }

        if (tagsArr.Length > 0)
        {
            var tagValues = tagsArr.Select(t => (FieldValue)t).ToList();
            should.Add(new TermsQuery
            {
                Field = new Field("tags.keyword"),
                Term = new TermsQueryField(tagValues)
            });
        }

        if (!string.IsNullOrWhiteSpace(platform))
            filter.Add(new TermQuery(new Field("platform.keyword")) { Value = platform });

        var req = new SearchRequest<Product>(_index)
        {
            Query = new FunctionScoreQuery
            {
                Query = new BoolQuery
                {
                    Should = should,
                    MinimumShouldMatch = should.Count > 0 ? 1 : 0,
                    Filter = filter
                },
                Functions = new List<FunctionScore>
{
                FunctionScore.FieldValueFactor(new FieldValueFactorScoreFunction
                {
                    Field    = new Field("popularityScore"),
                    Factor   = 1.0,
                    Modifier = FieldValueFactorModifier.Log1p
                })
             },
                BoostMode = FunctionBoostMode.Sum,
                ScoreMode = FunctionScoreMode.Sum
            },
            Size = size
        };

        var resp = await _client.SearchAsync<Product>(req, ct);
        if (!resp.IsValidResponse)
            throw new InvalidOperationException($"Recommend failed: {resp.ElasticsearchServerError?.Error?.Reason}");

        return resp.Documents;
    }

    public async Task<PopularMetricsResult> PopularMetricsAsync(CancellationToken ct = default)
    {
     
        var aggs = new Dictionary<string, Aggregation>
        {
            ["by_genre"] = Aggregation.Terms(new TermsAggregation { Field = new Field("genre.keyword"), Size = 20 }),
            ["by_platform"] = Aggregation.Terms(new TermsAggregation { Field = new Field("platform.keyword"), Size = 20 })
        };

        var aggReq = new SearchRequest<Product>(_index)
        {
            Size = 0,
            Aggregations = aggs
        };

        var aggResp = await _client.SearchAsync<Product>(aggReq, ct);
        if (!aggResp.IsValidResponse)
            throw new InvalidOperationException($"Aggs failed: {aggResp.ElasticsearchServerError?.Error?.Reason}");

       
        var byGenreAgg = aggResp.Aggregations.GetStringTerms("by_genre");
        var byPlatformAgg = aggResp.Aggregations.GetStringTerms("by_platform");

        var byGenre = (byGenreAgg?.Buckets ?? Array.Empty<StringTermsBucket>())
      .Select(b => (b.Key.ToString()!, b.DocCount))
      .ToList();

        var byPlatform = (byPlatformAgg?.Buckets ?? Array.Empty<StringTermsBucket>())
            .Select(b => (b.Key.ToString()!, b.DocCount))
            .ToList();

      
        var topReq = new SearchRequest<Product>(_index)
        {
            Query = new TermQuery(new Field("active")) { Value = true },
            Sort = new List<SortOptions>
        {
            SortOptions.Field(new Field("popularityScore"), new FieldSort { Order = SortOrder.Desc })
        },
            Size = 10
        };

        var topResp = await _client.SearchAsync<Product>(topReq, ct);
        if (!topResp.IsValidResponse)
            throw new InvalidOperationException($"Top search failed: {topResp.ElasticsearchServerError?.Error?.Reason}");

        var top = topResp.Documents.Select(d => (d.Name, (long)d.PopularityScore)).ToList();

        return new PopularMetricsResult(byGenre, byPlatform, top);
    }
}