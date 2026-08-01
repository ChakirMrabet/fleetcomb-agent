using FleetComb.Agent.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FleetComb.Agent.Infrastructure.Persistence;

public static class PersistenceServiceExtensions
{
    public static IServiceCollection AddAgentPersistence(this IServiceCollection services) =>
        services
            .AddSingleton<IAgentRegistrationStore, FileAgentRegistrationStore>()
            .AddSingleton<ILocalAdministratorStore, FileLocalAdministratorStore>()
            .AddSingleton<ILocalAdapterStore, FileLocalAdapterStore>()
            .AddSingleton<IProducerMessageStore, FileProducerMessageStore>()
            .AddSingleton<IAgentDataReset, FileAgentDataReset>()
            .AddSingleton<ISoftwareStateStore, FileSoftwareStateStore>();
}
