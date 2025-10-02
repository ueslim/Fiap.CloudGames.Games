using FIAP.CloudGames.Catalog.API.Configuration;
using FIAP.CloudGames.Catalog.API.Data;
using FIAP.CloudGames.Catalog.API.Services;
using FIAP.CloudGames.WebAPI.Core.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

LoggingConfig.ConfigureBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Serilog + OTLP para logs
builder.ConfigureSerilogWithOpenTelemetry("cart-api");

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Services.AddApiConfiguration(builder.Configuration);
builder.Services.AddJwtConfiguration(builder.Configuration);

builder.Services.AddSwaggerConfiguration();

builder.Services.RegisterServices();

// OpenTelemetry Tracing + Metrics
builder.Services.AddObservabilityConfiguration(builder.Configuration);

#region Elastic 
builder.Services.Configure<ElasticSettings>(builder.Configuration.GetSection("ElasticSettings"));
builder.Services.AddSingleton<IElasticSettings>(sp => sp.GetRequiredService<IOptions<ElasticSettings>>().Value);
builder.Services.AddSingleton(typeof(IElasticClient<>), typeof(ElasticClient<>));
builder.Services.AddScoped(typeof(IElasticSearchService<>), typeof(ElasticSearchService<>));

//Cloud
//builder.Services.Configure<ElasticSettings>(builder.Configuration.GetSection(nameof(ElasticSettings)));
#endregion

var app = builder.Build();

//SEED

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CatalogContext>();
    await context.Database.MigrateAsync();
    await CatalogContextSeed.EnsureSeedProducts(context);
}

app.UseSwaggerConfiguration();

app.UseApiConfiguration(app.Environment);

// Logs enriquecidos com user_id
app.UseRequestLogEnrichment();

app.Run();