using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dbce.Wheel.Recording;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// The ordinary recording cap against the largest row the real producer can write (DRIVE's RecordingCapacityTests
// method): every schema channel at the longest double text through the actual TelemetryOutput and recorder, the worst
// row adjusted for the longest clock text, at the budgeted rate for the whole duration. No game or owner file.
internal static class RecordingCapacityChecks
{
    // The longest text a finite double takes in the recorder's JSON: sign, 17 significant digits, point, E-ddd.
    const double LongestText = -1.2345678901234567E-300;
    // JsonLines.FooterReserveBytes in the pinned recorder; Boundary proves the recorder keeps exactly this.
    const int FooterReserve = 8192;
    // The largest driving row of two real captures (1155 and 1910 rows): 262 to 269 of the 333 schema channels.
    const int MeasuredRowBytes = 9357;

    static int Bytes(string line) => Encoding.UTF8.GetByteCount(line) + 1;   // each record ends with one newline

    // Every schema channel at the longest text. Publish sets the four sample.* channels from the clocks and counters,
    // so those carry long clock values instead.
    static TelemetrySample Largest(int i)
    {
        var s = new TelemetrySample { SessionId = "capacity", Sequence = 9007199254740991 - 64 + i, ElapsedSeconds = 1199.1234567890123 + i / 60.0,
            SimulationSeconds = 12345.678901234567 + i / 60.0, CarInstanceId = 1, State = "driving" };
        foreach (string channel in TelemetrySchema.Channels.Keys) s.Add(channel, LongestText);
        return s;
    }

    /// <summary>The largest production row at the budgeted rate for the full duration fits the ordinary cap.</summary>
    public static void Capacity(Action<bool, string> check)
    {
        var ordinary = RecordingProfiles.Ordinary();
        check(ordinary.MaxDurationSeconds == 1200 && ordinary.MaxChannelsPerSample == RecordingProfiles.MaxChannels, "20 minutes; channel bound unchanged");
        check(TelemetrySchema.Channels.Count <= RecordingProfiles.MaxChannels, "every schema channel fits one row: " + TelemetrySchema.Channels.Count);
        // Measured, not universal: 1/60 s on every driving gap of two real captures, plus the idle path's one row per 0.1 s.
        check(RecordingProfiles.PhysicsHz == 60 && RecordingProfiles.IdleHz == 10 && RecordingProfiles.ProducerHz == 70, "budget rate: measured physics plus the idle bound");

        string path = Path.Combine(Path.GetTempPath(), "woden-capacity-" + Guid.NewGuid().ToString("N") + ".jsonl");
        string[] lines;
        try
        {
            using (var output = new TelemetryOutput(new OutputOptions(ForzaPort: 0, DetailPort: 0, RecordingPath: path, Enabled: false), "capacity", "synthetic", "0.2.14", null, ordinary))
            {
                for (int i = 0; i < 64; i++) output.Publish(Largest(i));
                output.StopRecording();
                for (int i = 0; i < 250 && output.RecordingStatus is "Recording" or "Stopping"; i++) Thread.Sleep(20);
                var counters = output.RecordingCounters!;
                check(output.RecordingStatus == "Completed" && counters.WrittenSamples == 64 && counters.DroppedSamples == 0, "every row written, none dropped: " + output.RecordingStatus);
            }
            lines = File.ReadAllLines(path, Encoding.UTF8);
        }
        finally { File.Delete(path); }

        // A production row's recorder sequence reaches 5 digits by 20 minutes at the budgeted rate; elapsed text up to 24 characters.
        int sequenceDigits = (RecordingProfiles.ProducerHz * (long)ordinary.MaxDurationSeconds).ToString(CultureInfo.InvariantCulture).Length;
        string longest = LongestText.ToString("R", CultureInfo.InvariantCulture);
        check(longest.Length == 24, "the longest double text is 24 characters: " + longest);
        int worst = 0, samples = 0;
        foreach (string line in lines.Where(l => l.Contains("\"kind\":\"sample\"", StringComparison.Ordinal)))
        {
            var envelope = Regex.Match(line, "\"elapsedSeconds\":([^,}]+),\"sequence\":(\\d+)\\}");
            check(envelope.Success, "sample envelope: " + line[Math.Max(0, line.Length - 120)..]);
            check(line.Contains(longest, StringComparison.Ordinal), "the longest text is written: " + line[..Math.Min(line.Length, 240)]);
            worst = Math.Max(worst, Bytes(line) + Math.Max(0, 24 - envelope.Groups[1].Length) + Math.Max(0, sequenceDigits - envelope.Groups[2].Length));
            samples++;
        }
        check(samples == 64, "64 sample rows: " + samples);
        // Bounds: the recorder refuses a header above MaxRecordBytes; rows at the budgeted rate for the whole duration.
        long budget = ordinary.MaxRecordBytes + 1L + (long)(RecordingProfiles.ProducerHz * ordinary.MaxDurationSeconds) * worst + FooterReserve;
        Console.WriteLine($"  largest row {worst} B; 20 minutes at {RecordingProfiles.ProducerHz} Hz budgets {budget} B of {ordinary.MaxFileBytes} B");
        check(budget <= ordinary.MaxFileBytes, $"budget {budget} B (row {worst} B) within the cap {ordinary.MaxFileBytes} B");
        check(64L * 1024 * 1024 < (long)MeasuredRowBytes * RecordingProfiles.PhysicsHz * 1200 &&
            (long)MeasuredRowBytes * RecordingProfiles.ProducerHz * 1200 < ordinary.MaxFileBytes, "the measured rows fit; the former 64 MiB would not");
        check(ordinary.MaxFileBytes < 2L << 30, "still bounded, below 2 GiB");

        var stage = RecordingProfiles.Stage();
        check(stage.QueueCapacity == 512 && stage.MaxDurationSeconds == 1801 && stage.MaxFileBytes == 128L * 1024 * 1024 &&
            stage.MaxChannelsPerSample == RecordingProfiles.MaxChannels, "supervised stage profile unchanged");
        check(RecordingProfiles.OrdinaryLimit == "20 minutes / 1.75 GiB" && RecordingProfiles.Size(ordinary.MaxFileBytes) == "1.75 GiB" &&
            RecordingProfiles.Size(64L * 1024 * 1024) == "64.0 MiB", "stated limits read the profile");
        // The companion script cannot read the profile, so it states the same text.
        string script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../tools/Start-RecordedGame.ps1"));
        check(script.Contains("(" + RecordingProfiles.OrdinaryLimit + " maximum)", StringComparison.Ordinal), "Start-RecordedGame states the profile's limit");
    }

