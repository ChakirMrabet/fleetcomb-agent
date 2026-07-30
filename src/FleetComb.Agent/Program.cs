using System.Security.Cryptography;
using FleetComb.Agent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;

if (args.FirstOrDefault()?.Equals("enroll", StringComparison.OrdinalIgnoreCase) == true)
{
    await Enroll(args.Skip(1).ToArray());
    return;
}
if (args.FirstOrDefault()?.Equals("local-token", StringComparison.OrdinalIgnoreCase) == true)
{
    var existing = await new FileAgentStateStore().LoadAsync(CancellationToken.None)
        ?? throw new InvalidOperationException("The Agent is not enrolled.");
    if (string.IsNullOrWhiteSpace(existing.LocalApiToken))
    {
        existing = existing with { LocalApiToken = CreateToken() };
        await new FileAgentStateStore().SaveAsync(existing, CancellationToken.None);
    }
    Console.WriteLine(existing.LocalApiToken);
    return;
}

var stateStore = new FileAgentStateStore();
var state = await stateStore.LoadAsync(CancellationToken.None);
if (state is not null && string.IsNullOrWhiteSpace(state.LocalApiToken))
{
    state = state with { LocalApiToken = CreateToken() };
    await stateStore.SaveAsync(state, CancellationToken.None);
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5137");
builder.Services.AddWindowsService(options => options.ServiceName = "FleetComb Agent");
builder.Services.AddSystemd();
builder.Services.AddSingleton<IAgentStateStore>(stateStore);
builder.Services.AddSingleton<ISoftwareStateStore, FileSoftwareStateStore>();
builder.Services.AddSingleton<UpdateCoordinator>();
builder.Services.AddHttpClient<AgentApiClient>();
builder.Services.AddHostedService<HeartbeatWorker>();
var app = builder.Build();

app.Use(async (context, next) =>
{
    var current = await stateStore.LoadAsync(context.RequestAborted);
    var supplied = context.Request.Headers.Authorization.ToString();
    var expected = current is null ? "" : $"Bearer {current.LocalApiToken}";
    if (string.IsNullOrWhiteSpace(expected) ||
        !CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(supplied),
            System.Text.Encoding.UTF8.GetBytes(expected)))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    await next(context);
});

app.MapGet("/local/v1/status", async (
    IAgentStateStore agents, ISoftwareStateStore software, CancellationToken token) =>
{
    var agent = await agents.LoadAsync(token);
    return Results.Ok(new
    {
        agent = agent is null ? null : new
        {
            agent.AssetId,
            agent.InstallationId,
            serverUrl = agent.ServerUrl.ToString()
        },
        desiredState = await software.LoadDesiredAsync(token),
        installedApplications = await software.LoadInventoryAsync(token),
        update = await software.LoadUpdateStatusAsync(token)
    });
});
app.MapGet("/local/v1/desired-state", async (
    ISoftwareStateStore software, CancellationToken token) =>
        await software.LoadDesiredAsync(token) is { } desired
            ? Results.Ok(desired) : Results.NoContent());
app.MapGet("/local/v1/applications", async (
    ISoftwareStateStore software, CancellationToken token) =>
        Results.Ok(await software.LoadInventoryAsync(token)));
app.MapPost("/local/v1/applications/report", async (
    ExternalApplicationReport request, ISoftwareStateStore software,
    CancellationToken token) =>
{
    if (request.ApplicationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Version))
        return Results.BadRequest();
    await software.SaveObservationAsync(
        new ApplicationObservation(
            request.ApplicationId, request.SoftwareReleaseId, request.Version.Trim(),
            "ExternallyReported", DateTimeOffset.UtcNow), token);
    return Results.Accepted();
});
app.MapGet("/local/v1/updates/current", async (
    ISoftwareStateStore software, CancellationToken token) =>
        Results.Ok(await software.LoadUpdateStatusAsync(token)));
app.MapPost("/local/v1/applications/{applicationId:guid}/install", async (
    Guid applicationId, UpdateCoordinator coordinator, CancellationToken token) =>
        Results.Ok(await coordinator.StartAsync(applicationId, token)));
app.MapPost("/local/v1/applications/{applicationId:guid}/install-completion", async (
    Guid applicationId, AdapterInstallCompletion request,
    UpdateCoordinator coordinator, CancellationToken token) =>
{
    try
    {
        return Results.Ok(await coordinator.CompleteAdapterInstallAsync(
            applicationId, request.Succeeded, request.Message, token));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

await app.RunAsync();

static async Task Enroll(string[] arguments)
{
    var server = Value(arguments, "--server");
    var code = Value(arguments, "--code");
    if (!Uri.TryCreate(server, UriKind.Absolute, out var serverUrl) ||
        string.IsNullOrWhiteSpace(code))
        throw new ArgumentException(
            "Usage: FleetComb.Agent enroll --server https://fleetcomb.example --code FC1-...");
    var (publicKey, privateKey) = AgentIdentity.Create();
    var api = new AgentApiClient(new HttpClient());
    var claimed = await api.ClaimAsync(serverUrl, code, publicKey, CancellationToken.None);
    var localApiToken = CreateToken();
    var enrolled = new AgentState(
        serverUrl, claimed.TenantId, claimed.AssetId, claimed.AgentInstallationId,
        privateKey, claimed.HeartbeatIntervalSeconds, localApiToken);
    var enrollmentStore = new FileAgentStateStore();
    await enrollmentStore.SaveAsync(enrolled, CancellationToken.None);
    var stateDirectory = AgentDataDirectory.Resolve();
    Console.WriteLine($"FleetComb Agent enrolled for Asset {claimed.AssetId}.");
    Console.WriteLine($"Identity saved under: {stateDirectory}");
    Console.WriteLine("Local API: http://127.0.0.1:5137/local/v1");
    Console.WriteLine($"Local API bearer token: {localApiToken}");
    Console.WriteLine("Store this token securely for the local System Manager adapter.");
    Console.WriteLine("Keep FLEETCOMB_AGENT_DATA_DIR set, then run:");
    Console.WriteLine("  dotnet run --project src/FleetComb.Agent");
}

static string CreateToken() =>
    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

static string Value(string[] arguments, string name)
{
    var index = Array.FindIndex(
        arguments, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : string.Empty;
}

internal sealed record ExternalApplicationReport(
    Guid ApplicationId, Guid? SoftwareReleaseId, string Version);
internal sealed record AdapterInstallCompletion(bool Succeeded, string Message);
