using FleetComb.Agent.Application.Synchronization.Commands;
using FleetComb.Agent.Application.Updates.Commands;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetComb.Agent.Api;

public sealed class SynchronizationWorker(
    IMediator mediator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await mediator.Send(new RecoverInterruptedUpdate.Command(), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            await mediator.Send(new RunSynchronization.Command(), stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
