using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport;
using Microsoft.Extensions.Options;

namespace FIAP.CloudGames.Catalog.API.Configuration
{
    public static class ElasticsearchConfig
    {
        public class ElasticsearchOptions
        {
            public string Uri { get; set; } = "http://localhost:9200";
            public string Username { get; set; } = "elastic";
            public string Password { get; set; } = "changeme";
            public string IndexName { get; set; } = "cloudgames-products";
        }

        public static void AddElasticsearch(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ElasticsearchOptions>(configuration.GetSection("Elasticsearch"));

            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<ElasticsearchOptions>>().Value;

                var settings = new ElasticsearchClientSettings(new Uri(opts.Uri))
                    .DefaultIndex(opts.IndexName)
                    .Authentication(new BasicAuthentication(opts.Username, opts.Password));

                return new ElasticsearchClient(settings);
            });

            services.AddHostedService<ElasticsearchIndexBootstrapper>();
        }
    }

    /// <summary>Cria o índice com analyzers/mapping (client v8).</summary>
    public class ElasticsearchIndexBootstrapper : IHostedService
    {
        private readonly ElasticsearchClient _client;
        private readonly IOptions<ElasticsearchConfig.ElasticsearchOptions> _opts;

        public ElasticsearchIndexBootstrapper(
            ElasticsearchClient client,
            IOptions<ElasticsearchConfig.ElasticsearchOptions> opts)
        {
            _client = client;
            _opts = opts;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var index = _opts.Value.IndexName;

            var exists = await _client.Indices.ExistsAsync(index, cancellationToken);
            if (exists.Exists) return;

            var createReq = new CreateIndexRequest(index)
            {
                Settings = new IndexSettings
                {
                    Analysis = new IndexSettingsAnalysis
                    {
                        Tokenizers = new Tokenizers
                                {
                                    {
                                        "edge_ngram_tokenizer",
                                        new EdgeNGramTokenizer
                                        {
                                            MinGram = 2,
                                            MaxGram = 20,
                                            TokenChars = new[]
                                            {
                                                TokenChar.Letter,
                                                TokenChar.Digit
                                            }
                                        }
                                    }
                                },
                        Analyzers = new Analyzers
                                {
                                    {
                                        "autocomplete",
                                        new CustomAnalyzer
                                        {
                                            Tokenizer = "edge_ngram_tokenizer",
                                            Filter = new[] { "lowercase" }
                                        }
                                    },
                                    {
                                        "autocomplete_search",
                                        new CustomAnalyzer
                                        {
                                            Tokenizer = "standard",
                                            Filter = new[] { "lowercase" }
                                        }
                                    }
        }
                    }
                }
,
                Mappings = new TypeMapping
                {
                    Properties = new Properties
                    {
                        { "id", new KeywordProperty() },
                        { "name", new TextProperty { Analyzer = "autocomplete", SearchAnalyzer = "autocomplete_search" } },
                        { "description", new TextProperty { Analyzer = "standard" } },
                        { "platform", new KeywordProperty() },
                        { "genre", new KeywordProperty() },
                        { "tags", new KeywordProperty() },
                        { "value", new DoubleNumberProperty() },
                        { "metacritic", new IntegerNumberProperty() },
                        { "userRating", new DoubleNumberProperty() },
                        { "releaseDate", new DateProperty() },
                        { "active", new BooleanProperty() },
                        { "popularityScore", new LongNumberProperty() },
                        { "sales", new LongNumberProperty() },
                        { "views", new LongNumberProperty() },
                        { "image", new KeywordProperty() }
                    }
                }
            };

            var resp = await _client.Indices.CreateAsync(createReq, cancellationToken);
            if (!resp.IsValidResponse)
            {
                throw new InvalidOperationException($"Failed to create index '{index}': {resp.ElasticsearchServerError?.Error?.Reason}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}