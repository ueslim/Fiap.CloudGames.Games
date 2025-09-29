using FIAP.CloudGames.Catalog.API.Models;
using FIAP.CloudGames.Core.DomainObjects;
using FIAP.CloudGames.Core.Messages.Integration;

namespace FIAP.CloudGames.Catalog.API.Services
{
    // Message bus removed; disable background handler.
    public class CatalogIntegrationHandler : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public CatalogIntegrationHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            SetSubscribers();
            return Task.CompletedTask;
        }

        private void SetSubscribers()
        {
            // No-op: message bus removed
        }

        private async Task DeductStock(OrderAuthorizedIntegrationEvent message)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var productsWithStock = new List<Product>();
                var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();

                var idsProducts = string.Join(",", message.Items.Select(c => c.Key));
                var products = await productRepository.GetProductsById(idsProducts);

                if (products.Count != message.Items.Count)
                {
                    CancelOrderForInsufficientStock(message);
                    return;
                }

                foreach (var product in products)
                {
                    var productQuantity = message.Items.FirstOrDefault(p => p.Key == product.Id).Value;

                    if (product.IsAvailable(productQuantity))
                    {
                        product.DecrementStock(productQuantity);
                        productsWithStock.Add(product);
                    }
                }

                if (productsWithStock.Count != message.Items.Count)
                {
                    CancelOrderForInsufficientStock(message);
                    return;
                }

                foreach (var produto in productsWithStock)
                {
                    productRepository.Update(produto);
                }

                if (!await productRepository.UnitOfWork.Commit())
                {
                    throw new DomainException($"Problemas ao atualizar estoque do pedido {message.OrderId}");
                }

                // TODO: integrate via HTTP or other mechanism if needed
            }
        }

        public async void CancelOrderForInsufficientStock(OrderAuthorizedIntegrationEvent message)
        {
            // TODO: integrate via HTTP or other mechanism if needed
        }
    }
}