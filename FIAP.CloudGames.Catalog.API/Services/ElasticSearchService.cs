using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.Aggregations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FIAP.CloudGames.Catalog.API.Configuration;

namespace FIAP.CloudGames.Catalog.API.Services
{
    public interface IElasticSearchService<T>
    {
        Task<bool> IndexDocumentAsync(T doc, string indexName);
        Task<IReadOnlyCollection<T>> GetDocumentsAsync(int page, int size, string indexName);
        Task<IReadOnlyCollection<T>> SearchAsync(string query, string indexName, string sortBy = null, bool descending = false);
        Task<IReadOnlyCollection<T>> RecommendAsync(string genre, string indexName);
        Task<object> AggregateAsync(string indexName, string field);
    }

    public class ElasticSearchService<T> : IElasticSearchService<T> where T : class
    {
        private readonly IElasticClient<T> _elasticClient;
        private readonly ElasticsearchClient _rawClient;

        public ElasticSearchService(IElasticClient<T> elasticClient, ElasticsearchClient rawClient)
        {
            _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
            _rawClient = rawClient ?? throw new ArgumentNullException(nameof(rawClient));
        }

        public async Task<bool> IndexDocumentAsync(T doc, string indexName)
        {
            return await _elasticClient.Create(doc, indexName);
        }

        public async Task<IReadOnlyCollection<T>> GetDocumentsAsync(int page, int size, string indexName)
        {
            return await _elasticClient.Get(page, size, indexName);
        }

        public async Task<IReadOnlyCollection<T>> SearchAsync(string query, string indexName, string sortBy = null, bool descending = false)
        {
            var response = await _rawClient.SearchAsync<T>(s => s
                .Index(indexName)
                .Query(q => q
                    .MultiMatch(m => m
                        .Fields(new[] { "name", "description", "genre", "platform" })
                        .Query(query)
                        .Type(TextQueryType.BestFields)
                    )
                )
                .Size(20)
                .Sort(s => s
                    .Field(new Field("popularity"), f => f.Order(SortOrder.Desc))
                )
            );

            return response.IsValidResponse ? response.Documents : Array.Empty<T>();
        }

        public async Task<IReadOnlyCollection<T>> RecommendAsync(string genre, string indexName)
        {
            var response = await _rawClient.SearchAsync<T>(s => s
                .Index(indexName)
                .Query(q => q
                    .Match(m => m
                        .Field(new Field("genre"))
                        .Query(genre)
                    )
                )
                .Sort(s => s
                    .Field(new Field("popularity"), f => f.Order(SortOrder.Desc))
                )
                .Size(10)
            );

            return response.IsValidResponse ? response.Documents : Array.Empty<T>();
        }

        public async Task<object> AggregateAsync(string indexName, string field)
        {
            var fieldName = field.ToLower();
            var response = await _rawClient.SearchAsync<T>(s => s
                .Index(indexName)
                .Size(0)
                .Aggregations(a => a
                    .Terms("group_by_field", t => t
                        .Field(new Field(fieldName))
                        .Size(10)
                    )
                    .Avg("avg_" + fieldName, avg => avg
                        .Field(new Field(fieldName))
                    )
                )
            );

            if (!response.IsValidResponse)
                return new { Terms = Array.Empty<object>(), Average = (double?)null };

            var termsAgg = response.Aggregations
                    .GetStringTerms("group_by_field")?
                    .Buckets
                    .Select(b => new { Key = b.Key.ToString(), Count = b.DocCount })
                    .ToList();

            var avgAgg = response.Aggregations
                .GetAverage("avg_" + fieldName)?
                .Value;

            return new
            {
                Terms = termsAgg,
                Average = avgAgg
            };
        }
    }
}