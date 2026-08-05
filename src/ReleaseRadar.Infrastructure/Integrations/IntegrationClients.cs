using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ReleaseRadar.Domain;
using ReleaseRadar.Domain.Models;
using ReleaseRadar.Infrastructure.Configuration;

namespace ReleaseRadar.Infrastructure.Integrations;

public sealed class ArgoCdClient(HttpClient httpClient, IOptions<IntegrationOptions> options) : IArgoCdClient
{
    private readonly ArgoCdOptions _options = options.Value.ArgoCd;

    public string Mode => _options.Enabled ? "live" : "demo";
    public string Endpoint => _options.Enabled ? _options.BaseUrl : "built-in simulator";

    public async Task<ArgoApplicationState> GetStateAsync(DeploymentApplication application, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new ArgoApplicationState(application.Health, application.SyncStatus, application.LiveRevision);
        }

        using var request = CreateRequest(HttpMethod.Get, $"/api/v1/applications/{Uri.EscapeDataString(application.Name)}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var status = json.RootElement.GetProperty("status");
        var health = ParseHealth(status.GetProperty("health").GetProperty("status").GetString());
        var sync = status.GetProperty("sync");
        var syncStatus = ParseSync(sync.GetProperty("status").GetString());
        var revision = sync.TryGetProperty("revision", out var value) ? value.GetString() ?? application.LiveRevision : application.LiveRevision;
        return new ArgoApplicationState(health, syncStatus, revision);
    }

    public Task<DeploymentCommandResult> SyncAsync(DeploymentApplication application, string revision, CancellationToken cancellationToken) =>
        SendSyncAsync(application, revision, "Sync accepted by Argo CD.", cancellationToken);

    public Task<DeploymentCommandResult> RollbackAsync(DeploymentApplication application, string revision, CancellationToken cancellationToken) =>
        SendSyncAsync(application, revision, $"Revision {revision} restored through Argo CD.", cancellationToken);

    private async Task<DeploymentCommandResult> SendSyncAsync(DeploymentApplication application, string revision, string message, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            await Task.Delay(350, cancellationToken);
            return new DeploymentCommandResult(true, revision, message);
        }

        using var request = CreateRequest(HttpMethod.Post, $"/api/v1/applications/{Uri.EscapeDataString(application.Name)}/sync");
        request.Content = JsonContent.Create(new
        {
            revision,
            prune = true,
            dryRun = false,
            strategy = new { hook = new { force = true } }
        });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var accepted = response.IsSuccessStatusCode;
        var responseMessage = accepted ? message : $"Argo CD returned {(int)response.StatusCode} {response.ReasonPhrase}.";
        return new DeploymentCommandResult(accepted, revision, responseMessage);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(_options.BaseUrl.TrimEnd('/') + "/"), path.TrimStart('/')));
        if (!string.IsNullOrWhiteSpace(_options.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
        }
        return request;
    }

    private static DeploymentHealth ParseHealth(string? value) => Enum.TryParse<DeploymentHealth>(value, true, out var parsed) ? parsed : DeploymentHealth.Unknown;
    private static DeploymentSyncStatus ParseSync(string? value) => Enum.TryParse<DeploymentSyncStatus>(value, true, out var parsed) ? parsed : DeploymentSyncStatus.Unknown;
}

public sealed class KubernetesClient(HttpClient httpClient, IOptions<IntegrationOptions> options) : IKubernetesClient
{
    private readonly KubernetesOptions _options = options.Value.Kubernetes;

    public string Mode => _options.Enabled ? "live" : "demo";
    public string Endpoint => _options.Enabled ? _options.BaseUrl : "built-in simulator";

    public async Task<KubernetesWorkloadState> GetWorkloadAsync(DeploymentApplication application, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new KubernetesWorkloadState(application.ReadyReplicas, application.DesiredReplicas);
        }

