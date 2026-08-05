using ReleaseRadar.Api.Observability;
using ReleaseRadar.Contracts;
using ReleaseRadar.Domain;
using ReleaseRadar.Domain.Models;
using ReleaseRadar.Domain.Services;
using ReleaseRadar.Infrastructure;

namespace ReleaseRadar.Api.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapReleaseRadarEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");

        api.MapGet("/overview", GetOverviewAsync);
        api.MapGet("/applications", GetApplicationsAsync);
        api.MapGet("/applications/{name}", GetApplicationAsync);
        api.MapPost("/applications/{name}/sync", SyncAsync);
        api.MapPost("/applications/{name}/rollback", RollbackAsync);
        api.MapGet("/clusters", GetClustersAsync);
        api.MapGet("/audit", GetAuditAsync);
        api.MapGet("/integrations/status", GetIntegrations);

        return endpoints;
    }

    private static async Task<IResult> GetOverviewAsync(IDeploymentRepository repository, CancellationToken cancellationToken)
    {
        var applications = await repository.ListAsync(cancellationToken);
        var activity = await repository.ListAuditAsync(6, null, cancellationToken);
        var overview = new OverviewDto(
            applications.Count,
            applications.Count(x => x.Health == DeploymentHealth.Healthy),
            applications.Count(x => x.Health == DeploymentHealth.Progressing),
            applications.Count(x => x.Health == DeploymentHealth.Degraded),
            applications.Count(x => x.SyncStatus == DeploymentSyncStatus.OutOfSync),
            applications.Count(x => x.Environment == "production"),
            DateTimeOffset.UtcNow,
            BuildClusters(applications),
            activity.Select(x => x.ToDto()).ToArray());
        return Results.Ok(overview);
    }

    private static async Task<IResult> GetApplicationsAsync(
        IDeploymentRepository repository,
        string? environment,
        string? health,
        string? search,
        CancellationToken cancellationToken)
    {
        var applications = await repository.ListAsync(cancellationToken);
        var filtered = applications.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(environment) && !environment.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(x => x.Environment.Equals(environment, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(health) && !health.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(x => x.Health.ToString().Equals(health, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Team.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return Results.Ok(filtered.Select(x => x.ToListItem()).ToArray());
    }

    private static async Task<IResult> GetApplicationAsync(string name, IDeploymentRepository repository, CancellationToken cancellationToken)
    {
        var application = await repository.GetByNameAsync(name, cancellationToken);
        if (application is null)
        {
            return Results.NotFound();
        }

        var timeline = await repository.ListAuditAsync(30, name, cancellationToken);
        return Results.Ok(application.ToDetail(timeline.Select(x => x.ToDto()).ToArray()));
    }

    private static async Task<IResult> SyncAsync(
        string name,
        DeploymentActionRequest request,
        DeploymentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SyncAsync(name, request.Revision, NormalizeActor(request.RequestedBy), cancellationToken);
        if (result is null)
        {
            return Results.NotFound();
        }

        DeploymentMetrics.Actions.WithLabels(name, "sync", result.Accepted ? "accepted" : "rejected").Inc();
        return Results.Ok(new DeploymentActionResultDto(result.Accepted, name, result.Action, result.Message, result.Application.ToListItem()));
    }

    private static async Task<IResult> RollbackAsync(
        string name,
        DeploymentActionRequest request,
        DeploymentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RollbackAsync(name, NormalizeActor(request.RequestedBy), cancellationToken);
        if (result is null)
        {
            return Results.NotFound();
        }

        DeploymentMetrics.Actions.WithLabels(name, "rollback", result.Accepted ? "accepted" : "rejected").Inc();
        return Results.Ok(new DeploymentActionResultDto(result.Accepted, name, result.Action, result.Message, result.Application.ToListItem()));
    }

    private static async Task<IResult> GetClustersAsync(IDeploymentRepository repository, CancellationToken cancellationToken)
    {
        var applications = await repository.ListAsync(cancellationToken);
        return Results.Ok(BuildClusters(applications));
    }

    private static async Task<IResult> GetAuditAsync(IDeploymentRepository repository, int take = 30, string? application = null, CancellationToken cancellationToken = default) =>
        Results.Ok((await repository.ListAuditAsync(take, application, cancellationToken)).Select(x => x.ToDto()).ToArray());

    private static IResult GetIntegrations(IArgoCdClient argoCd, IKubernetesClient kubernetes, IPrometheusClient prometheus, ISourceControlClient sourceControl) =>
        Results.Ok(new[]
        {
            new IntegrationStatusDto("Argo CD", argoCd.Mode, "Connected", argoCd.Endpoint),
            new IntegrationStatusDto("Kubernetes", kubernetes.Mode, "Connected", kubernetes.Endpoint),
            new IntegrationStatusDto("Prometheus", prometheus.Mode, "Connected", prometheus.Endpoint),
            new IntegrationStatusDto("GitHub", sourceControl.Mode, "Connected", sourceControl.Endpoint)
        });

    private static IReadOnlyList<ClusterDto> BuildClusters(IReadOnlyList<DeploymentApplication> applications)
    {
        var metadata = new Dictionary<string, (string Region, string Version, int Nodes)>(StringComparer.OrdinalIgnoreCase)
        {
            ["aks-prod-weu"] = ("West Europe", "1.32.6", 6),
            ["aks-stage-weu"] = ("West Europe", "1.32.6", 3),
            ["aks-dev-neu"] = ("North Europe", "1.31.10", 2)
        };

        return applications
            .GroupBy(x => x.Cluster)
            .Select(group =>
            {
                var info = metadata.GetValueOrDefault(group.Key, (Region: "Unknown", Version: "unknown", Nodes: 1));
                var healthy = group.All(x => x.Health is DeploymentHealth.Healthy or DeploymentHealth.Progressing);
                return new ClusterDto(
                    group.Key,
                    info.Region,
                    "Azure AKS",
                    info.Version,
                    healthy ? "Healthy" : "Attention",
                    info.Nodes,
                    group.Count(),
                    Math.Round(group.Average(x => x.CpuPercent), 1),
                    Math.Round(group.Average(x => x.MemoryPercent), 1));
            })
            .OrderByDescending(x => x.Workloads)
            .ToArray();
    }

    private static string NormalizeActor(string? actor) => string.IsNullOrWhiteSpace(actor) ? "release-radar-user" : actor.Trim();
}

