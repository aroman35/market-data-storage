using System.IO.Compression;
using System.Text.Json.Serialization;
using Banana.TardisDataLoader.Clients;
using Banana.TardisDataLoader.Extensions;
using Banana.TardisDataLoader.Options;
using Banana.TardisDataLoader.Persistence;
using Banana.TardisDataLoader.Persistence.Abstractions;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NJsonSchema;
using NJsonSchema.Generation.TypeMappers;
using Serilog;

Decompress("1000bonkusdc_2025-09-01_l2.csv.gz", "1000bonkusdc_2025-09-01_l2.csv");
Decompress("1000bonkusdc_2025-09-01_trades.csv.gz", "1000bonkusdc_2025-09-01_trades.csv");

var builder = WebApplication.CreateBuilder(args);

#region Options

builder.Services.ConfigureOptions<TardisHttpClientOptions>(builder.Configuration);
builder.Services.ConfigureOptions<RepositoryOptions>(builder.Configuration);
builder.Services.ConfigureOptions<LocalCacheOptions>(builder.Configuration);

#endregion

builder.Services
    .AddHttpClient<TardisClient>()
    .ConfigureHttpClient(builder.Configuration.GetOptions<TardisHttpClientOptions>());

builder.Services
    .AddFastEndpoints(fastEndpoints =>
    {
        fastEndpoints.IncludeAbstractValidators = true;
    })
    .SwaggerDocument(swagger =>
    {
        swagger.SerializerSettings = serializer =>
        {
            serializer.Converters.Add(new JsonStringEnumConverter());
        };
        swagger.DocumentSettings = s =>
        {
            s.SchemaSettings.TypeMappers.Add(
                new PrimitiveTypeMapper(
                    typeof(DateOnly),
                    schema =>
                    {
                        schema.Type = JsonObjectType.String;
                        schema.Format = JsonFormatStrings.Date;
                        schema.Example = "2025-09-01";
                    }));
            s.SchemaSettings.TypeMappers.Add(
                new PrimitiveTypeMapper(
                    typeof(DateOnly?),
                    schema =>
                    {
                        schema.Type = JsonObjectType.String;
                        schema.Format = JsonFormatStrings.Date;
                        schema.IsNullableRaw = true;
                        schema.Example = "2025-09-01";
                    }));

            // (optional) TimeOnly -> string(time)
            s.SchemaSettings.TypeMappers.Add(
                new PrimitiveTypeMapper(
                    typeof(TimeOnly),
                    schema =>
                    {
                        schema.Type = JsonObjectType.String;
                        schema.Format = JsonFormatStrings.Time;
                        schema.Example = "13:37:00";
                    }));

            s.SchemaSettings.TypeMappers.Add(
                new PrimitiveTypeMapper(
                    typeof(TimeOnly?),
                    schema =>
                    {
                        schema.Type = JsonObjectType.String;
                        schema.Format = JsonFormatStrings.Time;
                        schema.IsNullableRaw = true;
                        schema.Example = "13:37:00";
                    }));
        };
        swagger.RemoveEmptyRequestSchema = false;
    });
builder.Services.AddHealthChecks()
    .AddCheck("Liveness", _ => HealthCheckResult.Healthy(), ["live"])
    .AddCheck("Readiness", _ => HealthCheckResult.Healthy());
builder.Services.AddSingleton<IPgConnectionPool, PgConnectionPool>();
builder.Configuration
    .GetOptions<RepositoryOptions>()
    .ConfigureRepository(builder.Services);
builder.Services.AddMediatR(mediator =>
{
    mediator.RegisterServicesFromAssembly(typeof(TardisClient).Assembly);
    mediator.Lifetime = ServiceLifetime.Scoped;
});

builder.Host.UseSerilog((hostContext, loggerBuilder) => loggerBuilder.ReadFrom.Configuration(hostContext.Configuration));

await using var app = builder.Build();

app.UseFastEndpoints(config =>
    {
        config.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
    })
    .UseSwaggerGen()
    .UseSwaggerUi();

app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready")
});
app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = _ => false,
    AllowCachingResponses = true,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

await app.RunAsync();

static void Decompress(string compressedFileName, string decompressedFileName)
{
    //1000bonkusdc_2025-09-01_l2.csv.gz
    var sourcesDir = "E:\\tmp\\md-cache\\binance-futures\\perpetual\\1000BONKUSDC\\";
    var compressedFilePath = Path.Combine(sourcesDir, compressedFileName);
    var outputFilePath = Path.Combine(sourcesDir, decompressedFileName);
    using (var outputFileStream = new FileStream(outputFilePath, FileMode.CreateNew, FileAccess.Write))
    {
        using (var compressedStream = new FileStream(compressedFilePath, FileMode.Open, FileAccess.Read))
        {
            using (var decompressionStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                decompressionStream.CopyTo(outputFileStream);
            }
        }
    }
}
