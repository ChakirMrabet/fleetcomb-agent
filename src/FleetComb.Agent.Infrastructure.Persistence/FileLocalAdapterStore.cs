using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Infrastructure.Persistence;

public sealed class FileLocalAdapterStore(IAgentRegistrationStore registrations)
    : ILocalAdapterStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<IReadOnlyList<LocalAdapterIdentity>> LoadAsync(
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await ReadCoreAsync(cancellationToken); }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(
        LocalAdapterIdentity identity, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadCoreAsync(cancellationToken);
            var updated = current.Where(item => item.Id != identity.Id)
                .Append(identity).OrderBy(item => item.Name).ToArray();
            Directory.CreateDirectory(registrations.DataDirectory);
            var path = Path.Combine(registrations.DataDirectory, "local-adapters.json");
            await using var stream = new FileStream(
                path, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, updated, JsonOptions, cancellationToken);
            FileAgentRegistrationStore.Secure(path);
        }
        finally { gate.Release(); }
    }

    public async Task<LocalAdapterIdentity?> FindByTokenHashAsync(
        string tokenHash, CancellationToken cancellationToken)
    {
        var identities = await LoadAsync(cancellationToken);
        var supplied = Encoding.UTF8.GetBytes(tokenHash);
        return identities.FirstOrDefault(identity =>
        {
            var expected = Encoding.UTF8.GetBytes(identity.TokenHash);
            return identity.RevokedAt is null && supplied.Length == expected.Length &&
                CryptographicOperations.FixedTimeEquals(supplied, expected);
        });
    }

    private async Task<LocalAdapterIdentity[]> ReadCoreAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(registrations.DataDirectory, "local-adapters.json");
        if (!File.Exists(path)) return [];
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<LocalAdapterIdentity[]>(
            stream, JsonOptions, cancellationToken) ?? [];
    }
}
