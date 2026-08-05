namespace ReleaseRadar.Infrastructure.Configuration;

public sealed class IntegrationOptions
{
    public const string SectionName = "Integrations";
    public ArgoCdOptions ArgoCd { get; set; } = new();
    public KubernetesOptions Kubernetes { get; set; } = new();
    public PrometheusOptions Prometheus { get; set; } = new();
    public GitHubOptions GitHub { get; set; } = new();
}

public sealed class ArgoCdOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://argocd-server.argocd.svc";
    public string Token { get; set; } = string.Empty;
}

public sealed class KubernetesOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://kubernetes.default.svc";
    public string Token { get; set; } = string.Empty;
}

public sealed class PrometheusOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://prometheus:9090";
}

public sealed class GitHubOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.github.com";
    public string Token { get; set; } = string.Empty;
}
