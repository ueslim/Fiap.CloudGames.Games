using FIAP.CloudGames.Catalog.API.Data;
using FIAP.CloudGames.Catalog.API.Data.Repository;
using FIAP.CloudGames.Catalog.API.Data.Search;
using FIAP.CloudGames.Catalog.API.Models;

namespace FIAP.CloudGames.Catalog.API.Configuration
{
    public static class DependencyInjectionConfig
    {
        public static void RegisterServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<CatalogContext>();

            services.AddElasticsearch(configuration);
            services.AddScoped<IProductSearchService, ElasticsearchProductSearchService>();
        }
    }
}