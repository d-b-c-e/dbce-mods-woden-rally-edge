using BepInEx.Configuration;

namespace WodenRallyEdge;

/// <summary>
/// The developer opt-in (STD-050). Drive recording, stage playback, native observation, course dumps, the recording
/// indicator and the qualification runner's save suppression exist only with <c>[Dev] InternalTools = true</c>, or
/// <c>DBCE_INTERNAL_TOOLS=1</c> in a direct launch's environment (Steam does not pass it on). Off, the plugin never
/// loads <c>Dbce.Wheel.Recording</c> or <c>Dbce.Wheel.Playback</c>, and the player package ships neither. Read once per
/// launch, before settings; it is a UI and tooling switch, not security.
/// </summary>
internal static class InternalTools
{
    internal const string Variable = "DBCE_INTERNAL_TOOLS";
    internal static readonly string[] Assemblies = { "Dbce.Wheel.Recording.dll", "Dbce.Wheel.Playback.dll" };
    internal static bool Enabled { get; private set; }

    /// <summary>Returns why the tools stay off despite the opt-in, or null.</summary>
    internal static string? Bind(ConfigFile config, string pluginDirectory)
    {
        bool requested = Requested(config.Bind("Dev", "InternalTools", false,
            "Development only: recording, stage playback and other test tooling. Needs the developer build's Recording and Playback DLLs beside the plugin. " +
            Variable + "=1 in a direct launch's environment also opts in.").Value, Environment.GetEnvironmentVariable(Variable));
        string? missing = requested ? Assemblies.FirstOrDefault(name => !File.Exists(Path.Combine(pluginDirectory, name))) : null;
        Enabled = requested && missing == null;
        return missing == null ? null : "Internal tools stay off: " + missing + " is not beside the plugin (player package).";
    }

    internal static bool Requested(bool configValue, string? environmentValue) => configValue || environmentValue == "1";

    /// <summary>Test fixtures only.</summary>
    internal static void Set(bool enabled) => Enabled = enabled;
}
