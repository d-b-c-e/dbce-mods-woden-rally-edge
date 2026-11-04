using System.Text;
using System.Text.Json;
using BepInEx.Configuration;
using WodenRallyEdge;
using WodenRallyEdge.Core;

if (args.Length != 2) return 2;
string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
string path = Path.Combine(root, "presentation.cfg");
int checks = 0, failures = 0;
void Check(bool pass, string label) { checks++; if (!pass) failures++; Console.Error.WriteLine((pass ? "ok " : "FAIL ") + label); }
bool Refuses(Action save) { try { save(); return false; } catch (IOException) { return true; } catch (UnauthorizedAccessException) { return true; } catch (DecoderFallbackException) { return true; } }
byte[] Bytes(string text, bool bom = false) => (bom ? new byte[] { 239,187,191 } : Array.Empty<byte>()).Concat(Encoding.UTF8.GetBytes(text)).ToArray();
const string raw = "# owner café\r\n[Interface]\n  View  = Simple  \r\n Page = Setup \nScalePercent=117\r\n[ForceFeedback]\nEnabled=false\r\nModel=Classic\nStrengthPercent = 43.75\r\nDamping=0.123\n[Future]\r\nMystery=keep";
(Settings Settings, ConfigFile Config) Load(byte[] data) { File.WriteAllBytes(path, data); var cfg = new ConfigFile(path, false); return (new Settings(cfg), cfg); }
bool OldModel(Settings s, ConfigFile c) => s.UiView == "Simple" && s.UiPage == "Setup" && c.Bind("Interface","View","Simple").Value == "Simple" && c.Bind("Interface","Page","Setup").Value == "Setup";
void NoTemps() => Check(!Directory.GetFiles(root).Any(p => Path.GetFileName(p).Contains(".tmp-", StringComparison.Ordinal)), "owned temps closed and removed");

