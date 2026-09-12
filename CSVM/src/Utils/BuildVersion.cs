using Godot;

namespace CSVM.Utils;

/// <summary>
/// The build's own version, read once from <c>application/config/version</c> in
/// <c>project.godot</c>, which is the number's one home. Three surfaces state it back so a report
/// names a build without being asked how it was obtained: the log's first line, the launchscreen's
/// bottom corner, and the exe's Windows file properties (stamped by the export preset, not from
/// here). <c>ExportRelease.ps1</c> reads the same key to name the zip.
/// ⚠ Reads <see cref="ProjectSettings"/>, so it resolves only inside a running engine, which is
/// why <see cref="Log.Open"/> takes the version as an argument rather than reading it itself.
/// </summary>
public static class BuildVersion
{
    /// <summary>What a build with no version key states. It says so rather than claiming a number
    /// that may not be its own.</summary>
    public const string Unknown = "unknown";

    private static string? _current;

    /// <summary>The version, <c>0.1.0</c> shaped and with no leading <c>v</c>; the surfaces that
    /// want one add it themselves.</summary>
    public static string Current => _current ??= Read();

    private static string Read()
    {
        string version = ProjectSettings.GetSetting("application/config/version").AsString().Trim();
        return version.Length > 0 ? version : Unknown;
    }
}
