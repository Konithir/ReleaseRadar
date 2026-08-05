using Microsoft.EntityFrameworkCore;
using ReleaseRadar.Domain.Models;

namespace ReleaseRadar.Infrastructure.Persistence;

public static class DatabaseBootstrapper
{
    public static async Task InitializeAsync(ReleaseRadarDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        if (await dbContext.Applications.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var applications = new[]
        {
            DeploymentApplication.Create("checkout-api", "Commerce", "Checkout orchestration and payment hand-off.", "production", "aks-prod-weu", "commerce", "northwind/checkout-api", "ghcr.io/northwind/checkout-api", "9f61c2a", 6, now.AddHours(-3)),
            DeploymentApplication.Create("catalog-worker", "Catalog", "Product indexing and inventory projection worker.", "production", "aks-prod-weu", "catalog", "northwind/catalog-worker", "ghcr.io/northwind/catalog-worker", "4d12ea8", 4, now.AddDays(-1).AddHours(-2)),
            DeploymentApplication.Create("identity-api", "Identity", "Token issuing, session validation and account access.", "staging", "aks-stage-weu", "identity", "northwind/identity-api", "ghcr.io/northwind/identity-api", "a813d90", 3, now.AddHours(-7)),
            DeploymentApplication.Create("pricing-api", "Commerce", "Price calculation and promotion eligibility service.", "development", "aks-dev-neu", "commerce-dev", "northwind/pricing-api", "ghcr.io/northwind/pricing-api", "c72b118", 2, now.AddMinutes(-42)),
            DeploymentApplication.Create("web-bff", "Experience", "Backend for the customer web application.", "production", "aks-prod-weu", "experience", "northwind/web-bff", "ghcr.io/northwind/web-bff", "7b44f0e", 5, now.AddHours(-18))
        };

        applications[0].UpdateRuntime(DeploymentHealth.Healthy, DeploymentSyncStatus.Synced, 6, 6, 41.8, 58.4, "9f61c2a");
        applications[1].UpdateRuntime(DeploymentHealth.Healthy, DeploymentSyncStatus.Synced, 4, 4, 27.2, 44.1, "4d12ea8");
        applications[2].UpdateRuntime(DeploymentHealth.Degraded, DeploymentSyncStatus.OutOfSync, 2, 3, 73.5, 81.3, "98bd210");
        applications[3].UpdateRuntime(DeploymentHealth.Progressing, DeploymentSyncStatus.Progressing, 1, 2, 62.6, 69.8, "b19af41");
        applications[4].UpdateRuntime(DeploymentHealth.Healthy, DeploymentSyncStatus.Synced, 5, 5, 34.7, 52.9, "7b44f0e");

        dbContext.Applications.AddRange(applications);
        dbContext.AuditRecords.AddRange(
            AuditRecord.Create("pricing-api", "Sync", "Started", "c72b118", "github-actions", "Argo CD started applying the new revision.", now.AddMinutes(-42)),
            AuditRecord.Create("identity-api", "Drift detected", "Warning", "a813d90", "reconciler", "Live manifests differ from the desired revision.", now.AddHours(-1).AddMinutes(-16)),
            AuditRecord.Create("checkout-api", "Sync", "Succeeded", "9f61c2a", "marta.nowak", "Rollout completed: 6 of 6 replicas are ready.", now.AddHours(-3)),
            AuditRecord.Create("catalog-worker", "Sync", "Succeeded", "4d12ea8", "github-actions", "Image and Helm values applied successfully.", now.AddDays(-1).AddHours(-2)),
            AuditRecord.Create("web-bff", "Rollback", "Succeeded", "7b44f0e", "piotr.kowalski", "Previous stable revision restored.", now.AddHours(-18)));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