foreach (int variant in args[1] == "regression" ? new[] { 0 } : new[] { 0,1,2,3 }) {
    string text = variant == 2 ? raw.Replace("\r\n", "\n") : variant == 3 ? raw.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n") : raw;
    byte[] before = Bytes(text, variant == 1);
    var (s, cfg) = Load(before); var force = s.ForceOptions; var pose = s.GetCameraPose(false);
    s.SetPresentation("Advanced", "Driving");
    byte[] expected = Bytes(text.Replace("View  = Simple", "View  = Advanced").Replace("Page = Setup", "Page = Driving"), variant == 1);
    Check(File.ReadAllBytes(path).SequenceEqual(expected), "actual SetPresentation changes only two values/BOM/EOL/EOF");
    Check(s.UiView == "Advanced" && s.UiPage == "Driving" && cfg.Bind("Interface","View","Simple").Value == "Advanced" && cfg.Bind("Interface","Page","Setup").Value == "Driving", "model and real BepInEx entries follow committed preferences");
    Check(force == s.ForceOptions && pose == s.GetCameraPose(false) && !s.FfbEnabled && s.UiScale == 117, "presentation keeps force/camera/runtime options");
    if (args[1] != "regression") {
        var cold = new Settings(new ConfigFile(path, false));
        Check(cold.UiView == "Advanced" && cold.UiPage == "Driving" && !cold.FfbEnabled && cold.ForceOptions == force, "real ConfigFile cold readback keeps preferences and tune");
    }
}
if (args[1] != "regression") {
    const string absent = "# retained\r\n[Interface]\n Page = Setup \n[Future]\r\nMystery=keep";
    var (s, cfg) = Load(Bytes(absent)); s.SetPresentation("Advanced", "Driving");
    Check(File.ReadAllBytes(path).SequenceEqual(Bytes("# retained\r\n[Interface]\n Page = Driving \nView = Advanced\r\n[Future]\r\nMystery=keep")), "absent View inserts at section end, not across unrelated section");
    Check(cfg.Bind("Interface","View","Simple").Value == "Advanced" && cfg.Bind("Interface","Page","Setup").Value == "Driving", "absent key updates cached Bep entries");
    const string noSection = "# retained\r\n[Future]\r\nMystery=keep";
    (s, cfg) = Load(Bytes(noSection)); s.SetPresentation("Advanced", "Driving");
    Check(File.ReadAllBytes(path).SequenceEqual(Bytes(noSection + "\r\n\r\n[Interface]\r\nView = Advanced\r\nPage = Driving\r\n")), "missing Interface appends owned section without rewriting original prefix");
    Check(s.UiView == "Advanced" && s.UiPage == "Driving", "missing section commits both model values");
    (s, cfg) = Load(Bytes(raw));
    byte[] duplicate = Bytes(raw.Replace(" Page = Setup \n", " Page = Setup \nPage=Setup\r\n")); File.WriteAllBytes(path, duplicate);
    Check(Refuses(() => s.SetPresentation("Advanced", "Driving")), "late Page ambiguity throws after planning View");
    Check(File.ReadAllBytes(path).SequenceEqual(duplicate), "late Page refusal never publishes partial View");
    Check(OldModel(s,cfg), "failed transaction leaves model and Bep cached entries unchanged"); NoTemps();
    File.WriteAllBytes(path, Bytes(raw));
    Check(Refuses(() => ConfigLine.WriteBatch(path,"Interface",("View","Advanced"),("View","Simple"))), "duplicate requested key refuses");
    Check(File.ReadAllBytes(path).SequenceEqual(Bytes(raw)), "duplicate request leaves original unchanged");
    Check(Refuses(() => ConfigLine.WriteBatch(path,"Interface",("View","Advanced"),("Page","bad\nline"))), "invalid second request refuses");
    Check(File.ReadAllBytes(path).SequenceEqual(Bytes(raw)), "invalid second request cannot partially save first");
    (s, cfg) = Load(Bytes(raw));
    using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
        Check(Refuses(() => s.SetPresentation("Advanced","Driving")), "real locked replacement throws to caller");
        Check(File.ReadAllBytes(path).SequenceEqual(Bytes(raw)), "locked batch retains both original values");
        Check(OldModel(s,cfg), "locked failure leaves both model/cache values unchanged");
    }
    NoTemps();
    File.SetAttributes(path, FileAttributes.ReadOnly);
    try {
        Check(Refuses(() => s.SetPresentation("Advanced","Driving")) && File.ReadAllBytes(path).SequenceEqual(Bytes(raw)), "readonly batch refuses unchanged");
        Check(OldModel(s,cfg), "readonly failure leaves model/cache unchanged");
    } finally { File.SetAttributes(path, FileAttributes.Normal); }
    NoTemps();
    foreach (byte[] invalid in new[] { Bytes(raw + "\r\n[broken"), Bytes(raw + "\r\n[Interface]\r\nPage=Setup"), Bytes(raw).Concat(new byte[] { 255 }).ToArray(), new byte[] { 255,254,91,0,93,0 } }) {
        (s,cfg) = Load(Bytes(raw)); File.WriteAllBytes(path, invalid);
        Check(Refuses(() => s.SetPresentation("Advanced","Driving")) && File.ReadAllBytes(path).SequenceEqual(invalid), "malformed/ambiguous/unsupported source refuses without write");
        Check(OldModel(s,cfg), "invalid-source refusal keeps model/cache");
    }
    NoTemps();
    string first = Path.Combine(root,"nested","first.cfg");
    var fresh = new Settings(new ConfigFile(first,false)); fresh.SetPresentation("Advanced","Driving");
    string initial = File.ReadAllText(first);
    Check(initial.Contains("View = Advanced") && initial.Contains("Page = Driving") && !initial.Contains("ForceFeedback"), "first save creates parent and only owned presentation entries");
    var reloaded = new Settings(new ConfigFile(first,false));
    Check(reloaded.UiView == "Advanced" && reloaded.UiPage == "Driving", "fresh two-key file cold reads correctly");
    fresh.SetPresentation("Simple","Driving");
    Check(fresh.UiView == "Simple" && fresh.UiPage == "Setup", "Advanced-only page falls back to Setup in Simple");
    fresh.SetPresentation("typo","Controls");
    Check(fresh.UiView == "Simple" && fresh.UiPage == "Controls", "invalid view normalization retains common page");
    ConfigLineChecks.Run(Check); // Existing seven actual single-key preservation/refusal checks.
    File.WriteAllBytes(path, Bytes(raw));
    Task.WaitAll(
        Task.Run(() => { for (int i=0;i<20;i++) ConfigLine.Write(path,"Interface","View","Advanced"); }),
        Task.Run(() => { for (int i=0;i<20;i++) ConfigLine.Write(path,"Interface","Page","Driving"); }));
    Check(File.ReadAllBytes(path).SequenceEqual(Bytes(raw.Replace("View  = Simple","View  = Advanced").Replace("Page = Setup","Page = Driving"))), "single-key and batch callers share in-process serialization");
    (s,cfg) = Load(Bytes(raw)); string unrelated = path + ".tmp-unrelated"; File.WriteAllText(unrelated,"keep sibling");
    s.SetPresentation("Advanced","Driving");
    Check(File.ReadAllText(unrelated) == "keep sibling", "batch does not reclaim unrelated temp");
    Check(!Directory.GetFiles(root).Any(p => p != unrelated && Path.GetFileName(p).Contains(".tmp-",StringComparison.Ordinal)), "only unrelated temp remains");
}
Console.WriteLine(JsonSerializer.Serialize(new { pass = failures == 0, checks, failures, mode = args[1], realBepConfig = true, devices = false }));
return failures == 0 ? 0 : 1;
