using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;
using Microsoft.Extensions.Logging;

namespace FleetComb.Agent.Application;

public sealed class FileUploadService(
    IAgentRegistrationStore registrations, IFileUploadStore uploads,
    ILocalUploadFileProvider files, IAgentCloudClient cloud, IAgentStatusNotifier notifier,
    ILogger<FileUploadService> logger)
{
    public const int ChunkSize = 4 * 1024 * 1024;
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task<IReadOnlyList<FileUploadSession>> ListAsync(CancellationToken token) =>
        uploads.LoadAsync(token);

    public Task<FileUploadSession?> GetAsync(Guid id, CancellationToken token) =>
        uploads.GetAsync(id, token);

    public async Task<FileUploadSession> CreateAsync(
        Guid adapterId, string localPath, string category, string schema, string contentType,
        string metadataJson, DateTimeOffset? capturedAt, CancellationToken token)
    {
        if (adapterId == Guid.Empty) throw new UnauthorizedAccessException(
            "A scoped adapter credential is required.");
        if (category is not ("scan" or "project" or "diagnostic" or "other"))
            throw new ArgumentException("Category must be scan, project, diagnostic, or other.");
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (schema.Length > 200 || contentType.Length > 200 || metadataJson.Length > 64 * 1024)
            throw new ArgumentException("Upload metadata exceeds its allowed length.");
        var file = await files.InspectAsync(localPath, token);
        var now = DateTimeOffset.UtcNow;
        var chunkCount = checked((int)((file.Length + ChunkSize - 1) / ChunkSize));
        var session = new FileUploadSession(
            Guid.NewGuid(), adapterId, file.FullPath, category, schema.Trim(), file.FileName,
            contentType.Trim(), file.Length, file.Sha256, ChunkSize, chunkCount, metadataJson,
            capturedAt ?? now, file.LastWriteAt, "Pending", 0, 0, "", now, now, now, false);
        await uploads.SaveAsync(session, token);
        await notifier.NotifyAsync("upload", token);
        return session;
    }

    public async Task<bool> CancelAsync(Guid id, Guid adapterId, CancellationToken token)
    {
        var current = await uploads.GetAsync(id, token);
        if (current is null || current.AdapterId != adapterId ||
            current.State is "Completed" or "Cancelled") return false;
        await uploads.SaveAsync(current with
        {
            CancellationRequested = true,
            State = "Pending",
            NextRetryAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        }, token);
        return true;
    }

    public async Task<bool> RetryAsync(Guid id, Guid adapterId, CancellationToken token)
    {
        var current = await uploads.GetAsync(id, token);
        if (current is null || current.AdapterId != adapterId || current.State != "Failed")
            return false;
        await uploads.SaveAsync(current with
        {
            State = "Pending", Error = "", NextRetryAt = DateTimeOffset.UtcNow,
            CancellationRequested = false, UpdatedAt = DateTimeOffset.UtcNow
        }, token);
        return true;
    }

    public async Task ProcessPendingAsync(CancellationToken token)
    {
        if (!await gate.WaitAsync(0, token)) return;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var upload = (await uploads.LoadAsync(token)).Where(item =>
                    item.State is "Pending" or "Uploading" ||
                    item.State == "Failed" && item.NextRetryAt.HasValue && item.NextRetryAt <= now)
                .OrderBy(item => item.CreatedAt).FirstOrDefault();
            if (upload is null) return;
            await ProcessAsync(upload, token);
        }
        finally { gate.Release(); }
    }

    private async Task ProcessAsync(FileUploadSession upload, CancellationToken token)
    {
        var registration = await registrations.LoadAsync(token);
        if (registration is null) return;
        try
        {
            if (upload.CancellationRequested)
            {
                await cloud.CreateFileUploadAsync(registration, upload, token);
                await cloud.CancelFileUploadAsync(registration, upload.Id, token);
                await Save(upload with { State = "Cancelled", Error = "", NextRetryAt = null }, token);
                return;
            }
            var file = await files.InspectAsync(upload.LocalPath, token);
            if (file.Length != upload.Length || file.Sha256 != upload.Sha256 ||
                file.LastWriteAt != upload.FileLastWriteAt)
                throw new InvalidDataException("The local file changed after upload creation.");
            upload = await Save(upload with { State = "Uploading", Error = "" }, token);
            var remote = await cloud.CreateFileUploadAsync(registration, upload, token);
            if (remote.Status == "Completed")
            {
                await Save(upload with { State = "Completed", UploadedChunks = upload.ChunkCount,
                    ProgressPercent = 100, NextRetryAt = null }, token);
                return;
            }
            var completed = remote.UploadedChunks.ToHashSet();
            for (var index = 0; index < upload.ChunkCount; index++)
            {
                var latest = await uploads.GetAsync(upload.Id, token) ?? upload;
                if (latest.CancellationRequested)
                {
                    await cloud.CancelFileUploadAsync(registration, upload.Id, token);
                    await Save(latest with { State = "Cancelled", Error = "", NextRetryAt = null }, token);
                    return;
                }
                if (!completed.Contains(index))
                    await cloud.UploadFileChunkAsync(registration, upload.Id, index,
                        await files.ReadChunkAsync(file, index, upload.ChunkSize, token), token);
                completed.Add(index);
                var count = completed.Count;
                upload = await Save(upload with { UploadedChunks = count,
                    ProgressPercent = (int)((long)count * 100 / upload.ChunkCount) }, token);
            }
            await cloud.CompleteFileUploadAsync(registration, upload.Id, token);
            await Save(upload with { State = "Completed", UploadedChunks = upload.ChunkCount,
                ProgressPercent = 100, Error = "", NextRetryAt = null }, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var retryable = exception is HttpRequestException;
            logger.LogError(exception, "File upload {UploadId} failed.", upload.Id);
            await Save(upload with { State = "Failed", Error = exception.Message,
                NextRetryAt = retryable ? DateTimeOffset.UtcNow.AddSeconds(15) : null },
                CancellationToken.None);
        }
    }

    private async Task<FileUploadSession> Save(FileUploadSession value, CancellationToken token)
    {
        var updated = value with { UpdatedAt = DateTimeOffset.UtcNow };
        await uploads.SaveAsync(updated, token);
        await notifier.NotifyAsync("upload", token);
        return updated;
    }
}
