using System.Security.Cryptography;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;
using Microsoft.Extensions.Configuration;

namespace FleetComb.Agent.Infrastructure.Persistence;

public sealed class LocalUploadFileProvider : ILocalUploadFileProvider
{
    private readonly string[] roots;
    public string InboxDirectory { get; }
    public IReadOnlyList<string> AllowedRoots => roots;

    public LocalUploadFileProvider(
        IConfiguration configuration, IAgentRegistrationStore registrations)
    {
        InboxDirectory = Path.Combine(registrations.DataDirectory, "upload-inbox");
        Directory.CreateDirectory(InboxDirectory);
        roots = configuration.GetSection("AgentUploads:AllowedRoots").Get<string[]>()?
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(Path.GetFullPath).ToArray()
            ?? [];
        if (roots.Length == 0) roots = [Path.GetFullPath(InboxDirectory)];
    }

    public async Task<LocalUploadFile> InspectAsync(string path, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var root = roots.FirstOrDefault(value => IsWithin(fullPath, value))
            ?? throw new UnauthorizedAccessException(
                "The file is outside the Agent upload allowlist.");
        RejectLinks(fullPath, root);
        var info = new FileInfo(fullPath);
        if (!info.Exists) throw new FileNotFoundException("The upload file does not exist.", fullPath);
        if (info.Length <= 0 || info.Length > 100L * 1024 * 1024 * 1024)
            throw new InvalidOperationException("Upload files must be between 1 byte and 100 GiB.");
        await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token));
        info.Refresh();
        return new LocalUploadFile(fullPath, info.Name, info.Length, hash, info.LastWriteTimeUtc);
    }

    public async Task<byte[]> ReadChunkAsync(
        LocalUploadFile file, int chunkIndex, int chunkSize, CancellationToken token)
    {
        var info = new FileInfo(file.FullPath);
        if (!info.Exists || info.Length != file.Length || info.LastWriteTimeUtc != file.LastWriteAt.UtcDateTime)
            throw new IOException("The upload file changed after the session was created.");
        var offset = (long)chunkIndex * chunkSize;
        var count = checked((int)Math.Min(chunkSize, file.Length - offset));
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(chunkIndex));
        var bytes = new byte[count];
        await using var stream = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.RandomAccess);
        stream.Position = offset;
        await stream.ReadExactlyAsync(bytes, token);
        return bytes;
    }

    private static bool IsWithin(string path, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static void RejectLinks(string path, string root)
    {
        for (var current = new FileInfo(path) as FileSystemInfo;
             current is not null && IsWithin(current.FullName, root);
             current = current switch { FileInfo file => file.Directory, DirectoryInfo dir => dir.Parent,
                 _ => null })
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Symbolic links are not accepted for uploads.");
    }
}
