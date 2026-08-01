namespace FleetComb.Agent.Domain;

public sealed record FileUploadSession(
    Guid Id, Guid AdapterId, string LocalPath, string Category, string Schema,
    string FileName, string ContentType, long Length, string Sha256, int ChunkSize,
    int ChunkCount, string MetadataJson, DateTimeOffset CapturedAt,
    DateTimeOffset FileLastWriteAt, string State, int UploadedChunks,
    int ProgressPercent, string Error, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    DateTimeOffset? NextRetryAt, bool CancellationRequested);

public sealed record LocalUploadFile(
    string FullPath, string FileName, long Length, string Sha256,
    DateTimeOffset LastWriteAt);

public sealed record CloudUploadSession(
    Guid UploadId, string Status, IReadOnlyList<int> UploadedChunks);
