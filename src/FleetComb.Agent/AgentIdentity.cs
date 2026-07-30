using System.Security.Cryptography;
using System.Text;

namespace FleetComb.Agent;

public static class AgentIdentity
{
    public static (string PublicKey, string PrivateKey) Create()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (
            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            Convert.ToBase64String(key.ExportPkcs8PrivateKey()));
    }

    public static string Sign(string privateKey, string payload)
    {
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
        return Convert.ToBase64String(key.SignData(
            Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256));
    }

    public static string SignaturePayload(
        Guid installationId, long timestamp, string nonce, string bodyHash) =>
        $"{installationId:D}\n{timestamp}\n{nonce}\n{bodyHash.ToLowerInvariant()}";
}
