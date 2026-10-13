using System.Text.Json;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// The stage bundle's signal writers, shared by the supervised stage tool and the ordinary full-session capture.
// Engine-free: the caller writes stage-context.json first (it reads live game state).
internal static class StageSignalFiles
{
    internal static readonly string[] Files = { "source.jsonl", "force-config.json", "channels.json", "stage-context.json" };
    internal sealed record Identity(string GameHash, string PluginPath, string RecordingSource, string PluginVersion, string SessionId);

    internal static TelemetryOutput Open(string directory, Identity id, ForceOptions options, bool physicalOutput)
    {
        RecordingArtifacts.WriteForceConfig(Path.Combine(directory, "force-config.json"), options);
        File.WriteAllText(Path.Combine(directory, "channels.json"), JsonSerializer.Serialize(TelemetrySchema.Channels));
        var properties = new Dictionary<string, string> { ["capability"] = "signal-reprocess", ["physicalOutput"] = physicalOutput ? "true" : "false", ["captureSource"] = "live-physics",
            ["gameAssemblySha256"] = id.GameHash, ["pluginSha256"] = RecordingArtifacts.Sha256(id.PluginPath),
            ["forceConfigSha256"] = RecordingArtifacts.Sha256(Path.Combine(directory, "force-config.json")),
            ["analysisForceContract"] = ForceAnalysisHistory.Contract, ["analysisForce"] = ForceAnalysisHistory.Scope,
            ["posePhase"] = "MainCar.FixedUpdate.prefix; signals from postfix before Unity solve",
            ["alignment"] = "capture.trajectoryIndex is the zero-based trajectory row; sample.simulationSeconds is the absolute physics clock" };
        return new TelemetryOutput(new OutputOptions(RecordingPath: Path.Combine(directory, "source.jsonl"), Enabled: false), id.SessionId,
            id.RecordingSource, id.PluginVersion, properties,
            new RecordingOptions { QueueCapacity = 512, MaxDurationSeconds = 1801, MaxFileBytes = 128L * 1024 * 1024, MaxChannelsPerSample = 512 });
    }

    /// <summary>Closes the stage signals; refuses anything short of one complete, drop-free row per recorded pose.</summary>
    internal static void Finish(TelemetryOutput signals, int rows, int poses)
    {
        signals.Dispose();
        if (signals.RecordingDrops != 0 || signals.RecordingError != null || signals.RecordingStatus != "Completed" || rows < 2 || rows != poses)
            throw new IOException("Signal recording was not complete: " + signals.RecordingStatus + "; " + signals.RecordingError);
    }
}
