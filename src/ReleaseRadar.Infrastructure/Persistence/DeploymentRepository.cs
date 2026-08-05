using Microsoft.EntityFrameworkCore;
using ReleaseRadar.Domain;
using ReleaseRadar.Domain.Models;

namespace ReleaseRadar.Infrastructure.Persistence;

public sealed class DeploymentRepository(ReleaseRadarDbContext dbContext) : IDeploymentRepository
{
    public async Task<IReadOnlyList<DeploymentApplication>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Applications.OrderBy(x => x.Environment).ThenBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<DeploymentApplication?> GetByNameAsync(string name, CancellationToken cancellationToken) =>
        dbContext.Applications.SingleOrDefaultAsync(x => x.Name == name, cancellationToken);

    public async Task<IReadOnlyList<AuditRecord>> ListAuditAsync(int take, string? application, CancellationToken cancellationToken)
    {
        var query = dbContext.AuditRecords.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(application))
        {
            query = query.Where(x => x.Application == application);
        }

        return await query.OrderByDescending(x => x.OccurredAt).Take(Math.Clamp(take, 1, 100)).ToListAsync(cancellationToken);
    }

    public Task AddAuditAsync(AuditRecord record, CancellationToken cancellationToken) =>
        dbContext.AuditRecords.AddAsync(record, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
