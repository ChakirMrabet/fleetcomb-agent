using FleetComb.Agent.Application;
using FleetComb.Agent.Application.Status.Queries;
using FleetComb.Agent.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FleetComb.Agent.Ui.Pages;

[Authorize]
public sealed class IndexModel(IMediator mediator) : PageModel
{
    public LocalAgentIdentity? Registration { get; private set; }
    public DesiredState? Desired { get; private set; }
    public IReadOnlyList<ApplicationObservation> Inventory { get; private set; } = [];
    public UpdateStatus Update { get; private set; } = UpdateStatus.Idle();
    public SynchronizationStatus Synchronization { get; private set; } =
        SynchronizationStatus.NotEnrolled();
    public CustomerAdapterStatus Adapter { get; private set; } =
        CustomerAdapterStatus.NotConnected();

    public async Task OnGet(CancellationToken cancellationToken)
    {
        var status = await mediator.Send(
            new GetLocalAgentStatus.Query(), cancellationToken);
        Registration = status.Agent;
        Desired = status.DesiredState;
        Inventory = status.InstalledApplications;
        Update = status.Update;
        Synchronization = status.Synchronization;
        Adapter = status.Adapter;
    }
}
