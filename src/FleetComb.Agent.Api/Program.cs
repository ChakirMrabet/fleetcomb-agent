using System.Security.Cryptography;
using System.Text;
using FleetComb.Agent.Api;
using FleetComb.Agent.Application;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;
using FleetComb.Agent.Infrastructure.Cloud;
using FleetComb.Agent.Infrastructure.Persistence;
using FleetComb.Agent.Infrastructure.Updates;
using FleetComb.Agent.Ui;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "FleetComb Agent");
builder.Services.AddSystemd();
builder.Services
    .AddAgentPersistence()
    .AddFleetCombCloud()
    .AddAgentUpdateInfrastructure();
builder.Services.AddSingleton<EnrollmentService>();
builder.Services.AddSingleton<AgentSynchronizationService>();
builder.Services.AddSingleton<UpdateService>();
builder.Services.AddSingleton<AgentResetService>();
builder.Services.AddHostedService<SynchronizationWorker>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "AgentUi";
        options.DefaultChallengeScheme = "AgentUi";
        options.DefaultSignInScheme = "AgentUi";
        options.DefaultSignOutScheme = "AgentUi";
    })
    .AddCookie("AgentUi", options =>
    {
        options.LoginPath = "/Login";
        options.Cookie.Name = "FleetCombAgent.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();
builder.Services.AddRateLimiter(options =>
    options.AddFixedWindowLimiter("setup", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    }));
builder.Services.AddRazorPages()
    .AddApplicationPart(typeof(UiAssemblyMarker).Assembly);

var configuredUrls = builder.Configuration["AgentWeb:Urls"];
builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(configuredUrls)
    ? "http://0.0.0.0:5137"
    : configuredUrls);

var app = builder.Build();
var command = args.FirstOrDefault();
if (command?.Equals("enroll", StringComparison.OrdinalIgnoreCase) == true)
{
    await EnrollFromCommandLine(
        args.Skip(1).ToArray(),
        app.Services.GetRequiredService<EnrollmentService>());
    return;
}
if (command?.Equals("local-token", StringComparison.OrdinalIgnoreCase) == true)
{
    Console.WriteLine(await app.Services.GetRequiredService<EnrollmentService>()
        .GetOrCreateLocalApiTokenAsync(CancellationToken.None));
    return;
}

app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/agent-ui/agent.css", () =>
    Results.Text(UiAssets.Css, "text/css; charset=utf-8"));

app.MapPost("/logout", async (HttpContext context, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(context);
    await context.SignOutAsync("AgentUi");
    return Results.Redirect("/Login");
}).RequireAuthorization();

var localApi = app.MapGroup("/local/v1");
localApi.AddEndpointFilter(async (context, next) =>
{
    var request = context.HttpContext.Request;
    var registrationStore =
        context.HttpContext.RequestServices.GetRequiredService<IAgentRegistrationStore>();
    var registration = await registrationStore.LoadAsync(request.HttpContext.RequestAborted);
    var supplied = request.Headers.Authorization.ToString();
    var expected = registration is null ? "" : $"Bearer {registration.LocalApiToken}";
    if (string.IsNullOrWhiteSpace(expected) ||
        !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected)))
        return Results.Unauthorized();
    return await next(context);
});
localApi.MapGet("/status", async (
    IAgentRegistrationStore registrations, ISoftwareStateStore software,
    CancellationToken token) =>
{
    var registration = await registrations.LoadAsync(token);
    return Results.Ok(new
    {
        agent = registration is null ? null : new
        {
            registration.AssetId,
            registration.InstallationId,
            serverUrl = registration.ServerUrl.ToString()
        },
        desiredState = await software.LoadDesiredAsync(token),
        installedApplications = await software.LoadInventoryAsync(token),
        update = await software.LoadUpdateStatusAsync(token)
    });
});
localApi.MapGet("/desired-state", async (
    ISoftwareStateStore software, CancellationToken token) =>
        await software.LoadDesiredAsync(token) is { } desired
            ? Results.Ok(desired) : Results.NoContent());
localApi.MapGet("/applications", async (
    ISoftwareStateStore software, CancellationToken token) =>
        Results.Ok(await software.LoadInventoryAsync(token)));
localApi.MapPost("/applications/report", async (
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
localApi.MapGet("/updates/current", async (
    ISoftwareStateStore software, CancellationToken token) =>
        Results.Ok(await software.LoadUpdateStatusAsync(token)));
localApi.MapPost("/applications/{applicationId:guid}/install", async (
    Guid applicationId, UpdateService updates, CancellationToken token) =>
        Results.Ok(await updates.StartAsync(applicationId, token)));
localApi.MapPost("/applications/{applicationId:guid}/install-completion", async (
    Guid applicationId, AdapterInstallCompletion request,
    UpdateService updates, CancellationToken token) =>
{
    try
    {
        return Results.Ok(await updates.CompleteAdapterInstallAsync(
            applicationId, request.Succeeded, request.Message, token));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

app.MapRazorPages();
await app.RunAsync();

static async Task EnrollFromCommandLine(
    string[] arguments, EnrollmentService enrollment)
{
    var server = Value(arguments, "--server");
    var code = Value(arguments, "--code");
    if (!Uri.TryCreate(server, UriKind.Absolute, out var serverUrl) ||
        string.IsNullOrWhiteSpace(code))
        throw new ArgumentException(
            "Usage: FleetComb.Agent enroll --server https://fleetcomb.example --code FC1-...");
    var result = await enrollment.EnrollAsync(
        serverUrl, code, CancellationToken.None);
    Console.WriteLine($"FleetComb Agent enrolled for Asset {result.AssetId}.");
    Console.WriteLine($"Identity saved under: {result.DataDirectory}");
    Console.WriteLine("Local API bearer token:");
    Console.WriteLine(result.LocalApiToken);
    Console.WriteLine("Start the Agent and open its Web UI to create the local administrator.");
}

static string Value(string[] arguments, string name)
{
    var index = Array.FindIndex(
        arguments, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : string.Empty;
}

internal sealed record ExternalApplicationReport(
    Guid ApplicationId, Guid? SoftwareReleaseId, string Version);
internal sealed record AdapterInstallCompletion(bool Succeeded, string Message);
