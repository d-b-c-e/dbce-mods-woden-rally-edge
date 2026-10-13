using System.Globalization;
using System.Text;
using Dbce.Wheel.Playback;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

internal static class ForceExport
{
    /// <param name="gripRatio">Null: the classic v3 model (strength, peak). A value: the recorded Grip version at that load ratio
    /// (peak ignored), which needs the live friction-curve channels; streams without them are refused, never default-filled.</param>
    internal static void Run(string directory, string output, float strength, float peak, float? gripRatio = null)
    {
        if (!float.IsFinite(strength) || strength < 0 || strength > 100 || !float.IsFinite(peak) || peak <= 0 || peak > 50)
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength 0..100 and peak >0..50 required");
        // Never downgrade a damaged stage to the ordinary capture contract.
        if (!new[] { "complete.tsv", "stage-context.json", "channels.json" }.Any(n => File.Exists(Path.Combine(directory, n))))
        {
            var rows = RecordedForceReplay.ExportTrial(directory, strength, peak, gripRatio);
            var ordinary = new StringBuilder("time_s,epoch,valid,speed_kmh,request,model,eligible,exclusion,steering,damping,model_version\n");
            foreach (var row in rows)
                ordinary.AppendLine(string.Join(",", new[] { row.Time.ToString("R", CultureInfo.InvariantCulture),
                    row.Epoch.ToString(CultureInfo.InvariantCulture), row.Valid ? "1" : "0", row.SpeedKmh?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                    row.Request.ToString("R", CultureInfo.InvariantCulture), row.ModelVersion >= 4 ? "grip" : "classic", row.Eligible ? "1" : "0", row.Exclusion,
                    row.Steering?.ToString("R", CultureInfo.InvariantCulture) ?? "", row.Damping?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                    row.ModelVersion.ToString(CultureInfo.InvariantCulture) }));
            using var target = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var text = new StreamWriter(target, new UTF8Encoding(false)); text.Write(ordinary);
            Console.WriteLine($"Exported verified ordinary replay-case trial: {rows.Count} model rows; strength {strength}, " +
                (gripRatio.HasValue ? $"Grip load ratio {gripRatio.Value}" : $"Classic peak {peak}") + ". Recorded gate/reset lifecycle retained; no device output.");
            return;
        }
        StageCaptureReview.Run(directory); // Original hashes, continuous model, tune and no-output proof first.
        if (ForceAnalysisHistory.Declared(SessionReader.Read(Path.Combine(directory, "source.jsonl")).ToArray()))
        {
            // New stages declare their independent history; legacy stage arithmetic below remains unchanged.
            RunAnalysis(directory, output, strength, peak, gripRatio); return;
        }
        string sealHash = ArtifactSeal.Hash(Path.Combine(directory, "complete.tsv"));
        var captured = StageCaptureReview.ReadOptions(directory);
        var options = captured with { Strength = strength, PeakPercent = peak, Model = 3 };
        if (gripRatio.HasValue)
        {
            if (!float.IsFinite(gripRatio.Value) || gripRatio.Value <= 0) throw new ArgumentOutOfRangeException(nameof(gripRatio));
            // A grip capture keeps its recorded grip version (v4/v5/v6); a classic capture trials the latest grip model.
            options = options with { Model = captured.Grip ? captured.Model : GripSignal.ModelVersion, LoadRatio = gripRatio.Value,
                GripSmoothing = captured.Grip ? captured.GripSmoothing : .2f };
        }
        var signal = new ForceSignal(); signal.Reset();
        var grip = new GripSignal(); grip.NewCar();
        // steering/damping are the evaluated trial's own pre-clamp terms (grip models; empty for classic), never the
        // recorded ones: a strength override changes the steering term (Codex review of 2d5ab07).
        var csv = new StringBuilder("time_s,epoch,valid,speed_kmh,request,model,eligible,exclusion,steering,damping,model_version\n");
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
                options.Grip ? "grip" : "classic", exclusion.Length == 0 ? "1" : "0", exclusion,
                options.Grip ? grip.LastSteering.ToString("R", CultureInfo.InvariantCulture) : "", options.Grip ? grip.LastDamping.ToString("R", CultureInfo.InvariantCulture) : "",
                options.Model.ToString(CultureInfo.InvariantCulture) }));
        }
        ArtifactSeal.Verify(directory, "source.jsonl", "force-config.json", "stage-context.json", "channels.json");
        if (ArtifactSeal.Hash(Path.Combine(directory, "complete.tsv")) != sealHash) throw new IOException("Capture seal changed during force export");
        using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(file, new UTF8Encoding(false)); writer.Write(csv);
        Console.WriteLine(options.Grip ? $"Exported production GripSignal trial: strength {strength}, load ratio {options.LoadRatio}. Compare on the eligible column. No device output."
            : $"Exported production ForceSignal trial: strength {strength}, peak {peak}. No device output.");
    }

    internal static void RunAnalysis(string directory, string output, float strength, float peak, float? gripRatio = null)
    {
        if (!float.IsFinite(strength) || strength is < 0 or > 100 || !float.IsFinite(peak) || peak <= 0 || peak > 50 ||
            gripRatio.HasValue && (!float.IsFinite(gripRatio.Value) || gripRatio.Value <= 0)) throw new ArgumentOutOfRangeException(nameof(strength));
        string source = Path.Combine(directory, "source.jsonl"), config = Path.Combine(directory, "force-config.json");
        string sourceHash = ArtifactSeal.Hash(source), configHash = ArtifactSeal.Hash(config);
        IReadOnlyList<RecordedForceTrialSample> rows;
        bool stage = new[] { "complete.tsv", "stage-context.json", "channels.json" }.Any(n => File.Exists(Path.Combine(directory, n)));
        string? seal = null;
        if (stage)
        {
            StageCaptureReview.Run(directory);
            seal = ArtifactSeal.Hash(Path.Combine(directory, "complete.tsv"));
            var captured = StageCaptureReview.ReadOptions(directory);
            var options = captured with { Strength = strength, PeakPercent = peak, Model = 3 };
            if (gripRatio.HasValue) options = options with { Model = captured.Grip ? captured.Model : GripSignal.ModelVersion,
                LoadRatio = gripRatio.Value, GripSmoothing = captured.Grip ? captured.GripSmoothing : .2f };
            rows = ForceAnalysisHistory.Replay(SessionReader.Read(source).ToArray(), captured, options);
            ArtifactSeal.Verify(directory, "source.jsonl", "force-config.json", "stage-context.json", "channels.json");
        }
        else rows = RecordedForceReplay.ExportAnalysis(directory, strength, peak, gripRatio);
        if (sourceHash != ArtifactSeal.Hash(source) || configHash != ArtifactSeal.Hash(config) ||
            stage && seal != ArtifactSeal.Hash(Path.Combine(directory, "complete.tsv"))) throw new IOException("Analysis source identity changed during export");
        var csv = new StringBuilder("time_s,epoch,valid,speed_kmh,request,model,eligible,exclusion,steering,damping,model_version,stream,history_contract,source_sha256,source_config_sha256,trial_strength_percent,trial_peak_percent,trial_grip_ratio,physical_output\n");
        string Number(double n) => n.ToString("R", CultureInfo.InvariantCulture);
        foreach (var row in rows)
            csv.AppendLine(string.Join(",", new[] { Number(row.Time), row.Epoch.ToString(CultureInfo.InvariantCulture), row.Valid ? "1" : "0",
                row.SpeedKmh.HasValue ? Number(row.SpeedKmh.Value) : "", Number(row.Request), row.ModelVersion >= 4 ? "grip" : "classic",
                row.Eligible ? "1" : "0", row.Exclusion, row.Steering.HasValue ? Number(row.Steering.Value) : "", row.Damping.HasValue ? Number(row.Damping.Value) : "",
                row.ModelVersion.ToString(CultureInfo.InvariantCulture), "independent-analysis", ForceAnalysisHistory.Contract, sourceHash, configHash,
                Number(strength), Number(peak), gripRatio.HasValue ? Number(gripRatio.Value) : "", "false" }));
        using var target = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(target, new UTF8Encoding(false)); writer.Write(csv);
        Console.WriteLine($"Exported {ForceAnalysisHistory.Contract}: {rows.Count} analysis rows, {rows.Count(r => r.Eligible)} eligible; independent steering+damping only, no physical output. Actual ffb.* replay remains separate.");
    }
}
