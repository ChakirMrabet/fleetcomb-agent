using System.Security.Cryptography;
using System.Text.Json;
using FleetComb.Agent.Application.Abstractions;

namespace FleetComb.Agent.Infrastructure.Persistence;

public sealed class FileLocalAdministratorStore(IAgentRegistrationStore registrations)
    : ILocalAdministratorStore
{
    private const int Iterations = 210_000;
    private readonly string path = Path.Combine(
        registrations.DataDirectory, "local-administrator.json");

    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(path));

    public async Task SetPasswordAsync(string password, CancellationToken cancellationToken)
    {
        if (password.Length < 12)
            throw new ArgumentException(
                "The local administrator password must contain at least 12 characters.",
                nameof(password));
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        Directory.CreateDirectory(registrations.DataDirectory);
        await using (var stream = new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(
                stream,
                new PasswordRecord(
                    Convert.ToBase64String(salt), Convert.ToBase64String(hash), Iterations),
                cancellationToken: cancellationToken);
        FileAgentRegistrationStore.Secure(path);
    }

    public async Task<bool> VerifyPasswordAsync(
        string password, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return false;
        await using var stream = File.OpenRead(path);
        var record = await JsonSerializer.DeserializeAsync<PasswordRecord>(
            stream, cancellationToken: cancellationToken);
        if (record is null) return false;
        var salt = Convert.FromBase64String(record.Salt);
        var expected = Convert.FromBase64String(record.Hash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, record.Iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private sealed record PasswordRecord(string Salt, string Hash, int Iterations);
}
