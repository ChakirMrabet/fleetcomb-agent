using System.Runtime.InteropServices;

namespace FleetComb.Agent;

public sealed record PlatformInformation(
    string Hostname, string OsFamily, string OsVersion, string Architecture)
{
    public static PlatformInformation Current() => new(
        Environment.MachineName,
        OperatingSystem.IsWindows() ? "Windows" :
        OperatingSystem.IsLinux() ? "Linux" :
        OperatingSystem.IsMacOS() ? "MacOS" : "Unknown",
        RuntimeInformation.OSDescription,
        RuntimeInformation.OSArchitecture.ToString());
}
