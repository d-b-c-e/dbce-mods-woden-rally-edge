using System.Globalization;
using System.Text;
using Dbce.Wheel.Playback;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

internal static class ForceExport
{
    /// <param name="gripRatio">Null: the classic v3 model (strength, peak). A value: the grip v4 model at that load ratio
    /// (peak ignored), which needs the live friction-curve channels; streams without them are refused, never default-filled.</param>
    internal static void Run(string directory, string output, float strength, float peak, float? gripRatio = null)
    {
        if (!float.IsFinite(strength) || strength < 0 || strength > 100 || !float.IsFinite(peak) || peak <= 0 || peak > 50)
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength 0..100 and peak >0..50 required");
        StageCaptureReview.Run(directory); // Original hashes, continuous model, tune and no-output proof first.
        string sealHash = ArtifactSeal.Hash(Path.Combine(directory, "complete.tsv"));
        var captured = StageCaptureReview.ReadOptions(directory);
        var options = captured with { Strength = strength, PeakPercent = peak, Model = 3 };
        if (gripRatio.HasValue)
        {
            if (!float.IsFinite(gripRatio.Value) || gripRatio.Value <= 0) throw new ArgumentOutOfRangeException(nameof(gripRatio));
            options = options with { Model = GripSignal.ModelVersion, LoadRatio = gripRatio.Value,
                GripSmoothing = captured.Grip ? captured.GripSmoothing : .2f };
        }
        var signal = new ForceSignal(); signal.Reset();
        var grip = new GripSignal(); grip.NewCar();
        var csv = new StringBuilder("time_s,epoch,valid,speed_kmh,request,model,eligible,exclusion\n");
        foreach (var record in SessionReader.Read(Path.Combine(directory, "source.jsonl")))
        {
            if (record.Kind != SessionRecordKind.Sample) continue;
            var row = record.Sample; var v = row.Channels;
            var sample = new TelemetrySample { Sequence = row.Sequence, ElapsedSeconds = row.ElapsedSeconds,
                SimulationSeconds = v["sample.simulationSeconds"], State = "driving",
                Discontinuity = v["sample.discontinuity"] == 1 ? "recorded" : null };
            foreach (var pair in v) sample.Channels[pair.Key] = pair.Value;
            ForceResult result; string exclusion;
            if (options.Grip)
            {
                foreach (string corner in new[] { "fl", "fr" })
                    if (v.TryGetValue("wheel." + corner + ".grounded", out double g) && g == 1 && !v.ContainsKey("wheel." + corner + ".sideFriction.extremumSlip"))
                        throw new IOException("This stream has no recorded friction curves (captured before f1ee7af); a grip trial cannot be run from it");
                FrontLoadReference? shared = null;
                if (v.ContainsKey("ffb.grip.reference") || v.ContainsKey("ffb.grip.referenceKind"))
                    shared = StageCaptureReview.ReadReference(v);
                result = grip.Evaluate(sample, options, shared);
                var reference = shared ?? grip.Reference;
                exclusion = !result.Valid ? "model invalid" : reference.Kind == FrontLoadReferenceKind.None ? "no front-load reference" :
                    reference.Kind != FrontLoadReferenceKind.Driving ? "provisional reference" : "";
            }
            else { result = signal.Evaluate(sample, options); exclusion = result.Valid ? "" : "model invalid"; }
            csv.AppendLine(string.Join(",", new[] { row.ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture),
                (options.Grip ? grip.ResetCount : signal.ResetCount).ToString(CultureInfo.InvariantCulture), result.Valid ? "1" : "0",
                (Math.Abs(v["motion.speed"]) * 3.6).ToString("R", CultureInfo.InvariantCulture), result.Preview.ToString("R", CultureInfo.InvariantCulture),
                options.Grip ? "grip" : "classic", exclusion.Length == 0 ? "1" : "0", exclusion }));
        }
        ArtifactSeal.Verify(directory, "source.jsonl", "force-config.json", "stage-context.json", "channels.json");
        if (ArtifactSeal.Hash(Path.Combine(directory, "complete.tsv")) != sealHash) throw new IOException("Capture seal changed during force export");
        using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(file, new UTF8Encoding(false)); writer.Write(csv);
        Console.WriteLine(options.Grip ? $"Exported production GripSignal trial: strength {strength}, load ratio {options.LoadRatio}. Compare on the eligible column. No device output."
            : $"Exported production ForceSignal trial: strength {strength}, peak {peak}. No device output.");
    }
}
