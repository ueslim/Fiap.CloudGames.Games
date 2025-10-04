using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using FIAP.CloudGames.Catalog.API.Data;
using FIAP.CloudGames.Catalog.API.Data.Repository;
using FIAP.CloudGames.Catalog.API.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FIAP.CloudGames.Catalog.API.Tests.Unit
{
    public class ProductRepositoryTests
    {
        private static CatalogContext NewContext()
        {
            var opts = new DbContextOptionsBuilder<CatalogContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()) // DB novo por teste
                .Options;

            return new CatalogContext(opts);
        }

        private static Product NewProduct(bool active = true, string? name = null)
        {
            return new Product
            {
                Id = Guid.NewGuid(),
                Name = name ?? "Test Game",
                Description = "Desc",
                Active = active,
                Value = 59.99m,
                DateRegister = DateTime.UtcNow,
                Image = "img.jpg",
                StockQuantity = 7,
                Genre = "RPG",
                Platform = "PC",
                Tags = new[] { "tag1", "tag2" },
                Metacritic = 90,
                UserRating = 4.5,
                ReleaseDate = new DateTime(2020, 1, 1),
                PopularityScore = 1000,
                Sales = 10,
                Views = 100
            };
        }

        [Fact]
        public async Task GetAll_Should_Return_All_Products()
        {
            using var ctx = NewContext();
            ctx.Products.AddRange(NewProduct(), NewProduct(), NewProduct());
            await ctx.SaveChangesAsync();

            using var repo = new ProductRepository(ctx);

            var all = await repo.GetAll();

            all.Should().NotBeNull();
            all.Should().HaveCount(3);
        }

        [Fact]
        public async Task GetById_Should_Return_Product_When_Exists()
        {
            using var ctx = NewContext();
            var p = NewProduct(name: "Existing");
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();

            using var repo = new ProductRepository(ctx);

            var found = await repo.GetById(p.Id);

            found.Should().NotBeNull();
            found!.Id.Should().Be(p.Id);
            found.Name.Should().Be("Existing");
        }

        [Fact]
        public async Task GetById_Should_Return_Null_When_Not_Exists()
        {
            using var ctx = NewContext();
            using var repo = new ProductRepository(ctx);

            var found = await repo.GetById(Guid.NewGuid());

            found.Should().BeNull(); // assinatura é Product, mas FindAsync pode retornar null
        }

        [Fact]
        public async Task GetProductsById_Should_Return_Only_Active_And_Valid_Guids()
        {
            using var ctx = NewContext();

            var active1 = NewProduct(active: true, name: "A1");
            var active2 = NewProduct(active: true, name: "A2");
            var inactive1 = NewProduct(active: false, name: "I1");

            ctx.Products.AddRange(active1, active2, inactive1);
            await ctx.SaveChangesAsync();

            using var repo = new ProductRepository(ctx);

            // inclui um GUID inválido e o inativo
            var csv = $"{active1.Id},{inactive1.Id},not-a-guid,{active2.Id}";
            var list = await repo.GetProductsById(csv);

            list.Should().HaveCount(2);
            list.Select(p => p.Id).Should().BeEquivalentTo(new[] { active1.Id, active2.Id });
            list.Should().OnlyContain(p => p.Active);
        }

        [Fact]
        public async Task Add_Should_Persist_On_Commit()
        {
            using var ctx = NewContext();
            using var repo = new ProductRepository(ctx);

            var p = NewProduct(name: "Persist Me");
            repo.Add(p);

            var committed = await repo.UnitOfWork.Commit();
            committed.Should().BeTrue();

            var fromDb = await ctx.Products.FindAsync(p.Id);
            fromDb.Should().NotBeNull();
            fromDb!.Name.Should().Be("Persist Me");
        }

        [Fact]
        public async Task Update_Should_Persist_Changes_On_Commit()
        {
            using var ctx = NewContext();
            var p = NewProduct(name: "Before");
            ctx.Products.Add(p);
            await ctx.SaveChangesAsync();

            using var repo = new ProductRepository(ctx);

            p.Name = "After";
            p.StockQuantity = 99;
            repo.Update(p);

            var committed = await repo.UnitOfWork.Commit();
            committed.Should().BeTrue();

            var fromDb = await ctx.Products.FindAsync(p.Id);
            fromDb!.Name.Should().Be("After");
            fromDb.StockQuantity.Should().Be(99);
        }

        [Fact]
        public async Task GetProductsById_Should_Return_Empty_When_All_Guids_Invalid()
        {
            using var ctx = NewContext();
            using var repo = new ProductRepository(ctx);

            var list = await repo.GetProductsById("foo,bar,baz");
            list.Should().BeEmpty();
        }

        [Fact]
        public async Task GetProductsById_Should_Handle_Empty_String()
        {
            using var ctx = NewContext();
            using var repo = new ProductRepository(ctx);

            var list = await repo.GetProductsById(string.Empty);
            list.Should().BeEmpty();
        }
    }
}