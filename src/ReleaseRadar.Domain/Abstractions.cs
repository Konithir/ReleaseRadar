using ReleaseRadar.Domain.Models;

namespace ReleaseRadar.Domain;

public interface IDeploymentRepository
{
    Task<IReadOnlyList<DeploymentApplication>> ListAsync(CancellationToken cancellationToken);
    Task<DeploymentApplication?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditRecord>> ListAuditAsync(int take, string? application, CancellationToken cancellationToken);
    Task AddAuditAsync(AuditRecord record, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IArgoCdClient
{
    string Mode { get; }
    string Endpoint { get; }
    Task<ArgoApplicationState> GetStateAsync(DeploymentApplication application, CancellationToken cancellationToken);
    Task<DeploymentCommandResult> SyncAsync(DeploymentApplication application, string revision, CancellationToken cancellationToken);
    Task<DeploymentCommandResult> RollbackAsync(DeploymentApplication application, string revision, CancellationToken cancellationToken);
}

public interface IKubernetesClient
{
    string Mode { get; }
    string Endpoint { get; }
    Task<KubernetesWorkloadState> GetWorkloadAsync(DeploymentApplication application, CancellationToken cancellationToken);
}

public interface IPrometheusClient
{
    string Mode { get; }
    string Endpoint { get; }
    Task<WorkloadMetrics> GetMetricsAsync(DeploymentApplication application, CancellationToken cancellationToken);
}

public interface ISourceControlClient
{
    string Mode { get; }
    string Endpoint { get; }
    Task<string> ResolveRevisionAsync(DeploymentApplication application, string? requestedRevision, CancellationToken cancellationToken);
}

public interface IDeploymentEventPublisher
{
    Task PublishAsync(DeploymentApplication application, string eventName, CancellationToken cancellationToken);
}

public sealed record ArgoApplicationState(DeploymentHealth Health, DeploymentSyncStatus SyncStatus, string LiveRevision);
public sealed record KubernetesWorkloadState(int ReadyReplicas, int DesiredReplicas);
public sealed record WorkloadMetrics(double CpuPercent, double MemoryPercent);
public sealed record DeploymentCommandResult(bool Accepted, string Revision, string Message);
public sealed record DeploymentOperationResult(bool Accepted, string Action, string Message, DeploymentApplication Application);
