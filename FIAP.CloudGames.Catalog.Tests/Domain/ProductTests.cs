using System;
using FIAP.CloudGames.Catalog.API.Models;
using FluentAssertions;
using Xunit;

namespace FIAP.CloudGames.Catalog.Tests.Domain
{
    public class ProductTests
    {
        private static Product NewProduct(
            bool active = true,
            int stock = 10,
            decimal value = 59.99m)
        {
            return new Product
            {
                Name = "Game X",
                Description = "Desc",
                Active = active,
                Value = value,
                DateRegister = DateTime.UtcNow,
                Image = "https://example.com/img.jpg",
                StockQuantity = stock,
                Genre = "RPG",
                Platform = "PC",
                Tags = new[] { "tag1", "tag2" },
                Metacritic = 90,
                UserRating = 4.5,
                ReleaseDate = new DateTime(2022, 1, 1),
                PopularityScore = 1000,
                Sales = 50000,
                Views = 100000
            };
        }

        [Fact]
        public void IsAvailable_ShouldBeTrue_WhenActiveAndStockEnough()
        {
            var p = NewProduct(active: true, stock: 5);
            var ok = p.IsAvailable(3);
            ok.Should().BeTrue();
        }

        [Fact]
        public void IsAvailable_ShouldBeFalse_WhenNotActive()
        {
            var p = NewProduct(active: false, stock: 100);
            var ok = p.IsAvailable(1);
            ok.Should().BeFalse("produto inativo não pode estar disponível");
        }

        [Fact]
        public void IsAvailable_ShouldBeFalse_WhenInsufficientStock()
        {
            var p = NewProduct(active: true, stock: 2);
            var ok = p.IsAvailable(3);
            ok.Should().BeFalse();
        }

        [Fact]
        public void DecrementStock_ShouldReduce_WhenEnoughStock()
        {
            var p = NewProduct(stock: 10);
            p.DecrementStock(4);
            p.StockQuantity.Should().Be(6);
        }

        [Fact]
        public void DecrementStock_ShouldNotChange_WhenInsufficientStock()
        {
            var p = NewProduct(stock: 2);
            p.DecrementStock(5);
            p.StockQuantity.Should().Be(2, "o método só decrementa quando há estoque suficiente");
        }

        [Fact]
        public void Tags_ShouldNeverBeNull_ByDefaultEmptyArray()
        {
            var p = new Product
            {
                Name = "Game",
                Description = "Desc",
                Active = true,
                Value = 10,
                DateRegister = DateTime.UtcNow,
                Image = "img",
                StockQuantity = 0,
                Genre = "RPG",
                Platform = "PC"
            };

            p.Tags.Should().NotBeNull();
            p.Tags.Should().BeEmpty();
        }

        [Fact]
        public void Properties_ShouldPersistValues_Assigned()
        {
            var now = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);

            var p = new Product
            {
                Name = "The Witcher 3",
                Description = "Story-rich open-world RPG.",
                Active = true,
                Value = 39.99m,
                DateRegister = now,
                Image = "https://example.com/witcher3.jpg",
                StockQuantity = 25,
                Genre = "RPG",
                Platform = "PC",
                Tags = new[] { "open-world", "story-rich", "rpg" },
                Metacritic = 93,
                UserRating = 4.8,
                ReleaseDate = new DateTime(2015, 5, 19),
                PopularityScore = 10000,
                Sales = 50000000,
                Views = 400000
            };

            p.Name.Should().Be("The Witcher 3");
            p.Description.Should().Contain("open-world");
            p.Active.Should().BeTrue();
            p.Value.Should().Be(39.99m);
            p.DateRegister.Should().Be(now);
            p.Image.Should().StartWith("https://");
            p.StockQuantity.Should().Be(25);
            p.Genre.Should().Be("RPG");
            p.Platform.Should().Be("PC");
            p.Tags.Should().Contain(new[] { "open-world", "rpg" });
            p.Metacritic.Should().Be(93);
            p.UserRating.Should().BeApproximately(4.8, 0.0001);
            p.ReleaseDate.Should().Be(new DateTime(2015, 5, 19));
            p.PopularityScore.Should().Be(10000);
            p.Sales.Should().Be(50000000);
            p.Views.Should().Be(400000);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        public void IsAvailable_ShouldRequireNonNegativeQuantity(int qty)
        {
            var p = NewProduct(active: true, stock: 10);
            var ok = p.IsAvailable(qty);
            ok.Should().BeTrue("com estoque 10, quantidades 0..10 são atendidas (0 é um no-op permitido)");
        }

        [Fact]
        public void DecrementStock_WithZero_ShouldKeepStock()
        {
            var p = NewProduct(stock: 7);
            p.DecrementStock(0);
            p.StockQuantity.Should().Be(7);
        }
    }
}