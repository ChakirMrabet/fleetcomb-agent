using FleetComb.Agent.Application.Uploads.Commands;
using MediatR;

namespace FleetComb.Agent.Api;

public sealed class FileUploadWorker(IMediator mediator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await mediator.Send(new ProcessPendingFileUpload.Command(), stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
