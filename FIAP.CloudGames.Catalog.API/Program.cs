using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using FIAP.CloudGames.Catalog.API.Configuration;
using FIAP.CloudGames.Catalog.API.Data;
using FIAP.CloudGames.Catalog.API.Data.Search;
using FIAP.CloudGames.WebAPI.Core.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

LoggingConfig.ConfigureBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Serilog + OTLP para logs
builder.ConfigureSerilogWithOpenTelemetry("catalog-api");

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Services.AddApiConfiguration(builder.Configuration);
builder.Services.AddJwtConfiguration(builder.Configuration);

builder.Services.AddSwaggerConfiguration();

builder.Services.RegisterServices(builder.Configuration);

// OpenTelemetry Tracing + Metrics
builder.Services.AddObservabilityConfiguration(builder.Configuration);

var app = builder.Build();

// SEED + Index
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CatalogContext>();

    if ((await context.Database.GetPendingMigrationsAsync()).Any())
        await context.Database.MigrateAsync();

    await CatalogContextSeed.EnsureSeedProducts(context);

    // Garantir que o índice existe
    var es = scope.ServiceProvider.GetRequiredService<ElasticsearchClient>();
    var esOpts = scope.ServiceProvider.GetRequiredService<IOptions<ElasticsearchConfig.ElasticsearchOptions>>().Value;
    await EnsureIndexAsync(es, esOpts.IndexName);

    // Indexar tudo no ES na subida
    var search = scope.ServiceProvider.GetRequiredService<IProductSearchService>();
    await search.BulkIndexAllFromDatabase(context);
}

app.UseSwaggerConfiguration();

app.UseApiConfiguration(app.Environment);

// Logs enriquecidos com user_id
app.UseRequestLogEnrichment();

app.Run();

static async Task EnsureIndexAsync(ElasticsearchClient client, string index)
{
    var exists = await client.Indices.ExistsAsync(index);
    if (exists.Exists) return;

    var resp = await client.Indices.CreateAsync(index, c => c
        .Settings(s => s
            .NumberOfShards(1)
            .NumberOfReplicas(0)
        )
        .Mappings(new TypeMapping
        {
            Properties = new Properties
            {
                { "id", new KeywordProperty() },
                { "name", new TextProperty{Fields = new Properties{{ "keyword", new KeywordProperty { IgnoreAbove = 256 } }}}},
                { "description", new TextProperty() },
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
        })
    );

    if (!resp.IsValidResponse)
        throw new InvalidOperationException(resp.ElasticsearchServerError?.Error?.Reason);
}