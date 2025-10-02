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
        }
    }
}