namespace FleetComb.Agent;

public sealed record DesiredState(
    Guid AssetId,
    long Revision,
    DesiredProduct? Product,
    IReadOnlyList<DesiredSoftwarePlatform> SoftwarePlatforms);

public sealed record DesiredProduct(Guid Id, string PartNumber, string Name);

public sealed record DesiredSoftwarePlatform(
    Guid Id,
    string PartNumber,
    string Name,
    IReadOnlyList<DesiredApplication> Applications);

public sealed record DesiredApplication(
    Guid Id,
    string PartNumber,
    string Name,
    DesiredRelease? LatestRelease);

public sealed record DesiredRelease(
    Guid Id,
    string Version,
    string PackageType,
    string FileName,
    long Length,
    string Sha256,
    string Signature);

public sealed record ApplicationObservation(
    Guid ApplicationId,
    Guid? SoftwareReleaseId,
    string InstalledVersion,
    string Source,
    DateTimeOffset ObservedAt);

public sealed record UpdateStatus(
    Guid? ApplicationId,
    Guid? SoftwareReleaseId,
    string State,
    int ProgressPercent,
    string Message,
    DateTimeOffset UpdatedAt)
{
    public static UpdateStatus Idle() =>
        new(null, null, "Idle", 0, "No update is running.", DateTimeOffset.UtcNow);
}

public interface ISoftwareStateStore
{
    Task<DesiredState?> LoadDesiredAsync(CancellationToken cancellationToken);
    Task SaveDesiredAsync(DesiredState desired, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationObservation>> LoadInventoryAsync(
        CancellationToken cancellationToken);
    Task SaveObservationAsync(
        ApplicationObservation observation, CancellationToken cancellationToken);
    Task<UpdateStatus> LoadUpdateStatusAsync(CancellationToken cancellationToken);
    Task SaveUpdateStatusAsync(UpdateStatus status, CancellationToken cancellationToken);
}
