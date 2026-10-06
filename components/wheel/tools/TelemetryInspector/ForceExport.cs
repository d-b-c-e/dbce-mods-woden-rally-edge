using System.Globalization;
using System.Text;
using System.Text.Json;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

internal static class ForceExport
{
    internal static void Run(string directory, string output, float strength, float peak)
    {
        if (!float.IsFinite(strength) || strength < 0 || strength > 100 || !float.IsFinite(peak) || peak <= 0 || peak > 50)
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength 0..100 and peak >0..50 required");
        StageCaptureReview.Run(directory); // Original hashes, continuous model, tune and no-output proof first.
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "force-config.json")));
        var c = config.RootElement;
        var options = new ForceOptions(strength, peak, c.GetProperty("loadReference").GetSingle(),
            c.GetProperty("slipScale").GetSingle(), c.GetProperty("smoothingMs").GetSingle(),
            c.GetProperty("damping").GetSingle(), c.GetProperty("invert").GetBoolean());
        var signal = new ForceSignal(); signal.Reset();
        var csv = new StringBuilder("time_s,epoch,valid,speed_kmh,request\n");
        foreach (var record in SessionReader.Read(Path.Combine(directory, "source.jsonl")))
        {
            if (record.Kind != SessionRecordKind.Sample) continue;
            var row = record.Sample; var v = row.Channels;
            var sample = new TelemetrySample { Sequence = row.Sequence, ElapsedSeconds = row.ElapsedSeconds,
                SimulationSeconds = v["sample.simulationSeconds"], State = "driving",
                Discontinuity = v["sample.discontinuity"] == 1 ? "recorded" : null };
            foreach (var pair in v) sample.Channels[pair.Key] = pair.Value;
            var result = signal.Evaluate(sample, options);
            csv.AppendLine(string.Join(",", new[] { row.ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture),
                signal.ResetCount.ToString(CultureInfo.InvariantCulture), result.Valid ? "1" : "0",
                (Math.Abs(v["motion.speed"]) * 3.6).ToString("R", CultureInfo.InvariantCulture), result.Preview.ToString("R", CultureInfo.InvariantCulture) }));
        }
        using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(file, new UTF8Encoding(false)); writer.Write(csv);
        Console.WriteLine($"Exported production ForceSignal trial: strength {strength}, peak {peak}. No device output.");
    }
}
