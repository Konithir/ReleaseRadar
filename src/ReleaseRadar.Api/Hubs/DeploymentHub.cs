using Microsoft.AspNetCore.SignalR;
using ReleaseRadar.Domain;
using ReleaseRadar.Domain.Models;
using ReleaseRadar.Infrastructure;

namespace ReleaseRadar.Api.Hubs;

public sealed class DeploymentHub : Hub;

public sealed class SignalRDeploymentEventPublisher(IHubContext<DeploymentHub> hubContext) : IDeploymentEventPublisher
{
    public Task PublishAsync(DeploymentApplication application, string eventName, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("deploymentChanged", new
        {
            eventName,
            application = application.ToListItem(),
            occurredAt = DateTimeOffset.UtcNow
        }, cancellationToken);
}
