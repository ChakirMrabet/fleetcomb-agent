using FleetComb.Agent.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FleetComb.Agent.Infrastructure.Updates;

public static class UpdateServiceExtensions
{
    public static IServiceCollection AddAgentUpdateInfrastructure(
        this IServiceCollection services) => services
        .AddSingleton<IReleaseInstaller, StandardReleaseInstaller>()
        .AddSingleton<IReleaseArtifactValidator, ReleaseArtifactValidator>();
}
