using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace FleetComb.Agent;

public sealed class AgentApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ClaimResponse> ClaimAsync(
        Uri serverUrl, string code, string publicKey, CancellationToken cancellationToken)
    {
        var platform = PlatformInformation.Current();
        using var response = await httpClient.PostAsJsonAsync(
            new Uri(serverUrl, "/agent/v1/enrollments/claim"),
            new ClaimRequest(
                code, publicKey, AgentVersion.Current, "1.0", platform.Hostname,
                platform.OsFamily, platform.OsVersion, platform.Architecture),
            JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, "enrollment", cancellationToken);
        return await response.Content.ReadFromJsonAsync<ClaimResponse>(
            JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("FleetComb returned an empty enrollment response.");
    }

    public async Task<HeartbeatResponse> HeartbeatAsync(
        AgentState state, long uptimeSeconds,
        IReadOnlyList<ApplicationObservation> applications,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(
            new HeartbeatRequest(
                AgentVersion.Current, "1.0", uptimeSeconds, "Healthy", applications),
            JsonOptions);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var bodyHash = Convert.ToHexStringLower(SHA256.HashData(body));
        var payload = AgentIdentity.SignaturePayload(
            state.InstallationId, timestamp, nonce, bodyHash);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri(state.ServerUrl, "/agent/v1/heartbeat"))
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-FleetComb-Tenant", state.TenantId.ToString("D"));
        request.Headers.Add("X-FleetComb-Installation", state.InstallationId.ToString("D"));
        request.Headers.Add("X-FleetComb-Timestamp", timestamp.ToString());
        request.Headers.Add("X-FleetComb-Nonce", nonce);
        request.Headers.Add("X-FleetComb-Signature", AgentIdentity.Sign(state.PrivateKey, payload));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "heartbeat", cancellationToken);
        return await response.Content.ReadFromJsonAsync<HeartbeatResponse>(
            JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("FleetComb returned an empty heartbeat response.");
    }

    public async Task DownloadReleaseAsync(
        AgentState state, DesiredRelease release, string destination,
        IProgress<int> progress, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(
            new ArtifactRequest(release.Id), JsonOptions);
        using var request = SignedRequest(
            state, "/agent/v1/releases/artifact", body);
        using var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, "artifact download", cancellationToken);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(
            destination, FileMode.Create, FileAccess.Write, FileShare.None);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        long copied = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) != 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hash.AppendData(buffer, 0, read);
            copied += read;
            progress.Report(release.Length == 0
                ? 0
                : (int)Math.Min(100, copied * 100 / release.Length));
        }
        var actualHash = Convert.ToHexStringLower(hash.GetHashAndReset());
        if (copied != release.Length ||
            !actualHash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "The downloaded artifact length or SHA-256 checksum does not match the release.");
    }

    private static HttpRequestMessage SignedRequest(
        AgentState state, string path, byte[] body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var bodyHash = Convert.ToHexStringLower(SHA256.HashData(body));
        var payload = AgentIdentity.SignaturePayload(
            state.InstallationId, timestamp, nonce, bodyHash);
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(state.ServerUrl, path))
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-FleetComb-Tenant", state.TenantId.ToString("D"));
        request.Headers.Add("X-FleetComb-Installation", state.InstallationId.ToString("D"));
        request.Headers.Add(
            "X-FleetComb-Timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.Add("X-FleetComb-Nonce", nonce);
        request.Headers.Add(
            "X-FleetComb-Signature", AgentIdentity.Sign(state.PrivateKey, payload));
        return request;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"FleetComb {operation} failed with {(int)response.StatusCode} " +
            $"({response.ReasonPhrase}).{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}")}",
            null,
            response.StatusCode);
    }

    private sealed record ClaimRequest(
        string EnrollmentCode, string PublicKey, string AgentVersion, string ProtocolVersion,
        string Hostname, string OsFamily, string OsVersion, string Architecture);
    public sealed record ClaimResponse(
        Guid TenantId, Guid AssetId, Guid AgentInstallationId,
        int HeartbeatIntervalSeconds, DateTimeOffset ServerTime);
    private sealed record HeartbeatRequest(
        string AgentVersion, string ProtocolVersion, long UptimeSeconds, string Health,
        IReadOnlyList<ApplicationObservation> Applications);
    private sealed record ArtifactRequest(Guid SoftwareReleaseId);
    public sealed record HeartbeatResponse(
        DateTimeOffset ServerTime, int NextHeartbeatSeconds, DesiredState DesiredState);
}

public static class AgentVersion
{
    public const string Current = "0.1.0";
}
