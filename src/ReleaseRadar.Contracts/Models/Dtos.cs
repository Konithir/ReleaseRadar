namespace ReleaseRadar.Contracts;

public sealed record OverviewDto(
    int Applications,
    int Healthy,
    int Progressing,
    int Degraded,
    int OutOfSync,
    int ProductionApplications,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ClusterDto> Clusters,
    IReadOnlyList<AuditEventDto> RecentActivity);

public sealed record ApplicationListItemDto(
    Guid Id,
    string Name,
    string Team,
    string Environment,
    string Cluster,
    string Namespace,
    string DesiredRevision,
    string LiveRevision,
    string Health,
    string SyncStatus,
    int ReadyReplicas,
    int DesiredReplicas,
    DateTimeOffset LastDeploymentAt,
    double CpuPercent,
    double MemoryPercent);

public sealed record ApplicationDetailDto(
    Guid Id,
    string Name,
    string Team,
    string Description,
    string Environment,
    string Cluster,
    string Namespace,
    string Repository,
    string Image,
    string DesiredRevision,
    string LiveRevision,
    string PreviousRevision,
    string Health,
    string SyncStatus,
    int ReadyReplicas,
    int DesiredReplicas,
    DateTimeOffset LastDeploymentAt,
    double CpuPercent,
    double MemoryPercent,
    IReadOnlyList<AuditEventDto> Timeline);

public sealed record ClusterDto(
    string Name,
    string Region,
    string Provider,
    string Version,
    string Health,
    int Nodes,
    int Workloads,
    double CpuPercent,
    double MemoryPercent);

public sealed record AuditEventDto(
    Guid Id,
    string Application,
    string Action,
    string Status,
    string Revision,
    string Actor,
    string Message,
    DateTimeOffset OccurredAt);

public sealed record DeploymentActionRequest(string? Revision, string? RequestedBy);

public sealed record DeploymentActionResultDto(
    bool Accepted,
    string Application,
    string Action,
    string Message,
    ApplicationListItemDto? State);

public sealed record IntegrationStatusDto(
    string Name,
    string Mode,
    string Status,
    string Endpoint);
