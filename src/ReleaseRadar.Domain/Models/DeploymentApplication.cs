namespace ReleaseRadar.Domain.Models;

public sealed class DeploymentApplication
{
    private DeploymentApplication() { }

    private DeploymentApplication(
        string name,
        string team,
        string description,
        string environment,
        string cluster,
        string @namespace,
        string repository,
        string image,
        string revision,
        int replicas,
        DateTimeOffset deployedAt)
    {
        Id = Guid.NewGuid();
        Name = name;
        Team = team;
        Description = description;
        Environment = environment;
        Cluster = cluster;
        Namespace = @namespace;
        Repository = repository;
        Image = image;
        DesiredRevision = revision;
        LiveRevision = revision;
        PreviousRevision = revision;
        DesiredReplicas = replicas;
        ReadyReplicas = replicas;
        LastDeploymentAt = deployedAt;
        Health = DeploymentHealth.Healthy;
        SyncStatus = DeploymentSyncStatus.Synced;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Team { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Environment { get; private set; } = string.Empty;
    public string Cluster { get; private set; } = string.Empty;
    public string Namespace { get; private set; } = string.Empty;
    public string Repository { get; private set; } = string.Empty;
    public string Image { get; private set; } = string.Empty;
    public string DesiredRevision { get; private set; } = string.Empty;
    public string LiveRevision { get; private set; } = string.Empty;
    public string PreviousRevision { get; private set; } = string.Empty;
    public DeploymentHealth Health { get; private set; }
    public DeploymentSyncStatus SyncStatus { get; private set; }
    public int ReadyReplicas { get; private set; }
    public int DesiredReplicas { get; private set; }
    public DateTimeOffset LastDeploymentAt { get; private set; }
    public double CpuPercent { get; private set; }
    public double MemoryPercent { get; private set; }

    public static DeploymentApplication Create(
        string name,
        string team,
        string description,
        string environment,
        string cluster,
        string @namespace,
        string repository,
        string image,
        string revision,
        int replicas,
        DateTimeOffset deployedAt) =>
        new(name, team, description, environment, cluster, @namespace, repository, image, revision, replicas, deployedAt);

    public void BeginSync(string revision)
    {
        if (string.IsNullOrWhiteSpace(revision))
        {
            throw new ArgumentException("Revision cannot be empty.", nameof(revision));
        }

        DesiredRevision = revision;
        SyncStatus = DeploymentSyncStatus.Progressing;
        Health = DeploymentHealth.Progressing;
    }

    public void CompleteSync(string revision, DateTimeOffset completedAt)
    {
        PreviousRevision = LiveRevision;
        DesiredRevision = revision;
        LiveRevision = revision;
        SyncStatus = DeploymentSyncStatus.Synced;
        Health = DeploymentHealth.Healthy;
        ReadyReplicas = DesiredReplicas;
        LastDeploymentAt = completedAt;
    }

    public void FailSync()
    {
        SyncStatus = DeploymentSyncStatus.OutOfSync;
        Health = DeploymentHealth.Degraded;
    }

    public void CompleteRollback(DateTimeOffset completedAt)
    {
        var rolledBackFrom = LiveRevision;
        LiveRevision = PreviousRevision;
        DesiredRevision = PreviousRevision;
        PreviousRevision = rolledBackFrom;
        SyncStatus = DeploymentSyncStatus.Synced;
        Health = DeploymentHealth.Healthy;
        ReadyReplicas = DesiredReplicas;
        LastDeploymentAt = completedAt;
    }

    public void UpdateRuntime(
        DeploymentHealth health,
        DeploymentSyncStatus syncStatus,
        int readyReplicas,
        int desiredReplicas,
        double cpuPercent,
        double memoryPercent,
        string liveRevision)
    {
        Health = health;
        SyncStatus = syncStatus;
        ReadyReplicas = Math.Max(0, readyReplicas);
        DesiredReplicas = Math.Max(1, desiredReplicas);
        CpuPercent = Math.Clamp(cpuPercent, 0, 100);
        MemoryPercent = Math.Clamp(memoryPercent, 0, 100);
        if (!string.IsNullOrWhiteSpace(liveRevision))
        {
            LiveRevision = liveRevision;
        }
    }
}

public enum DeploymentHealth
{
    Healthy,
    Progressing,
    Degraded,
    Suspended,
    Unknown
}

public enum DeploymentSyncStatus
{
    Synced,
    Progressing,
    OutOfSync,
    Unknown
}
