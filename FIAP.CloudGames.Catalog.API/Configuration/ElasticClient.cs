using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace FIAP.CloudGames.Catalog.API.Configuration
{
    public interface IElasticClient<T>
    {
        Task<IReadOnlyCollection<T>> Get(int page, int size, IndexName index);
        Task<bool> Create(T log, IndexName index);
    }

    public class ElasticClient<T> : IElasticClient<T>
    {
        private readonly ElasticsearchClient _client;

        public ElasticClient(IElasticSettings settings)
        {
            var options = new ElasticsearchClientSettings(new Uri(settings.Uri))
                .Authentication(new BasicAuthentication(settings.Username, settings.Password));

            _client = new ElasticsearchClient(options);
        }

        public async Task<IReadOnlyCollection<T>> Get(int page, int size, IndexName index)
        {
            var response = await _client.SearchAsync<T>(s => s
                .Index(index)
                .From(page)
                .Size(size)
            );

            return response.Documents;
        }

        public async Task<bool> Create(T log, IndexName index)
        {
            var response = await _client.IndexAsync<T>(log, i => i.Index(index));

            if (response.IsValidResponse)
            {
                return true;
            }
            else
            {
                Console.WriteLine("Erro ao indexar documento:");

                Console.WriteLine("DebugInformation: " + response.DebugInformation);
                Console.WriteLine("HTTP Status: " + response.ApiCallDetails?.HttpStatusCode);
                Console.WriteLine("Response Body: " + response.ApiCallDetails?.ResponseBodyInBytes != null
                    ? System.Text.Encoding.UTF8.GetString(response.ApiCallDetails.ResponseBodyInBytes)
                    : "sem corpo de resposta");

                if (response.ElasticsearchServerError is not null)
                {
                    Console.WriteLine("Error Type: " + response.ElasticsearchServerError.Error.Type);
                    Console.WriteLine("Reason: " + response.ElasticsearchServerError.Error.Reason);
                }

                return false;
            }
        }
    }

    // Cloud
    //    public ElasticClient(IElasticSettings settings)
    //    {
    //        this._client = new ElasticsearchClient(settings.CloudId, new ApiKey(settings.ApiKey));
    //    }
}
