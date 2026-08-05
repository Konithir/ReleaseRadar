using Prometheus;

namespace ReleaseRadar.Api.Observability;

public static class DeploymentMetrics
{
    public static readonly Counter Actions = Metrics.CreateCounter(
        "release_radar_deployment_actions_total",
        "Deployment actions requested through ReleaseRadar.",
        new CounterConfiguration { LabelNames = ["application", "action", "result"] });

    public static readonly Gauge ApplicationHealth = Metrics.CreateGauge(
        "release_radar_application_health",
        "Application health where 1 is healthy and 0 is unhealthy.",
        new GaugeConfiguration { LabelNames = ["application", "environment"] });
}
