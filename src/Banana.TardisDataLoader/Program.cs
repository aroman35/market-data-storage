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
using Serilog;

var builder = WebApplication.CreateBuilder(args);

#region Options

builder.Services.ConfigureOptions<TardisHttpClientOptions>(builder.Configuration);
builder.Services.ConfigureOptions<RepositoryOptions>(builder.Configuration);

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
