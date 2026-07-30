using System.Security.Cryptography;
using System.Text;
using FleetComb.Agent.Application.Abstractions;

namespace FleetComb.Agent.Infrastructure.Cloud;

public sealed class AgentIdentityProvider : IAgentIdentityProvider
{
    public (string PublicKey, string PrivateKey) Create()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (
            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            Convert.ToBase64String(key.ExportPkcs8PrivateKey()));
    }

    public string CreateLocalApiToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static string Sign(string privateKey, string payload)
    {
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
        return Convert.ToBase64String(key.SignData(
            Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256));
    }

    internal static string SignaturePayload(
        Guid installationId, long timestamp, string nonce, string bodyHash) =>
        $"{installationId:D}\n{timestamp}\n{nonce}\n{bodyHash.ToLowerInvariant()}";
}
