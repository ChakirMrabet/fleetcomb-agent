using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Infrastructure.Cloud;

public sealed class AgentCloudClient(HttpClient httpClient) : IAgentCloudClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EnrollmentClaim> ClaimAsync(
        Uri serverUrl, string code, string publicKey, PlatformInformation platform,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            new Uri(serverUrl, "/agent/v1/enrollments/claim"),
            new ClaimRequest(
                code, publicKey, AgentVersion.Current, "1.0", platform.Hostname,
                platform.OsFamily, platform.OsVersion, platform.Architecture),
            JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, "enrollment", cancellationToken);
        var claim = await response.Content.ReadFromJsonAsync<ClaimResponse>(
            JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("FleetComb returned an empty enrollment response.");
        return new EnrollmentClaim(
            claim.TenantId, claim.AssetId, claim.AgentInstallationId,
            claim.HeartbeatIntervalSeconds, claim.ServerTime);
    }

    public async Task<HeartbeatResult> HeartbeatAsync(
        AgentRegistration state, long uptimeSeconds,
        IReadOnlyList<ApplicationObservation> applications,
        IReadOnlyList<ProducerMessage> producerMessages,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(
            new HeartbeatRequest(
                AgentVersion.Current, "1.0", uptimeSeconds, "Healthy", applications,
                producerMessages.Select(message => new ProducerMessageRequest(
                    message.Id, message.AdapterId, message.Sequence, message.Kind,
                    message.Schema, message.Severity,
                    JsonDocument.Parse(message.PayloadJson).RootElement.Clone(),
                    message.CreatedAt)).ToArray(), AgentCapabilities.All),
            JsonOptions);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var bodyHash = Convert.ToHexStringLower(SHA256.HashData(body));
        var payload = AgentIdentityProvider.SignaturePayload(
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
        request.Headers.Add(
            "X-FleetComb-Signature", AgentIdentityProvider.Sign(state.PrivateKey, payload));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "heartbeat", cancellationToken);
        var heartbeat = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(
            JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("FleetComb returned an empty heartbeat response.");
        return new HeartbeatResult(
            heartbeat.ServerTime, heartbeat.NextHeartbeatSeconds, heartbeat.DesiredState,
            heartbeat.AcceptedProducerMessageIds ?? []);
    }

    public async Task DownloadReleaseAsync(
        AgentRegistration state, DesiredRelease release, string destination,
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

    public async Task<CloudUploadSession> CreateFileUploadAsync(
        AgentRegistration state, FileUploadSession upload, CancellationToken token)
    {
        using var metadata = JsonDocument.Parse(upload.MetadataJson);
        var body = JsonSerializer.SerializeToUtf8Bytes(new FileUploadCreateRequest(
            upload.Id, upload.AdapterId, upload.Category, upload.Schema, upload.FileName,
            upload.ContentType, upload.Length, upload.Sha256, upload.ChunkSize,
            upload.ChunkCount, metadata.RootElement.Clone(), upload.CapturedAt), JsonOptions);
        using var request = SignedRequest(state, "/agent/v1/uploads", body);
        using var response = await httpClient.SendAsync(request, token);
        await EnsureSuccessAsync(response, "file upload creation", token);
        return await response.Content.ReadFromJsonAsync<CloudUploadSession>(JsonOptions, token)
            ?? throw new InvalidOperationException("FleetComb returned an empty upload session.");
    }

    public async Task UploadFileChunkAsync(
        AgentRegistration state, Guid uploadId, int index, byte[] content,
        CancellationToken token)
    {
        using var request = SignedRequest(
            state, $"/agent/v1/uploads/{uploadId:D}/chunks/{index}", content, HttpMethod.Put);
        request.Content!.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        using var response = await httpClient.SendAsync(request, token);
        await EnsureSuccessAsync(response, $"file upload chunk {index}", token);
    }

    public Task CompleteFileUploadAsync(
        AgentRegistration state, Guid uploadId, CancellationToken token) =>
        SendUploadActionAsync(state, uploadId, "complete", token);

    public Task CancelFileUploadAsync(
        AgentRegistration state, Guid uploadId, CancellationToken token) =>
        SendUploadActionAsync(state, uploadId, "cancel", token);

    private async Task SendUploadActionAsync(
        AgentRegistration state, Guid uploadId, string action, CancellationToken token)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new { uploadId }, JsonOptions);
        using var request = SignedRequest(
            state, $"/agent/v1/uploads/{uploadId:D}/{action}", body);
        using var response = await httpClient.SendAsync(request, token);
        await EnsureSuccessAsync(response, $"file upload {action}", token);
    }

    private static HttpRequestMessage SignedRequest(
        AgentRegistration state, string path, byte[] body, HttpMethod? method = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var bodyHash = Convert.ToHexStringLower(SHA256.HashData(body));
        var payload = AgentIdentityProvider.SignaturePayload(
            state.InstallationId, timestamp, nonce, bodyHash);
        var request = new HttpRequestMessage(method ?? HttpMethod.Post, new Uri(state.ServerUrl, path))
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
            "X-FleetComb-Signature", AgentIdentityProvider.Sign(state.PrivateKey, payload));
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
        IReadOnlyList<ApplicationObservation> Applications,
        IReadOnlyList<ProducerMessageRequest> ProducerMessages,
        IReadOnlyList<string> Capabilities);
    private sealed record ProducerMessageRequest(
        Guid Id, Guid AdapterId, long Sequence, string Kind, string Schema,
        string Severity, JsonElement Payload, DateTimeOffset CreatedAt);
    private sealed record ArtifactRequest(Guid SoftwareReleaseId);
    private sealed record FileUploadCreateRequest(
        Guid UploadId, Guid AdapterId, string Category, string Schema, string FileName,
        string ContentType, long Length, string Sha256, int ChunkSize, int ChunkCount,
        JsonElement Metadata, DateTimeOffset CapturedAt);
    public sealed record HeartbeatResponse(
        DateTimeOffset ServerTime, int NextHeartbeatSeconds, DesiredState DesiredState,
        IReadOnlyList<Guid>? AcceptedProducerMessageIds);
}

public static class AgentVersion
{
    public const string Current = "0.2.0";
}
