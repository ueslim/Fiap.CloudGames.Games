using System.Text.Json;
using FIAP.CloudGames.Catalog.API.Data;
using FIAP.CloudGames.Catalog.API.Models;

namespace FIAP.CloudGames.Catalog.API.Configuration
{
    public class CatalogContextSeed
    {
        private const string SeedPath = "Data/Seed/products.seed.json";

        public static async Task EnsureSeedProducts(CatalogContext context)
        {
            if (context.Products.Any()) return;

            List<Product>? products = null;

            if (File.Exists(SeedPath))
            {
                var json = await File.ReadAllTextAsync(SeedPath);
                var opts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                products = JsonSerializer.Deserialize<List<Product>>(json, opts);
            }

            products ??= GetFallbackProducts();

            foreach (var p in products)
            {
                if (p.Id == Guid.Empty) p.Id = Guid.NewGuid();
                if (p.DateRegister == default) p.DateRegister = DateTime.UtcNow;
                p.Active = p.Active;
                p.Tags ??= Array.Empty<string>();
            }

            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }

        private static List<Product> GetFallbackProducts() => new()
        {
            new Product
            {
                Name = "Sample Game",
                Description = "Fallback product in case JSON is missing.",
                Value = 9.99m,
                Image = "https://via.placeholder.com/300x400",
                StockQuantity = 10,
                Active = true,
                DateRegister = DateTime.UtcNow,
                Genre = "Indie",
                Platform = "PC",
                Tags = new []{"indie","sample"},
                Metacritic = 80,
                UserRating = 4.0,
                ReleaseDate = new DateTime(2020,1,1),
                PopularityScore = 100,
                Sales = 1000,
                Views = 500
            }
        };
    }
}
