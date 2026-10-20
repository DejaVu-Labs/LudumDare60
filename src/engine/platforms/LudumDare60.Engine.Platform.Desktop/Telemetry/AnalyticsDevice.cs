using System.Globalization;
using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// The device fields every GameAnalytics event carries.
/// </summary>
/// <param name="Platform"><c>windows</c>, <c>linux</c> or <c>mac_osx</c>.</param>
/// <param name="OsVersion">The platform and up to three version numbers, for example <c>windows 10.0.26100</c>.</param>
internal sealed record AnalyticsDevice(string Platform, string OsVersion)
{
    /// <summary>
    /// The device this process runs on.
    /// </summary>
    public static AnalyticsDevice Current
    {
        get
        {
            var platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "mac_osx" : "linux";
            return new AnalyticsDevice(platform, GetOsVersion(platform, Environment.OSVersion.Version));
        }
    }

    /// <summary>
    /// <paramref name="platform"/> and the first three parts of <paramref name="version"/>, each at most five digits,
    /// as GameAnalytics requires.
    /// </summary>
    public static string GetOsVersion(string platform, Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        Span<int> parts = [version.Major, Math.Max(version.Minor, 0), Math.Max(version.Build, 0)];
        var text = string.Create(CultureInfo.InvariantCulture, $"{platform} {clamp(parts[0])}.{clamp(parts[1])}.{clamp(parts[2])}");
        return text;

        static int clamp(int part) => Math.Clamp(part, 0, 99999);
    }

    /// <summary>
    /// The processor architecture, as the <c>device</c> field: desktops have no model name.
    /// </summary>
    public static string Device => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
}
