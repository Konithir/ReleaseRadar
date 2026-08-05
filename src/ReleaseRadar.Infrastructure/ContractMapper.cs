using ReleaseRadar.Contracts;
using ReleaseRadar.Domain.Models;

namespace ReleaseRadar.Infrastructure;

public static class ContractMapper
{
    public static ApplicationListItemDto ToListItem(this DeploymentApplication application) => new(
        application.Id,
        application.Name,
        application.Team,
        application.Environment,
        application.Cluster,
        application.Namespace,
        application.DesiredRevision,
        application.LiveRevision,
        application.Health.ToString(),
        application.SyncStatus.ToString(),
        application.ReadyReplicas,
        application.DesiredReplicas,
        application.LastDeploymentAt,
        application.CpuPercent,
        application.MemoryPercent);

    public static ApplicationDetailDto ToDetail(this DeploymentApplication application, IReadOnlyList<AuditEventDto> timeline) => new(
        application.Id,
        application.Name,
        application.Team,
        application.Description,
        application.Environment,
        application.Cluster,
        application.Namespace,
        application.Repository,
        application.Image,
        application.DesiredRevision,
        application.LiveRevision,
        application.PreviousRevision,
        application.Health.ToString(),
        application.SyncStatus.ToString(),
        application.ReadyReplicas,
        application.DesiredReplicas,
        application.LastDeploymentAt,
        application.CpuPercent,
        application.MemoryPercent,
        timeline);

    public static AuditEventDto ToDto(this AuditRecord record) => new(
        record.Id,
        record.Application,
        record.Action,
        record.Status,
        record.Revision,
        record.Actor,
        record.Message,
        record.OccurredAt);
}
