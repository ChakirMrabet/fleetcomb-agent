using FleetComb.Agent.Domain;
using FleetComb.Agent.Infrastructure.Updates;
using Xunit;

namespace FleetComb.Agent.ArchitectureTests;

public sealed class ReleaseArtifactValidatorTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), $"fleetcomb-artifact-tests-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("Exe", "setup.exe", new byte[] { 0x4d, 0x5a, 0, 0 })]
    [InlineData("Deb", "setup.deb", new byte[] { 0x21, 0x3c, 0x61, 0x72, 0x63, 0x68, 0x3e, 0x0a })]
    [InlineData("Pkg", "setup.pkg", new byte[] { 0x78, 0x61, 0x72, 0x21 })]
    [InlineData("Zip", "setup.zip", new byte[] { 0x50, 0x4b, 0x03, 0x04 })]
    public async Task Accepts_matching_extension_and_header(
        string packageType, string fileName, byte[] header)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(path, header);

        await new ReleaseArtifactValidator().ValidateAsync(
            Release(packageType, fileName), path, CancellationToken.None);
    }

    [Fact]
    public async Task Rejects_package_type_disguised_by_filename()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "archive.deb");
        await File.WriteAllBytesAsync(path, [0x50, 0x4b, 0x03, 0x04]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ReleaseArtifactValidator().ValidateAsync(
                Release("Deb", "archive.deb"), path, CancellationToken.None));

        Assert.Contains("not a recognizable Deb", exception.Message);
    }

    [Fact]
    public async Task Rejects_declared_type_extension_mismatch()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "archive.zip");
        await File.WriteAllBytesAsync(path, [0x50, 0x4b, 0x03, 0x04]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ReleaseArtifactValidator().ValidateAsync(
                Release("Deb", "archive.zip"), path, CancellationToken.None));

        Assert.Contains("requires a .deb", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private static DesiredRelease Release(string packageType, string fileName) =>
        new(Guid.NewGuid(), "1.0", packageType, fileName, 4, "checksum", "signature");
}
