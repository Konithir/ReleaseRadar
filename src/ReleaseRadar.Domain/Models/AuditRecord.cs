namespace ReleaseRadar.Domain.Models;

public sealed class AuditRecord
{
    private AuditRecord() { }

    private AuditRecord(string application, string action, string status, string revision, string actor, string message, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        Application = application;
        Action = action;
        Status = status;
        Revision = revision;
        Actor = actor;
        Message = message;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public string Application { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string Revision { get; private set; } = string.Empty;
    public string Actor { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditRecord Create(string application, string action, string status, string revision, string actor, string message, DateTimeOffset? occurredAt = null) =>
        new(application, action, status, revision, actor, message, occurredAt ?? DateTimeOffset.UtcNow);
}
