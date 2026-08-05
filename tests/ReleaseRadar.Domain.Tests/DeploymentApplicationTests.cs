using ReleaseRadar.Domain.Models;

namespace ReleaseRadar.Domain.Tests;

public sealed class DeploymentApplicationTests
{
    [Fact]
    public void Completing_sync_keeps_previous_revision_for_rollback()
    {
        var application = CreateApplication("abc1234");

        application.BeginSync("def5678");
        application.CompleteSync("def5678", DateTimeOffset.Parse("2026-08-05T10:00:00Z"));

        Assert.Equal("def5678", application.LiveRevision);
        Assert.Equal("abc1234", application.PreviousRevision);
        Assert.Equal(DeploymentHealth.Healthy, application.Health);
        Assert.Equal(DeploymentSyncStatus.Synced, application.SyncStatus);
    }

    [Fact]
    public void Rollback_swaps_live_and_previous_revisions()
    {
        var application = CreateApplication("abc1234");
        application.BeginSync("def5678");
        application.CompleteSync("def5678", DateTimeOffset.UtcNow);

        application.BeginSync(application.PreviousRevision);
        application.CompleteRollback(DateTimeOffset.UtcNow.AddMinutes(5));

        Assert.Equal("abc1234", application.LiveRevision);
        Assert.Equal("def5678", application.PreviousRevision);
        Assert.Equal("abc1234", application.DesiredRevision);
    }

    [Fact]
    public void Empty_revision_cannot_start_a_sync()
    {
        var application = CreateApplication("abc1234");

        Assert.Throws<ArgumentException>(() => application.BeginSync("  "));
    }

    [Fact]
    public void Runtime_metrics_are_clamped_to_valid_percentages()
    {
        var application = CreateApplication("abc1234");

        application.UpdateRuntime(DeploymentHealth.Healthy, DeploymentSyncStatus.Synced, 4, 3, 140, -8, "abc1234");

        Assert.Equal(100, application.CpuPercent);
        Assert.Equal(0, application.MemoryPercent);
        Assert.Equal(4, application.ReadyReplicas);
    }

    private static DeploymentApplication CreateApplication(string revision) => DeploymentApplication.Create(
        "orders-api", "Commerce", "Orders", "production", "aks-prod", "commerce",
        "northwind/orders-api", "ghcr.io/northwind/orders-api", revision, 3, DateTimeOffset.UtcNow);
}
