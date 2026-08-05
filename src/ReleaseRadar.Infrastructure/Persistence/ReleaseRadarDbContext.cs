using Microsoft.EntityFrameworkCore;
using ReleaseRadar.Domain.Models;

namespace ReleaseRadar.Infrastructure.Persistence;

public sealed class ReleaseRadarDbContext(DbContextOptions<ReleaseRadarDbContext> options) : DbContext(options)
{
    public DbSet<DeploymentApplication> Applications => Set<DeploymentApplication>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeploymentApplication>(entity =>
        {
            entity.ToTable("DeploymentApplications");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Team).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Environment).HasMaxLength(40);
            entity.Property(x => x.Cluster).HasMaxLength(120);
            entity.Property(x => x.Namespace).HasMaxLength(120);
            entity.Property(x => x.Repository).HasMaxLength(240);
            entity.Property(x => x.Image).HasMaxLength(300);
            entity.Property(x => x.DesiredRevision).HasMaxLength(80);
            entity.Property(x => x.LiveRevision).HasMaxLength(80);
            entity.Property(x => x.PreviousRevision).HasMaxLength(80);
            entity.Property(x => x.Health).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.SyncStatus).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<AuditRecord>(entity =>
        {
            entity.ToTable("DeploymentAudit");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Application, x.OccurredAt });
            entity.Property(x => x.Application).HasMaxLength(120);
            entity.Property(x => x.Action).HasMaxLength(50);
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.Property(x => x.Revision).HasMaxLength(80);
            entity.Property(x => x.Actor).HasMaxLength(120);
            entity.Property(x => x.Message).HasMaxLength(600);
        });
    }
}
