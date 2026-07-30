using System.Runtime.InteropServices;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Infrastructure.Cloud;

public sealed class PlatformInformationProvider : IPlatformInformationProvider
{
    public PlatformInformation Current() => new(
        Environment.MachineName,
        OperatingSystem.IsWindows() ? "Windows" :
        OperatingSystem.IsLinux() ? "Linux" :
        OperatingSystem.IsMacOS() ? "MacOS" : "Unknown",
        RuntimeInformation.OSDescription,
        RuntimeInformation.OSArchitecture.ToString());
}