        var uri = new Uri(new Uri(_options.BaseUrl.TrimEnd('/') + "/"), $"apis/apps/v1/namespaces/{Uri.EscapeDataString(application.Namespace)}/deployments/{Uri.EscapeDataString(application.Name)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var token = ResolveToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = json.RootElement;
        var desired = root.GetProperty("spec").GetProperty("replicas").GetInt32();
        var status = root.GetProperty("status");
        var ready = status.TryGetProperty("readyReplicas", out var readyValue) ? readyValue.GetInt32() : 0;
        return new KubernetesWorkloadState(ready, desired);
    }

    private string ResolveToken()
    {
        if (!string.IsNullOrWhiteSpace(_options.Token))
        {
            return _options.Token;
        }

        const string serviceAccountToken = "/var/run/secrets/kubernetes.io/serviceaccount/token";
        return File.Exists(serviceAccountToken) ? File.ReadAllText(serviceAccountToken).Trim() : string.Empty;
    }
}

public sealed class PrometheusClient(HttpClient httpClient, IOptions<IntegrationOptions> options) : IPrometheusClient
{
    private readonly PrometheusOptions _options = options.Value.Prometheus;

    public string Mode => _options.Enabled ? "live" : "demo";
    public string Endpoint => _options.Enabled ? _options.BaseUrl : "built-in simulator";

    public async Task<WorkloadMetrics> GetMetricsAsync(DeploymentApplication application, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new WorkloadMetrics(application.CpuPercent, application.MemoryPercent);
        }

        var cpuQuery = $"sum(rate(container_cpu_usage_seconds_total{{namespace=\"{application.Namespace}\",pod=~\"{application.Name}.*\"}}[5m])) * 100";
        var memoryQuery = $"sum(container_memory_working_set_bytes{{namespace=\"{application.Namespace}\",pod=~\"{application.Name}.*\"}}) / sum(kube_pod_container_resource_limits{{namespace=\"{application.Namespace}\",pod=~\"{application.Name}.*\",resource=\"memory\"}}) * 100";
        var cpuTask = QueryAsync(cpuQuery, cancellationToken);
        var memoryTask = QueryAsync(memoryQuery, cancellationToken);
        await Task.WhenAll(cpuTask, memoryTask);
        return new WorkloadMetrics(await cpuTask, await memoryTask);
    }

    private async Task<double> QueryAsync(string query, CancellationToken cancellationToken)
    {
        var uri = new Uri(new Uri(_options.BaseUrl.TrimEnd('/') + "/"), $"api/v1/query?query={Uri.EscapeDataString(query)}");
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var result = json.RootElement.GetProperty("data").GetProperty("result");
        if (result.GetArrayLength() == 0)
        {
            return 0;
        }

        var raw = result[0].GetProperty("value")[1].GetString();
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? Math.Clamp(parsed, 0, 100) : 0;
    }
}

public sealed class GitHubSourceControlClient(HttpClient httpClient, IOptions<IntegrationOptions> options) : ISourceControlClient
{
    private readonly GitHubOptions _options = options.Value.GitHub;

    public string Mode => _options.Enabled ? "live" : "demo";
    public string Endpoint => _options.Enabled ? _options.BaseUrl : "built-in revision generator";

    public async Task<string> ResolveRevisionAsync(DeploymentApplication application, string? requestedRevision, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedRevision))
        {
            return requestedRevision.Trim();
        }

        if (!_options.Enabled)
        {
            var seed = Encoding.UTF8.GetBytes($"{application.Name}:{DateTimeOffset.UtcNow:yyyyMMddHHmm}");
            return Convert.ToHexString(SHA256.HashData(seed))[..7].ToLowerInvariant();
        }

        var uri = new Uri(new Uri(_options.BaseUrl.TrimEnd('/') + "/"), $"repos/{application.Repository}/commits?per_page=1");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("ReleaseRadar/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        if (!string.IsNullOrWhiteSpace(_options.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var sha = json.RootElement[0].GetProperty("sha").GetString() ?? application.DesiredRevision;
        return sha[..Math.Min(7, sha.Length)];
    }
}
