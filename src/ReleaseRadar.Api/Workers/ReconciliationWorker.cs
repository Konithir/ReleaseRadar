using ReleaseRadar.Api.Observability;
using ReleaseRadar.Domain.Services;
using ReleaseRadar.Domain;

namespace ReleaseRadar.Api.Workers;

public sealed class ReconciliationWorker(IServiceScopeFactory scopeFactory, ILogger<ReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<DeploymentService>();
                await service.ReconcileAsync(stoppingToken);

                var repository = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();
                var applications = await repository.ListAsync(stoppingToken);
                foreach (var application in applications)
                {
                    DeploymentMetrics.ApplicationHealth
                        .WithLabels(application.Name, application.Environment)
                        .Set(application.Health == Domain.Models.DeploymentHealth.Healthy ? 1 : 0);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Deployment reconciliation failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
