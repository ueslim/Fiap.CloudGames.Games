using FIAP.CloudGames.Catalog.API.Models;
using FIAP.CloudGames.Catalog.API.Services;
using FIAP.CloudGames.WebAPI.Core.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.CloudGames.Catalog.API.Controllers
{
    [Authorize]
    public class CatalogController : MainController
    {
        private readonly IProductRepository _productRepository;
        private readonly IElasticSearchService<SearchLog> _elasticService;
        private readonly IElasticSearchService<Product> _elasticProductService;

        public CatalogController(IProductRepository productRepository,
                                 IElasticSearchService<SearchLog> elasticService,
                                 IElasticSearchService<Product> elasticProductService)
        {
            _productRepository = productRepository;
            _elasticService = elasticService;
            _elasticProductService = elasticProductService;
        }

        [AllowAnonymous]
        [HttpGet("catalog/products")]
        public async Task<IEnumerable<Product>> Index()
        {
            var products = await _productRepository.GetAll();

            // Cria um log no Elasticsearch
            var log = new SearchLog
            {
                Action = "Listagem de produtos"
            };

            try
            {
                Console.WriteLine("Enviando log para Elasticsearch...");
                var success = await _elasticService.IndexDocumentAsync(log, "catalog-logs");
                Console.WriteLine("Log enviado? " + success);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao enviar log: " + ex.Message);
            }

            return products;
        }

        [HttpGet("catalog/products/{id}")]
        public async Task<Product> ProductDetail(Guid id)
        {
            return await _productRepository.GetById(id);
        }

        [HttpGet("catalog/products/list/{ids}")]
        public async Task<IEnumerable<Product>> GetProductsById(string ids)
        {
            return await _productRepository.GetProductsById(ids);
        }

        [AllowAnonymous]
        [HttpGet("catalog/products/search")]
        public async Task<IEnumerable<Product>> SearchProducts([FromQuery] string query)
        {
            return await _elasticProductService.SearchAsync(query, "products");
        }

        [AllowAnonymous]
        [HttpGet("catalog/products/recommendations")]
        public async Task<IEnumerable<Product>> RecommendProducts([FromQuery] string genre)
        {
            // Busca produtos do mesmo gênero, ordenados por Popularity
            return await _elasticProductService.SearchAsync(
                genre,
                "products",
                sortBy: "Popularity",
                descending: true
            );
        }

        [AllowAnonymous]
        [HttpGet("catalog/products/metrics/popular")]
        public async Task<object> GetPopularGamesMetrics()
        {
            return await _elasticProductService.AggregateAsync("products", "Popularity");
        }
    }
}