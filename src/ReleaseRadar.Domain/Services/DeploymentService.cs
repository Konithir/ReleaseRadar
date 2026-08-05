using ReleaseRadar.Domain.Models;

namespace ReleaseRadar.Domain.Services;

public sealed class DeploymentService(
    IDeploymentRepository repository,
    IArgoCdClient argoCd,
    IKubernetesClient kubernetes,
    IPrometheusClient prometheus,
    ISourceControlClient sourceControl,
    IDeploymentEventPublisher eventPublisher,
    TimeProvider timeProvider)
{
    public async Task<DeploymentOperationResult?> SyncAsync(string name, string? requestedRevision, string actor, CancellationToken cancellationToken)
    {
        var application = await repository.GetByNameAsync(name, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var revision = await sourceControl.ResolveRevisionAsync(application, requestedRevision, cancellationToken);
        application.BeginSync(revision);
        await repository.AddAuditAsync(AuditRecord.Create(application.Name, "Sync", "Started", revision, actor, $"Sync requested for {revision}."), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await eventPublisher.PublishAsync(application, "sync-started", cancellationToken);

        var result = await argoCd.SyncAsync(application, revision, cancellationToken);
        if (result.Accepted)
        {
            application.CompleteSync(result.Revision, timeProvider.GetUtcNow());
        }
        else
        {
            application.FailSync();
        }

        await repository.AddAuditAsync(AuditRecord.Create(
            application.Name,
            "Sync",
            result.Accepted ? "Succeeded" : "Failed",
            result.Revision,
            actor,
            result.Message), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await eventPublisher.PublishAsync(application, result.Accepted ? "sync-completed" : "sync-failed", cancellationToken);

        return new DeploymentOperationResult(result.Accepted, "Sync", result.Message, application);
    }

    public async Task<DeploymentOperationResult?> RollbackAsync(string name, string actor, CancellationToken cancellationToken)
    {
        var application = await repository.GetByNameAsync(name, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var target = application.PreviousRevision;
        application.BeginSync(target);
        await repository.AddAuditAsync(AuditRecord.Create(application.Name, "Rollback", "Started", target, actor, $"Rollback requested to {target}."), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await eventPublisher.PublishAsync(application, "rollback-started", cancellationToken);

        var result = await argoCd.RollbackAsync(application, target, cancellationToken);
        if (result.Accepted)
        {
            application.CompleteRollback(timeProvider.GetUtcNow());
        }
        else
        {
            application.FailSync();
        }

        await repository.AddAuditAsync(AuditRecord.Create(
            application.Name,
            "Rollback",
            result.Accepted ? "Succeeded" : "Failed",
            target,
            actor,
            result.Message), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await eventPublisher.PublishAsync(application, result.Accepted ? "rollback-completed" : "rollback-failed", cancellationToken);

        return new DeploymentOperationResult(result.Accepted, "Rollback", result.Message, application);
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var applications = await repository.ListAsync(cancellationToken);
        foreach (var application in applications)
        {
            try
            {
                var argoTask = argoCd.GetStateAsync(application, cancellationToken);
                var kubernetesTask = kubernetes.GetWorkloadAsync(application, cancellationToken);
                var metricsTask = prometheus.GetMetricsAsync(application, cancellationToken);
                await Task.WhenAll(argoTask, kubernetesTask, metricsTask);

                var argo = await argoTask;
                var workload = await kubernetesTask;
                var metrics = await metricsTask;
                application.UpdateRuntime(
                    argo.Health,
                    argo.SyncStatus,
                    workload.ReadyReplicas,
                    workload.DesiredReplicas,
                    metrics.CpuPercent,
                    metrics.MemoryPercent,
                    argo.LiveRevision);
            }
            catch (Exception)
            {
                application.UpdateRuntime(DeploymentHealth.Unknown, DeploymentSyncStatus.Unknown, 0, application.DesiredReplicas, application.CpuPercent, application.MemoryPercent, application.LiveRevision);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}
