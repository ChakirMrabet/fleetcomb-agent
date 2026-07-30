using FleetComb.Agent.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FleetComb.Agent.Infrastructure.Cloud;

public static class CloudServiceExtensions
{
    public static IServiceCollection AddFleetCombCloud(this IServiceCollection services)
    {
        services.AddSingleton<IAgentIdentityProvider, AgentIdentityProvider>();
        services.AddSingleton<IPlatformInformationProvider, PlatformInformationProvider>();
        services.AddHttpClient<IAgentCloudClient, AgentCloudClient>();
        return services;
    }
}
