using FleetComb.Agent.Api;
using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Application;
using FleetComb.Agent.Infrastructure.Cloud;
using FleetComb.Agent.Infrastructure.Persistence;
using FleetComb.Agent.Infrastructure.Updates;
using FleetComb.Agent.Ui;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "FleetComb Agent");
builder.Services.AddSystemd();
builder.Services
    .ConfigureApplication()
    .AddAgentPersistence()
    .AddFleetCombCloud()
    .AddAgentUpdateInfrastructure()
    .AddAgentApi()
    .AddRazorPages()
    .AddApplicationPart(typeof(UiAssemblyMarker).Assembly);
builder.Services.AddRateLimiter(options =>
    options.AddFixedWindowLimiter("setup", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    }));

var configuredUrls = builder.Configuration["AgentWeb:Urls"];
builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(configuredUrls)
    ? "http://0.0.0.0:5137"
    : configuredUrls);

var app = builder.Build();
if (await CliCommandRunner.TryRunAsync(args, app.Services)) return;

app.UseExceptionHandler();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
await app.RunAsync();
