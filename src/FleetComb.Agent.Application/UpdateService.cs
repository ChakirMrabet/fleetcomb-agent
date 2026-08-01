using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;
using Microsoft.Extensions.Logging;

namespace FleetComb.Agent.Application;

public sealed class UpdateService(
    IAgentRegistrationStore registrations,
    ISoftwareStateStore software,
    IAgentCloudClient cloud,
    IReleaseArtifactValidator artifactValidator,
    IEnumerable<IReleaseInstaller> installers,
    IAgentStatusNotifier notifier,
    ILogger<UpdateService> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<UpdateStatus> StartAsync(
        Guid applicationId, CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken))
            return await software.LoadUpdateStatusAsync(cancellationToken);
        try
        {
            var attemptId = Guid.NewGuid();
            var registration = await registrations.LoadAsync(cancellationToken)
                ?? throw new InvalidOperationException("The Agent is not enrolled.");
            var desired = await software.LoadDesiredAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "Desired software has not been received from FleetComb yet.");
            var application = desired.SoftwarePlatforms
                .SelectMany(platform => platform.Applications)
                .SingleOrDefault(item => item.Id == applicationId)
                ?? throw new InvalidOperationException(
                    "The Application is not assigned to this Asset.");
            var release = application.LatestRelease
                ?? throw new InvalidOperationException(
                    "No published release matches this Agent platform.");
            var stagingDirectory = Path.Combine(registrations.DataDirectory, "updates");
            Directory.CreateDirectory(stagingDirectory);
            var artifactPath = Path.Combine(
                stagingDirectory, $"{release.Id:N}-{Path.GetFileName(release.FileName)}");
            await SaveStatus(attemptId, applicationId, release.Id, "Downloading", 0,
                $"Downloading {release.FileName}.", cancellationToken);
            await cloud.DownloadReleaseAsync(
                registration, release, artifactPath,
                new InlineProgress<int>(percent =>
                    SaveStatus(attemptId, applicationId, release.Id, "Downloading", percent,
                            $"Downloading {release.FileName}.", CancellationToken.None)
                        .GetAwaiter().GetResult()),
                cancellationToken);
            await artifactValidator.ValidateAsync(release, artifactPath, cancellationToken);
            await SaveStatus(attemptId, applicationId, release.Id, "Verified", 100,
                "Artifact length, SHA-256 checksum, and package format verified.",
                cancellationToken);
            var installer = installers.FirstOrDefault(item => item.CanInstall(release));
            if (installer is null)
                return await SaveStatus(
                    attemptId, applicationId, release.Id, "AwaitingAdapter", 100,
                    $"Customer adapter must install: {artifactPath}", cancellationToken);
            await SaveStatus(attemptId, applicationId, release.Id, "Installing", 100,
                $"Installing {release.FileName}.", cancellationToken);
            var exitCode = await installer.InstallAsync(release, artifactPath, cancellationToken);
            if (exitCode != 0)
                return await SaveStatus(attemptId, applicationId, release.Id, "Failed", 100,
                    $"Installer exited with code {exitCode}.", cancellationToken);
            await RecordInstalled(applicationId, release, cancellationToken);
            return await SaveStatus(attemptId, applicationId, release.Id, "Completed", 100,
                $"Application {release.Version} installed.", cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or InvalidOperationException)
        {
            logger.LogError(exception, "Software update failed.");
            var current = await software.LoadUpdateStatusAsync(cancellationToken);
            return await SaveStatus(
                current.AttemptId == Guid.Empty ? Guid.NewGuid() : current.AttemptId,
                current.ApplicationId, current.SoftwareReleaseId, "Failed",
                current.ProgressPercent, exception.Message, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task<UpdateStatus> CompleteAdapterInstallAsync(
        Guid applicationId, bool succeeded, string message, CancellationToken cancellationToken)
    {
        var current = await software.LoadUpdateStatusAsync(cancellationToken);
        if (current.State != "AwaitingAdapter" || current.ApplicationId != applicationId)
            throw new InvalidOperationException(
                "There is no customer-adapter installation awaiting completion.");
        var desired = await software.LoadDesiredAsync(cancellationToken)
            ?? throw new InvalidOperationException("Desired software is unavailable.");
        var release = desired.SoftwarePlatforms.SelectMany(item => item.Applications)
            .Single(item => item.Id == applicationId).LatestRelease!;
        if (succeeded) await RecordInstalled(applicationId, release, cancellationToken);
        return await SaveStatus(
            current.AttemptId, applicationId, release.Id,
            succeeded ? "Completed" : "Failed", 100,
            string.IsNullOrWhiteSpace(message)
                ? succeeded ? $"Application {release.Version} installed." : "Installation failed."
                : message.Trim(), cancellationToken);
    }

    private Task RecordInstalled(
        Guid applicationId, DesiredRelease release, CancellationToken cancellationToken) =>
        software.SaveObservationAsync(
            new ApplicationObservation(
                applicationId, release.Id, release.Version, "AgentInstalled",
                DateTimeOffset.UtcNow), cancellationToken);

    public async Task<UpdateStatus> RecoverInterruptedAsync(CancellationToken cancellationToken)
    {
        var current = await software.LoadUpdateStatusAsync(cancellationToken);
        if (current.State is not ("Downloading" or "Verified" or "Installing")) return current;
        return await SaveStatus(
            current.AttemptId == Guid.Empty ? Guid.NewGuid() : current.AttemptId,
            current.ApplicationId, current.SoftwareReleaseId, "Failed",
            current.ProgressPercent,
            "The Agent restarted before the update completed. Start the update again.",
            cancellationToken);
    }

    private async Task<UpdateStatus> SaveStatus(
        Guid attemptId, Guid? applicationId, Guid? releaseId, string state, int progress,
        string message, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var status = new UpdateStatus(
            attemptId, applicationId, releaseId, state, progress, message, now);
        await software.SaveUpdateStatusAsync(status, cancellationToken);
        if (applicationId.HasValue && releaseId.HasValue)
        {
            var existing = (await software.LoadUpdateAttemptsAsync(cancellationToken))
                .SingleOrDefault(item => item.Id == attemptId);
            await software.SaveUpdateAttemptAsync(new UpdateAttempt(
                attemptId, applicationId.Value, releaseId.Value, state, progress, message,
                existing?.StartedAt ?? now, now,
                state is "Completed" or "Failed" or "RecoveryRequired" ? now : null),
                cancellationToken);
        }
        await notifier.NotifyAsync(
            state == "Completed" ? "inventory" : "update",
            cancellationToken);
        return status;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
