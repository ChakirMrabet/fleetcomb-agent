using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FleetComb.Agent;

public sealed class UpdateCoordinator(
    IAgentStateStore agentStateStore,
    ISoftwareStateStore softwareState,
    AgentApiClient api,
    ILogger<UpdateCoordinator> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<UpdateStatus> StartAsync(
        Guid applicationId, CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken))
            return await softwareState.LoadUpdateStatusAsync(cancellationToken);
        try
        {
            var agent = await agentStateStore.LoadAsync(cancellationToken)
                ?? throw new InvalidOperationException("The Agent is not enrolled.");
            var desired = await softwareState.LoadDesiredAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "Desired software has not been received from FleetComb yet.");
            var application = desired.SoftwarePlatforms
                .SelectMany(platform => platform.Applications)
                .SingleOrDefault(item => item.Id == applicationId)
                ?? throw new InvalidOperationException(
                    "The Application is not assigned to this Asset.");
            var release = application.LatestRelease
                ?? throw new InvalidOperationException(
                    "No published release matches this Agent's platform.");
            var stagingDirectory = Path.Combine(AgentDataDirectory.Resolve(), "updates");
            Directory.CreateDirectory(stagingDirectory);
            var artifactPath = Path.Combine(
                stagingDirectory, $"{release.Id:N}-{Path.GetFileName(release.FileName)}");
            await SaveStatus(
                applicationId, release.Id, "Downloading", 0,
                $"Downloading {release.FileName}.", cancellationToken);
            var progress = new InlineProgress<int>(percent =>
                SaveStatus(
                    applicationId, release.Id, "Downloading", percent,
                    $"Downloading {release.FileName}.", CancellationToken.None)
                    .GetAwaiter().GetResult());
            await api.DownloadReleaseAsync(
                agent, release, artifactPath, progress, cancellationToken);
            await SaveStatus(
                applicationId, release.Id, "Verified", 100,
                "Artifact length and SHA-256 checksum verified.", cancellationToken);
            if (release.PackageType is "Pkg" or "Zip")
                return await SaveStatus(
                    applicationId, release.Id, "AwaitingAdapter", 100,
                    $"Customer adapter must install: {artifactPath}", cancellationToken);
            await SaveStatus(
                applicationId, release.Id, "Installing", 100,
                $"Installing {release.FileName}.", cancellationToken);
            var exitCode = await RunStandardInstaller(release, artifactPath, cancellationToken);
            if (exitCode != 0)
                return await SaveStatus(
                    applicationId, release.Id, "Failed", 100,
                    $"Installer exited with code {exitCode}.", cancellationToken);
            await RecordInstalled(applicationId, release, cancellationToken);
            return await SaveStatus(
                applicationId, release.Id, "Completed", 100,
                $"Application {release.Version} installed.", cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or InvalidOperationException)
        {
            logger.LogError(exception, "Software update failed.");
            var current = await softwareState.LoadUpdateStatusAsync(cancellationToken);
            return await SaveStatus(
                current.ApplicationId, current.SoftwareReleaseId, "Failed",
                current.ProgressPercent, exception.Message, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task<UpdateStatus> CompleteAdapterInstallAsync(
        Guid applicationId, bool succeeded, string message, CancellationToken cancellationToken)
    {
        var current = await softwareState.LoadUpdateStatusAsync(cancellationToken);
        if (current.State != "AwaitingAdapter" ||
            current.ApplicationId != applicationId ||
            !current.SoftwareReleaseId.HasValue)
            throw new InvalidOperationException(
                "There is no customer-adapter installation awaiting completion.");
        var desired = await softwareState.LoadDesiredAsync(cancellationToken)
            ?? throw new InvalidOperationException("Desired software is unavailable.");
        var release = desired.SoftwarePlatforms.SelectMany(item => item.Applications)
            .Single(item => item.Id == applicationId).LatestRelease!;
        if (succeeded) await RecordInstalled(applicationId, release, cancellationToken);
        return await SaveStatus(
            applicationId, release.Id, succeeded ? "Completed" : "Failed", 100,
            string.IsNullOrWhiteSpace(message)
                ? succeeded ? $"Application {release.Version} installed." : "Installation failed."
                : message.Trim(),
            cancellationToken);
    }

    private async Task RecordInstalled(
        Guid applicationId, DesiredRelease release, CancellationToken cancellationToken) =>
        await softwareState.SaveObservationAsync(
            new ApplicationObservation(
                applicationId, release.Id, release.Version, "AgentInstalled",
                DateTimeOffset.UtcNow),
            cancellationToken);

    private static async Task<int> RunStandardInstaller(
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
                $"{release.PackageType} is not a standard installer on this operating system.");
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("The installer process could not be started.");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    private async Task<UpdateStatus> SaveStatus(
        Guid? applicationId, Guid? releaseId, string state, int progress,
        string message, CancellationToken cancellationToken)
    {
        var status = new UpdateStatus(
            applicationId, releaseId, state, progress, message, DateTimeOffset.UtcNow);
        await softwareState.SaveUpdateStatusAsync(status, cancellationToken);
        return status;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
