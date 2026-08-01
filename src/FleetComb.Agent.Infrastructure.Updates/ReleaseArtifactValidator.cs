using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Infrastructure.Updates;

public sealed class ReleaseArtifactValidator : IReleaseArtifactValidator
{
    private static readonly IReadOnlyDictionary<string, string> Extensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Exe"] = ".exe",
            ["Deb"] = ".deb",
            ["Pkg"] = ".pkg",
            ["Zip"] = ".zip"
        };

    public async Task ValidateAsync(
        DesiredRelease release,
        string artifactPath,
        CancellationToken cancellationToken)
    {
        if (!Extensions.TryGetValue(release.PackageType, out var requiredExtension))
            throw new InvalidOperationException(
                $"Package type '{release.PackageType}' is not supported.");

        var declaredExtension = Path.GetExtension(release.FileName);
        var downloadedExtension = Path.GetExtension(artifactPath);
        if (!declaredExtension.Equals(requiredExtension, StringComparison.OrdinalIgnoreCase) ||
            !downloadedExtension.Equals(requiredExtension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Package type {release.PackageType} requires a {requiredExtension} artifact.");

        await using var stream = new FileStream(
            artifactPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var header = new byte[8];
        var read = await stream.ReadAsync(header, cancellationToken);
        if (!HasExpectedHeader(release.PackageType, header.AsSpan(0, read)))
            throw new InvalidOperationException(
                $"The downloaded file is not a recognizable {release.PackageType} package.");
    }

    private static bool HasExpectedHeader(string packageType, ReadOnlySpan<byte> header) =>
        packageType switch
        {
            "Exe" => header.StartsWith("MZ"u8),
            "Deb" => header.StartsWith("!<arch>\n"u8),
            "Pkg" => header.StartsWith("xar!"u8),
            "Zip" =>
                header.StartsWith("PK\u0003\u0004"u8) ||
                header.StartsWith("PK\u0005\u0006"u8) ||
                header.StartsWith("PK\u0007\u0008"u8),
            _ => false
        };
}
