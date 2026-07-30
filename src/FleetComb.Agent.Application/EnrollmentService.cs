using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application;

public sealed class EnrollmentService(
    IAgentCloudClient cloud,
    IAgentRegistrationStore registrations,
    IAgentIdentityProvider identities,
    IPlatformInformationProvider platform)
{
    public async Task<EnrollmentResult> EnrollAsync(
        Uri serverUrl, string enrollmentCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(enrollmentCode))
            throw new ArgumentException("Enrollment code is required.", nameof(enrollmentCode));
        await registrations.EnsureWritableAsync(cancellationToken);
        var (publicKey, privateKey) = identities.Create();
        var claim = await cloud.ClaimAsync(
            serverUrl, enrollmentCode.Trim(), publicKey, platform.Current(), cancellationToken);
        var registration = new AgentRegistration(
            serverUrl, claim.TenantId, claim.AssetId, claim.InstallationId, privateKey,
            claim.HeartbeatIntervalSeconds, identities.CreateLocalApiToken());
        await registrations.SaveAsync(registration, cancellationToken);
        return new EnrollmentResult(
            registration.AssetId, registrations.DataDirectory, registration.LocalApiToken);
    }

    public async Task<string> GetOrCreateLocalApiTokenAsync(
        CancellationToken cancellationToken)
    {
        var registration = await registrations.LoadAsync(cancellationToken)
            ?? throw new InvalidOperationException("The Agent is not enrolled.");
        if (!string.IsNullOrWhiteSpace(registration.LocalApiToken))
            return registration.LocalApiToken;
        registration = registration with { LocalApiToken = identities.CreateLocalApiToken() };
        await registrations.SaveAsync(registration, cancellationToken);
        return registration.LocalApiToken;
    }
}

public sealed record EnrollmentResult(Guid AssetId, string DataDirectory, string LocalApiToken);
