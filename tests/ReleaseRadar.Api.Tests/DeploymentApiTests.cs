using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using ReleaseRadar.Contracts;

namespace ReleaseRadar.Api.Tests;

public sealed class DeploymentApiTests : IClassFixture<ReleaseRadarFactory>
{
    private readonly HttpClient _client;

    public DeploymentApiTests(ReleaseRadarFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Overview_contains_seeded_deployment_state()
    {
        var response = await _client.GetAsync("/api/overview");
        var overview = await response.Content.ReadFromJsonAsync<OverviewDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(overview);
        Assert.Equal(5, overview.Applications);
        Assert.Equal(3, overview.Clusters.Count);
        Assert.Contains(overview.RecentActivity, item => item.Application == "identity-api");
    }

    [Fact]
    public async Task Sync_updates_revision_and_writes_audit_event()
    {
        const string revision = "e01c4ab";
        var response = await _client.PostAsJsonAsync("/api/applications/checkout-api/sync", new DeploymentActionRequest(revision, "integration-test"));
        var result = await response.Content.ReadFromJsonAsync<DeploymentActionResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.Equal(revision, result.State?.LiveRevision);

        var audit = await _client.GetFromJsonAsync<AuditEventDto[]>("/api/audit?application=checkout-api");
        Assert.Contains(audit!, item => item.Revision == revision && item.Actor == "integration-test");
    }

    [Fact]
    public async Task Unknown_application_returns_not_found()
    {
        var response = await _client.GetAsync("/api/applications/not-found");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class ReleaseRadarFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("DatabaseProvider", "InMemory");
        builder.UseSetting("InMemoryDatabaseName", $"release-radar-tests-{Guid.NewGuid():N}");
    }
}
