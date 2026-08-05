using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using ReleaseRadar.Api.Endpoints;
using ReleaseRadar.Api.Hubs;
using ReleaseRadar.Api.Workers;
using ReleaseRadar.Domain;
using ReleaseRadar.Domain.Services;
using ReleaseRadar.Infrastructure.Configuration;
using ReleaseRadar.Infrastructure.Integrations;
using ReleaseRadar.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter()));

    builder.Services.Configure<IntegrationOptions>(builder.Configuration.GetSection(IntegrationOptions.SectionName));

    var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "InMemory";
    builder.Services.AddDbContext<ReleaseRadarDbContext>(options =>
    {
        if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("ReleaseRadar"), sql => sql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(5), null));
        }
        else
        {
            options.UseInMemoryDatabase(builder.Configuration["InMemoryDatabaseName"] ?? "release-radar-demo");
        }
    });

    builder.Services.AddScoped<IDeploymentRepository, DeploymentRepository>();
    builder.Services.AddScoped<DeploymentService>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<IDeploymentEventPublisher, SignalRDeploymentEventPublisher>();

    builder.Services.AddHttpClient<ArgoCdClient>();
    builder.Services.AddHttpClient<KubernetesClient>();
    builder.Services.AddHttpClient<PrometheusClient>();
    builder.Services.AddHttpClient<GitHubSourceControlClient>();
    builder.Services.AddScoped<IArgoCdClient>(services => services.GetRequiredService<ArgoCdClient>());
    builder.Services.AddScoped<IKubernetesClient>(services => services.GetRequiredService<KubernetesClient>());
    builder.Services.AddScoped<IPrometheusClient>(services => services.GetRequiredService<PrometheusClient>());
    builder.Services.AddScoped<ISourceControlClient>(services => services.GetRequiredService<GitHubSourceControlClient>());

    builder.Services.AddSignalR();
    builder.Services.AddHealthChecks();
    builder.Services.AddHostedService<ReconciliationWorker>();
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins("http://localhost:4200", "http://localhost:4300", "http://localhost:8080")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    var telemetry = builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("release-radar-api", serviceVersion: "1.0.0"));

    var tracing = telemetry.WithTracing(traces => traces
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());
    var metrics = telemetry.WithMetrics(metricBuilder => metricBuilder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());

    var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
    if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
    {
        tracing.WithTracing(builder => builder.AddOtlpExporter(options => options.Endpoint = endpoint));
        metrics.WithMetrics(builder => builder.AddOtlpExporter(options => options.Endpoint = endpoint));
    }

    var app = builder.Build();
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseHttpMetrics();

    app.MapGet("/", () => Results.Ok(new { service = "ReleaseRadar.Api", status = "ready", version = "1.0.0" }));
    app.MapHealthChecks("/health");
    app.MapMetrics("/metrics");
    app.MapHub<DeploymentHub>("/hubs/deployments");
    app.MapReleaseRadarEndpoints();

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ReleaseRadarDbContext>();
        await DatabaseBootstrapper.InitializeAsync(dbContext);
    }

    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "ReleaseRadar API terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;


