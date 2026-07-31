using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Api.Realtime;
using FleetComb.Agent.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;

namespace FleetComb.Agent.Api;

public static class ApiServiceExtensions
{
    public static IServiceCollection AddAgentApi(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthenticationSchemes.AgentUi;
                options.DefaultChallengeScheme = AuthenticationSchemes.AgentUi;
                options.DefaultSignInScheme = AuthenticationSchemes.AgentUi;
                options.DefaultSignOutScheme = AuthenticationSchemes.AgentUi;
            })
            .AddCookie(AuthenticationSchemes.AgentUi, options =>
            {
                options.LoginPath = "/Login";
                options.Cookie.Name = "FleetCombAgent.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            })
            .AddScheme<AuthenticationSchemeOptions, LocalApiAuthenticationHandler>(
                AuthenticationSchemes.LocalApi, _ => { });
        services.AddAuthorization();
        services.AddAntiforgery();
        services.AddControllers();
        services.AddSignalR();
        services.AddSingleton<IAgentStatusNotifier, SignalRAgentStatusNotifier>();
        services.AddHostedService<SynchronizationWorker>();
        return services;
    }
}
