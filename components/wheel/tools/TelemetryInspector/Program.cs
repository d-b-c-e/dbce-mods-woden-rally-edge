using System.Text.Json;
using Dbce.Wheel.Recording;
using Dbce.Wheel.Ffb;
using WodenRallyEdge.Core;

var json = new JsonSerializerOptions(TelemetryOutput.Json) { WriteIndented = true };
if ((args.Length == 5 || args.Length == 7 && args[5] == "grip") && args[0] == "ffb-export")
{
    // ffb-export <stage-or-case> <new.csv> <strength> <peak> [grip <loadRatio>]
    try { ForceExport.Run(args[1], args[2], float.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture), float.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture),
        args.Length == 7 ? float.Parse(args[6], System.Globalization.CultureInfo.InvariantCulture) : null); return 0; }
    catch (Exception ex) { Console.Error.WriteLine("FORCE EXPORT REFUSED: " + ex.Message); return 2; }
}
if (args.Length == 2 && args[0] == "stage-review")
{
    try { Console.WriteLine(JsonSerializer.Serialize(StageCaptureReview.Run(args[1]), json)); return 0; }
    catch (Exception ex) { Console.Error.WriteLine("STAGE REVIEW REFUSED: " + ex.Message); return 2; }
}
if (args.Length == 2 && args[0] == "schema")
{
    File.WriteAllText(args[1], JsonSerializer.Serialize(new { schema = TelemetrySchema.Name, version = TelemetrySchema.Version, channels = TelemetrySchema.Channels }, json));
    Console.WriteLine($"Wrote {TelemetrySchema.Channels.Count} channel definitions to {args[1]}"); return 0;
}
if (args.Length == 2 && args[0] == "devices")
{
    if (!WheelFfbNative.Load(Path.GetFullPath(args[1]))) throw new IOException(WheelFfbNative.LastError);
    var devices = WheelFfbNative.ListAllDevices().Select(x => new { x.Name, x.InstanceGuid, x.Axes, x.Buttons, x.ForceFeedback });
    Console.WriteLine(JsonSerializer.Serialize(devices, json));
    WheelFfbNative.ShutdownAll(); return 0;
}
if (args.Length == 2 && args[0] == "reprocess")
{
    try { Console.WriteLine(JsonSerializer.Serialize(RecordedForceReplay.Reprocess(args[1]), json)); return 0; }
    catch (Exception ex) { Console.Error.WriteLine("REPROCESS REFUSED: " + ex.Message); return 2; }
}
if (args.Length is 4 or 5 && args[0] == "trial")
{
    try
    {
        double tolerance = args.Length == 5 ? double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : .000001;
        Console.WriteLine(JsonSerializer.Serialize(RecordedForceReplay.Trial(args[1], args[2], args[3], tolerance), json)); return 0;
    }
    catch (Exception ex) { Console.Error.WriteLine("TRIAL REFUSED: " + ex.Message); return 2; }
}
if (args.Length is 3 or 4 && args[0] == "compare")
{
    try
    {
        double tolerance = args.Length == 4 ? double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : .000001;
        var result = RecordedForceReplay.Compare(args[1], args[2], tolerance); Console.WriteLine(JsonSerializer.Serialize(result, json)); return result.Equal ? 0 : 1;
    }
    catch (Exception ex) { Console.Error.WriteLine("OBSERVATION INVALID: " + ex.Message); return 2; }
}
if (args.Length != 2 || args[0] != "inspect") { Console.Error.WriteLine("Usage: schema <output.json> | inspect <session.jsonl> | stage-review <recording-directory> | reprocess <case-directory> | trial <case-directory> <candidate-config.json> <new-observation.jsonl> [tolerance] | compare <baseline.jsonl> <candidate.jsonl> [tolerance] | devices <native DLL directory>"); return 1; }
try
{
    var stats = new Dictionary<string, (long count, double min, double max, double sum)>();
    long samples = 0, markers = 0; double first = -1, previous = 0, maxGap = 0; SessionFooter? footer = null;
    foreach (var record in SessionReader.Read(args[1]))
    {
        if (record.Kind == SessionRecordKind.Metadata) Console.WriteLine(JsonSerializer.Serialize(record.Metadata, json));
        if (record.Kind == SessionRecordKind.Marker) markers++;
        if (record.Kind == SessionRecordKind.Sample)
        {
            var s = record.Sample; if (first < 0) first = s.ElapsedSeconds; else maxGap = Math.Max(maxGap, s.ElapsedSeconds - previous);
            previous = s.ElapsedSeconds; samples++;
            foreach (var pair in s.Channels)
            {
                var st = stats.TryGetValue(pair.Key, out var old) ? old : (0L, double.PositiveInfinity, double.NegativeInfinity, 0.0);
                stats[pair.Key] = (st.Item1 + 1, Math.Min(st.Item2, pair.Value), Math.Max(st.Item3, pair.Value), st.Item4 + pair.Value);
            }
        }
        if (record.Kind == SessionRecordKind.Footer) footer = record.Footer;
    }
    Console.WriteLine(JsonSerializer.Serialize(new { validFile = true, samples, markers, duration = previous - Math.Max(0, first), maxGapSeconds = maxGap,
        footer = footer == null ? null : new { footer.Completed, footer.StopReason, footer.Counts },
        channels = stats.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => new { x.Value.count, x.Value.min, x.Value.max, mean = x.Value.sum / x.Value.count, coverage = x.Value.count / (double)Math.Max(1, samples) }) }, json));
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine("INVALID OR INCOMPLETE RECORDING: " + ex.Message); return 2; }
