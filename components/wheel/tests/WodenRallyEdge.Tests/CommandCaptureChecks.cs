using System.Text.Json;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

internal static class CommandCaptureChecks
{
    private sealed class Device : IForceDevice
    {
        internal readonly List<string> Calls = new();
        internal bool Ready, Accept = true, ThrowOpen;
        public string? Error => @"C:\private\driver username secret " + Guid.Empty;
        public bool CanOpen { get { Calls.Add("can-open:" + Ready); return Ready; } }
        public bool Open(Guid guid) { Calls.Add("open:True"); if (ThrowOpen) throw new InvalidOperationException("private driver text"); return true; }
        public bool Write(float force) { Calls.Add("write:" + BitConverter.SingleToInt32Bits(force) + ":" + Accept); return Accept; }
        public void ZeroAndStop() => Calls.Add("zero-stop");
        public void Panic() => Calls.Add("panic");
        public void Close() => Calls.Add("close");
    }
    private static (List<string> calls, CommandCaptureDocument? doc) Scenario(Func<CommandProvenanceCapture>? factory, Func<double, TelemetrySample> contact)
    {
        ForceControllerChecks.Create();
        Runtime.Settings.FfbGuid = "11111111-2222-3333-4444-555555555555";
        var capture = factory?.Invoke();
        var device = new Device(); var controller = new ForceController(device, capture);
        controller.Prepare(); device.Ready = true; controller.Prepare();
        for (int i = 1; i <= 12; i++) controller.Tick(contact(i * .02));
        Runtime.Devices!.Readable = false; controller.Prepare(); Runtime.Devices.Readable = true; controller.Prepare();
        Runtime.Settings.FfbGuid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"; controller.Prepare();
        Panel.Open = true; controller.Tick(contact(.3)); Panel.Open = false;
        device.Accept = false; controller.Tick(contact(.32)); device.Accept = true;
        controller.SetEnabled(true); controller.Prepare(); controller.Tick(contact(.34));
        controller.Shutdown(); controller.Prepare(); controller.Tick(contact(.36));
        device.Calls.Add("readers:" + Runtime.Devices.Closes + ":" + Runtime.Devices.Refreshes + ":" + Runtime.Settings.Saves);
        return (device.Calls, capture?.Finish());
    }
    internal static void Run(Action<bool, string> check, Func<double, TelemetrySample> contact)
    {
        void Refused(Action action, string name) { bool refused = false; try { action(); } catch (IOException) { refused = true; } check(refused, name); }
        var baseline = Scenario(null, contact);
        double observation = 0;
        var recorded = Scenario(() => new CommandProvenanceCapture(() => observation += .001), contact);
        check(baseline.calls.SequenceEqual(recorded.calls), "opt-in capture preserves exact fake native calls/float bits/settings saves/reader counts");
        var doc = recorded.doc!; CommandCaptureAudit.Validate(doc);
        check(doc.Footer.Complete && doc.Events.Any(e => e.Code == "write" && e.Result == false), "complete software trace retains failed attempted output");
        check(doc.Events.Any(e => e.Kind == "target-change") && doc.Events.Any(e => e.Kind == "reader" && e.Result == false), "actual controller target change and recovery decisions observed");
        check(doc.Events.Any(e => e.Kind == "settings") && doc.Events.Any(e => e.Kind == "model" && e.SimulationBits != null && e.SourceElapsedBits != null), "options identity and distinct exact model/source clocks observed");
        var projection = new List<string>();
        CommandCaptureAudit.ReplayCalls(doc, e => {
            if (e.Code is "reader-close" or "refresh") return;
            projection.Add(e.Code == "write" ? "write:" + e.CommandBits + ":" + e.Result : e.Code is "open" or "can-open" ? e.Code + ":" + e.Result : e.Code);
        });
        check(projection.SequenceEqual(baseline.calls.Take(baseline.calls.Count - 1)), "record validate replay projects exact canonical call sequence into pure fake sink");
        string json = JsonSerializer.Serialize(doc);
        check(!json.Contains("11111111") && !json.Contains("aaaaaaaa") && !json.Contains("private") && !json.Contains("username"), "serialized trace contains session tokens and bounded errors, no GUID/path/raw error");
        string dir = Path.Combine(Path.GetTempPath(), "woden-command-capture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "synthetic.json");
        CommandProvenanceCapture.WriteNew(file, doc);
        check(CommandCaptureAudit.Read(file).Events.Length == doc.Events.Length, "bounded atomic synthetic export validates");
        Refused(() => CommandProvenanceCapture.WriteNew(file, doc), "existing artifact cannot be overwritten");
        File.WriteAllText(Path.Combine(dir, "truncated.json"), json[..(json.Length / 2)]);
        Refused(() => CommandCaptureAudit.Read(Path.Combine(dir, "truncated.json")), "truncated capture refused");
        Refused(() => CommandCaptureAudit.Validate(doc with { Footer = doc.Footer with { Complete = false } }), "incomplete footer refused");
        var rows = doc.Events.ToArray(); int open = Array.FindIndex(rows, e => e.Kind == "call-end" && e.Code == "open"); rows[open] = rows[open] with { Result = false };
        Refused(() => CommandCaptureAudit.Validate(doc with { Events = rows }), "write after failed open refused");
        rows = doc.Events.ToArray(); rows[0] = rows[0] with { Device = Runtime.Settings.FfbGuid };
        Refused(() => CommandCaptureAudit.Validate(doc with { Events = rows }), "persistent GUID token refused");
        File.WriteAllText(Path.Combine(dir, "extra.json"), json.Replace("\"Schema\":", "\"PrivatePath\":\"secret\",\"Schema\":"));
        Refused(() => CommandCaptureAudit.Read(Path.Combine(dir, "extra.json")), "unknown privacy-bearing field refused");
        File.WriteAllText(Path.Combine(dir, "duplicate.json"), json.Replace("\"Schema\":", "\"Schema\":\"duplicate\",\"Schema\":"));
        Refused(() => CommandCaptureAudit.Read(Path.Combine(dir, "duplicate.json")), "duplicate JSON field refused");
        File.WriteAllBytes(Path.Combine(dir, "oversize.json"), new byte[CommandProvenanceCapture.MaximumBytes + 1]);
        Refused(() => CommandCaptureAudit.Read(Path.Combine(dir, "oversize.json")), "oversize input refused before allocation");
        rows = doc.Events.ToArray(); rows[0] = rows[0] with { OptionsSha256 = "private text" };
        Refused(() => CommandCaptureAudit.Validate(doc with { Events = rows }), "stray private options hash refused");
        Refused(() => CommandCaptureAudit.Validate(doc with { Unrecorded = new[] { "private", "2", "3", "4", "5", "6" } }), "scope omissions cannot carry private text");
        rows = doc.Events.ToArray(); rows[^1] = rows[^1] with { ClockBits = "0000000000000000" };
        Refused(() => CommandCaptureAudit.Validate(doc with { Events = rows }), "backwards observation clock refused");
        rows = doc.Events.ToArray(); int settings = Array.FindIndex(rows, e => e.Kind == "settings"); rows[settings] = rows[settings] with { OptionsSha256 = new string('0', 64) };
        Refused(() => CommandCaptureAudit.Validate(doc with { Events = rows }), "settings identity tamper refused");
        foreach (var mode in new[] { "overflow", "clock", "sink" }) {
            var bad = Scenario(() => mode == "overflow" ? new(() => 1, 16) : mode == "clock" ? new(() => throw new Exception("private clock")) : new(() => 1, sink: _ => throw new Exception("private sink")), contact);
            check(baseline.calls.SequenceEqual(bad.calls), mode + " leaves actual controller behavior unchanged");
            check(!bad.doc!.Footer.Complete, mode + " reports incomplete");
            Refused(() => CommandCaptureAudit.Validate(bad.doc!), mode + " cannot replay");
        }
        ForceControllerChecks.Create();
        var exceptionCapture = new CommandProvenanceCapture(() => 1);
        var exceptionDevice = new Device { Ready = true, ThrowOpen = true };
        var exceptionController = new ForceController(exceptionDevice, exceptionCapture);
        bool propagated = false; try { exceptionController.Prepare(); } catch (InvalidOperationException) { propagated = true; }
        exceptionController.Shutdown(); var exceptionDoc = exceptionCapture.Finish(); CommandCaptureAudit.Validate(exceptionDoc);
        check(propagated && exceptionDevice.Calls.SequenceEqual(new[] { "can-open:True", "open:True", "zero-stop", "close" }), "native exception propagates with exact existing cleanup sequence");
        check(exceptionDoc.Events.Any(e => e.Code == "open" && e.State == 1) && !JsonSerializer.Serialize(exceptionDoc).Contains("private"), "native exception uses bounded indicator without raw error");
        string evidence = Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/tests/canonical-command-capture-" + Guid.NewGuid().ToString("N") + ".json");
        CommandProvenanceCapture.WriteNew(evidence, doc);
        Console.WriteLine("  synthetic canonical command capture: " + Path.GetFullPath(evidence));
        Directory.Delete(dir, true);
    }
}