    /// <summary>The budget is the real recorder's own arithmetic: header, rows and footer reserve stop it exactly there.</summary>
    public static void Boundary(Action<bool, string> check)
    {
        var metadata = new SessionMetadata { Game = "synthetic Woden capacity", PluginVersion = "0.2.14", ToolkitVersion = "test",
            ChannelUnits = TelemetrySchema.Channels.ToDictionary(x => x.Key, x => x.Value.Unit) };
        var row = TelemetrySchema.Channels.Keys.ToDictionary(k => k, _ => LongestText);
        string[] Write(RecordingOptions options, int rows, out SessionRecorder done)
        {
            string path = Path.Combine(Path.GetTempPath(), "woden-capacity-boundary-" + Guid.NewGuid().ToString("N") + ".jsonl");
            var recorder = new SessionRecorder(path, metadata, options);
            double t = 1199.1234567890123;
            for (int i = 0; i < rows; i++) { recorder.TryRecord(t, row); t = Math.BitIncrement(t); }
            check(recorder.Stop(TimeSpan.FromSeconds(10)), "bounded stop");
            done = recorder;
            try { check(new FileInfo(path).Length <= options.MaxFileBytes, "file within its cap"); return File.ReadAllLines(path, Encoding.UTF8); }
            finally { File.Delete(path); }
        }

        var free = Write(RecordingProfiles.Ordinary(), 12, out _);
        long header = Bytes(free[0]);
        var rows = free.Skip(1).SkipLast(1).Select(Bytes).ToArray();
        check(rows.Length == 12, "12 rows measured");
        foreach (int k in new[] { 1, 7 })
        {
            long cap = header + rows.Take(k).Sum() + FooterReserve;
            foreach (var (limit, expected) in new[] { (cap, k), (cap - 1, k - 1) })
            {
                var options = RecordingProfiles.Ordinary(); options.MaxFileBytes = limit;
                var lines = Write(options, 12, out var recorder);
                var c = recorder.GetCounters();
                check(recorder.Status == RecorderStatus.LimitReached && recorder.StopReason == "fileSizeLimit", $"cap {limit}: stopped at the size limit");
                check(c.WrittenSamples == expected && lines.Length == expected + 2, $"cap {limit}: exactly {expected} rows written, got {c.WrittenSamples}");
                check(lines[^1].Contains("\"stopReason\":\"fileSizeLimit\"", StringComparison.Ordinal) && lines[^1].Contains("\"completed\":false", StringComparison.Ordinal),
                    $"cap {limit}: footer records the limit");
            }
        }
    }
}
