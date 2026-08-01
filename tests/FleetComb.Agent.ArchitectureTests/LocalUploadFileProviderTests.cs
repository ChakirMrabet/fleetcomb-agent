using FleetComb.Agent.Domain;
using FleetComb.Agent.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using FleetComb.Agent.Application.Abstractions;
using Xunit;

namespace FleetComb.Agent.ArchitectureTests;

public sealed class LocalUploadFileProviderTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), $"fleetcomb-upload-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task InspectsAndReadsFileInsideAllowlist()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "scan.bin");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4, 5]);
        var provider = Provider(directory);

        var file = await provider.InspectAsync(path, CancellationToken.None);
        var secondChunk = await provider.ReadChunkAsync(file, 1, 3, CancellationToken.None);

        Assert.Equal("scan.bin", file.FileName);
        Assert.Equal(5, file.Length);
        Assert.Equal([4, 5], secondChunk);
    }

    [Fact]
    public async Task RejectsFileOutsideAllowlist()
    {
        Directory.CreateDirectory(directory);
        var outside = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(outside, [1]);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                Provider(directory).InspectAsync(outside, CancellationToken.None));
        }
        finally { File.Delete(outside); }
    }

    [Fact]
    public async Task DetectsFileChangedAfterInspection()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "project.bin");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        var provider = Provider(directory);
        LocalUploadFile file = await provider.InspectAsync(path, CancellationToken.None);
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);

        await Assert.ThrowsAsync<IOException>(() =>
            provider.ReadChunkAsync(file, 0, 4, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private LocalUploadFileProvider Provider(string root)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AgentUploads:AllowedRoots:0"] = root }).Build();
        return new LocalUploadFileProvider(configuration, new RegistrationStore(directory));
    }

    private sealed class RegistrationStore(string dataDirectory) : IAgentRegistrationStore
    {
        public string DataDirectory => dataDirectory;
        public Task EnsureWritableAsync(CancellationToken token) => Task.CompletedTask;
        public Task<AgentRegistration?> LoadAsync(CancellationToken token) => Task.FromResult<AgentRegistration?>(null);
        public Task SaveAsync(AgentRegistration registration, CancellationToken token) => Task.CompletedTask;
    }
}
