using FIAP.CloudGames.Catalog.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FIAP.CloudGames.Catalog.API.Data.Mappings
{
    public class ProductMapping : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name).IsRequired().HasColumnType("varchar(250)");
            builder.Property(c => c.Description).IsRequired().HasColumnType("varchar(500)");
            builder.Property(c => c.Image).IsRequired().HasColumnType("varchar(250)");

            builder.Property(c => c.Genre).HasColumnType("varchar(100)");
            builder.Property(c => c.Platform).HasColumnType("varchar(50)");
            builder.Property(c => c.Metacritic);
            builder.Property(c => c.UserRating);
            builder.Property(c => c.ReleaseDate);
            builder.Property(c => c.PopularityScore);
            builder.Property(c => c.Sales);
            builder.Property(c => c.Views);

            builder.Property(c => c.Tags)
                .HasConversion(
                    v => string.Join(';', v ?? Array.Empty<string>()),
                    v => string.IsNullOrWhiteSpace(v) ? Array.Empty<string>() : v.Split(';', StringSplitOptions.RemoveEmptyEntries))
                .HasColumnType("varchar(1000)");

            builder.ToTable("Products");
        }
    }
}