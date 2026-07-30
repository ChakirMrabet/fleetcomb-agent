using System.Diagnostics;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Infrastructure.Updates;

public sealed class StandardReleaseInstaller : IReleaseInstaller
{
    public bool CanInstall(DesiredRelease release) =>
        release.PackageType == "Deb" && OperatingSystem.IsLinux() ||
        release.PackageType == "Exe" && OperatingSystem.IsWindows();

    public async Task<int> InstallAsync(
        DesiredRelease release, string artifactPath, CancellationToken cancellationToken)
    {
        ProcessStartInfo start;
        if (release.PackageType == "Deb" && OperatingSystem.IsLinux())
            start = new ProcessStartInfo("dpkg")
            {
                UseShellExecute = false,
                ArgumentList = { "--install", artifactPath }
            };
        else if (release.PackageType == "Exe" && OperatingSystem.IsWindows())
            start = new ProcessStartInfo(artifactPath) { UseShellExecute = false };
        else
            throw new InvalidOperationException(
                $"{release.PackageType} is not supported by the standard installer.");
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("The installer process could not be started.");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
