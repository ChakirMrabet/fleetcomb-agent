using FleetComb.Agent.Application;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Application.Status.Queries;
using FleetComb.Agent.Domain;
using FleetComb.Agent.Infrastructure.Persistence;
using Xunit;

namespace FleetComb.Agent.ArchitectureTests;

public sealed class AuthorizationRosterTests
{
    [Fact]
    public async Task ExistingDesiredStateWithoutAuthorizationRemainsReadable()
    {
        var directory = NewDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "desired-state.json"), $$"""
                {
                  "assetId": "{{Guid.NewGuid()}}",
                  "revision": 1,
                  "product": null,
                  "softwarePlatforms": []
                }
                """);
            var store = new FileSoftwareStateStore(new RegistrationStore(directory));

            var desired = await store.LoadDesiredAsync(default);

            Assert.NotNull(desired);
            Assert.Null(desired.Authorization);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AuthorizedUserQuerySurvivesRestartAndFiltersExpiredEntries()
    {
        var directory = NewDirectory();
        try
        {
            var now = new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
            var validMembershipId = Guid.NewGuid();
            var store = new FileSoftwareStateStore(new RegistrationStore(directory));
            await store.SaveDesiredAsync(new DesiredState(
                Guid.NewGuid(), 3, null, [], new DesiredAuthorizationRoster(
                    1, 2, now, now.AddDays(30), "SN-100",
                    [
                        new DesiredAuthorizedUser(
                            validMembershipId, "valid.user", "Valid User", now.AddDays(1)),
                        new DesiredAuthorizedUser(
                            Guid.NewGuid(), "expired.user", "Expired User", now.AddSeconds(-1))
                    ])), default);
            var restartedStore = new FileSoftwareStateStore(new RegistrationStore(directory));
            var adapters = new CustomerAdapterService(
                restartedStore, new AdapterStore(), new Notifier());
            var status = new AgentStatusService(
                new RegistrationStore(directory), restartedStore, adapters);
            var handler = new GetAuthorizedUsers.Handler(status, new FixedTimeProvider(now));

            var result = await handler.Handle(new GetAuthorizedUsers.Query(), default);

            Assert.NotNull(result);
            Assert.Equal("SN-100", result.AssetSerialNumber);
            Assert.Equal(validMembershipId, Assert.Single(result.Users).MembershipId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExpirationMonitorInvalidatesRosterWithoutCloudSynchronization()
    {
        var directory = NewDirectory();
        try
        {
            var now = DateTimeOffset.UtcNow;
            var store = new FileSoftwareStateStore(new RegistrationStore(directory));
            await store.SaveDesiredAsync(new DesiredState(
                Guid.NewGuid(), 1, null, [], new DesiredAuthorizationRoster(
                    1, 1, now, now.AddDays(30), "SN-100",
                    [new DesiredAuthorizedUser(
                        Guid.NewGuid(), "expiring.user", "Expiring User", now.AddMilliseconds(100))])),
                default);
            var notifier = new RecordingNotifier();
            var service = new AuthorizationRosterExpirationService(
                store, notifier, TimeProvider.System);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            var running = service.RunAsync(timeout.Token);
            var change = await notifier.Notified.Task.WaitAsync(timeout.Token);
            await timeout.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);

            Assert.Equal("authorized-users", change);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string NewDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), $"fleetcomb-agent-authorization-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Notifier : IAgentStatusNotifier
    {
        public Task NotifyAsync(string change, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingNotifier : IAgentStatusNotifier
    {
        public TaskCompletionSource<string> Notified { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task NotifyAsync(string change, CancellationToken cancellationToken)
        {
            Notified.TrySetResult(change);
            return Task.CompletedTask;
        }
    }

    private sealed class AdapterStore : ILocalAdapterStore
    {
        public Task<IReadOnlyList<LocalAdapterIdentity>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LocalAdapterIdentity>>([]);

        public Task SaveAsync(LocalAdapterIdentity identity, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<LocalAdapterIdentity?> FindByTokenHashAsync(
            string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult<LocalAdapterIdentity?>(null);
    }

    private sealed class RegistrationStore(string directory) : IAgentRegistrationStore
    {
        public string DataDirectory => directory;

        public Task EnsureWritableAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<AgentRegistration?> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AgentRegistration?>(null);

        public Task SaveAsync(AgentRegistration registration, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
