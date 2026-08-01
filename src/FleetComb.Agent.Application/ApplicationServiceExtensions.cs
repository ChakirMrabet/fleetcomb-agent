using FluentValidation;
using FleetComb.Agent.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FleetComb.Agent.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection ConfigureApplication(this IServiceCollection services)
    {
        services.AddMediatR(typeof(ApplicationServiceExtensions));
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceExtensions).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services
            .AddSingleton<EnrollmentService>()
            .AddSingleton<AgentSynchronizationService>()
            .AddSingleton<UpdateService>()
            .AddSingleton<AgentResetService>()
            .AddSingleton<CustomerAdapterService>()
            .AddSingleton<ProducerMessageService>()
            .AddSingleton<FileUploadService>()
            .AddSingleton<AgentStatusService>();
    }
}
