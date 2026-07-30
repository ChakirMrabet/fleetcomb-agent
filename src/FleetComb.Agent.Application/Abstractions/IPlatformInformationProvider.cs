using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application.Abstractions;

public interface IPlatformInformationProvider
{
    PlatformInformation Current();
}
