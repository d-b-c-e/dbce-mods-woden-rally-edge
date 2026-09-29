using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WodenRallyEdge.Core;

public static class RecordingArtifacts
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };
    public static string Sha256(string path)
    {
        using var input = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(input)).ToLowerInvariant();
    }

    public static string WriteForceConfig(string path, ForceOptions options)
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", "woden.force-config"); writer.WriteNumber("version", 1);
            writer.WriteString("model", ForceObservationSemantics.Model);
            writer.WriteNumber("strengthPercent", options.Strength); writer.WriteNumber("peakPercent", options.PeakPercent);
            writer.WriteNumber("loadReference", options.LoadReference); writer.WriteNumber("slipScale", options.SlipScale);
            writer.WriteNumber("smoothingMs", options.SmoothingMs); writer.WriteNumber("damping", options.Damping);
            writer.WriteBoolean("invert", options.Invert); writer.WriteEndObject();
        }
        WriteNew(path, memory.ToArray()); return Sha256(path);
    }

    public static string WriteCaptureProfile(string path, string gameAssemblySha256, string pluginSha256,
        string modConfigSha256, string bindingsSha256, bool wheelEnabled, bool ffbEnabled, bool followSteering, string selectedFfbGuid)
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory, WriterOptions))
        {
            writer.WriteStartObject(); writer.WriteString("schema", "woden.capture-profile"); writer.WriteNumber("version", 1);
            writer.WriteString("gameAssemblySha256", gameAssemblySha256); writer.WriteString("pluginSha256", pluginSha256);
            writer.WriteString("modConfigSha256", modConfigSha256); writer.WriteString("bindingsSha256", bindingsSha256);
            writer.WriteBoolean("wheelEnabled", wheelEnabled); writer.WriteBoolean("ffbEnabled", ffbEnabled);
            writer.WriteBoolean("followSteering", followSteering); writer.WriteString("selectedFfbGuid", selectedFfbGuid);
            writer.WriteEndObject();
        }
        WriteNew(path, memory.ToArray()); return Sha256(path);
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes); output.WriteByte((byte)'\n'); output.Flush(true);
    }
}
