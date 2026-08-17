namespace FleetComb.Agent.Domain;

public sealed record DesiredState(
    Guid AssetId,
    long Revision,
    DesiredProduct? Product,
    IReadOnlyList<DesiredSoftwarePlatform> SoftwarePlatforms,
    DesiredAuthorizationRoster? Authorization = null);

public sealed record DesiredAuthorizationRoster(
    int SchemaVersion,
    long Revision,
    DateTimeOffset GeneratedAt,
    DateTimeOffset LeaseExpiresAt,
    string AssetSerialNumber,
    IReadOnlyList<DesiredAuthorizedUser> Users);

public sealed record DesiredAuthorizedUser(
    Guid MembershipId,
    string Username,
    string DisplayName,
    DateTimeOffset NotAfter);

public static class AgentCapabilities
{
    public const string AuthorizedUsersV1 = "authorized-users.v1";

    public static readonly IReadOnlyList<string> All = [AuthorizedUsersV1];
}

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
    Guid AttemptId,
    Guid? ApplicationId,
    Guid? SoftwareReleaseId,
    string State,
    int ProgressPercent,
    string Message,
    DateTimeOffset UpdatedAt)
{
    public static UpdateStatus Idle() =>
        new(Guid.Empty, null, null, "Idle", 0, "No update is running.", DateTimeOffset.UtcNow);
}

public sealed record UpdateAttempt(
    Guid Id,
    Guid ApplicationId,
    Guid SoftwareReleaseId,
    string State,
    int ProgressPercent,
    string Message,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record SynchronizationStatus(
    string State,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? LastSuccessfulAt,
    DateTimeOffset? NextRetryAt,
    string LastError)
{
    public static SynchronizationStatus NotEnrolled() =>
        new("NotEnrolled", null, null, null, "");
}

public sealed record CustomerAdapterStatus(
    string State,
    string Name,
    string Version,
    IReadOnlyList<string> Capabilities,
    DateTimeOffset? LastSeenAt)
{
    public static CustomerAdapterStatus NotConnected() =>
        new("NotConnected", "", "", [], null);
}
