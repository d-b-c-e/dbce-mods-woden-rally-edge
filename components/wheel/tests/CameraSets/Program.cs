using System.Text.Json;
using System.Text;
using BepInEx.Configuration;
using WodenRallyEdge;
using WodenRallyEdge.Core;

var results = new List<object>();
int failures = 0;
void Check(string assertion, bool pass)
{
    results.Add(new { assertion, pass });
    if (!pass) failures++;
}

// The old production helper was called by startup with the persisted CompareView.
// A player setting from the retired comparison cycle must no longer disable the
// normal corrected renderer. Shift+F7 still has a separate in-memory cycle.
Check("retired Original pick cannot disable next-start correction", TripleGeometry.FromSaved(1) == null);
Check("retired Angle pick cannot suppress next-start curve", TripleGeometry.FromSaved(2) == null);
Check("retired Angle+curve pick is not session developer state", TripleGeometry.FromSaved(3) == null);
Check("normal flat output starts corrected", TripleGeometry.CurrentStep(null, false) == TripleGeometry.AngleStep);
Check("normal curved output starts fully corrected", TripleGeometry.CurrentStep(null, true) == TripleGeometry.AngleCurveStep);
Check("developer comparison retains original as a session step", TripleGeometry.NextStep(null, true) == TripleGeometry.OriginalStep);

Check("chase Realistic choice is independent of mounted Game choice", !TripleGeometry.ShowsGameFov(null, true, false, MountedView.Stock));
double chaseEye = TripleGeometry.EyeDistanceForView(398.5, 660, true, false, MountedView.Stock, 70, null);
Check("chase Realistic projection uses the measured starting eye", Math.Abs(chaseEye - 660) < 1e-9);

var physical = CurvedRig.Compute(708.4, 398.5, 660, 70, 8, 1500);
Check("measured eye distance stays at the panel-edge plane", Math.Abs(physical.Warps![1].Panel.LowerLeft.Z + 660) < 1e-8);
var keys = CameraTuning.DefaultKeys();
Check("numpad plus narrows the field of view", keys["Camera narrower"] == "NumpadPlus");
Check("numpad minus widens the field of view", keys["Camera wider"] == "NumpadMinus");
Check("narrowing actually zooms in", CameraTuning.Adjust(CameraPose.Bonnet, "Camera narrower").Fov < CameraPose.Bonnet.Fov);

// Actual shared warp: changing the render eye must not change the physical-seat
// rays or UV samples. The source surface, rather than the measured panel, moves.
foreach (double renderEye in new[] { 300d, 660d, 1200d })
{
    var rendered = CurvedRig.Compute(708.4, 398.5, renderEye, 70, 8, 1500, 660);
    double error = 0;
    for (int panel = 0; panel < 3; panel++)
    for (int u = 0; u <= 10; u++)
    for (int v = 0; v <= 6; v++)
    {
        physical.Warps![panel].SourceUv(u / 10d, v / 6d, out double expectedU, out double expectedV);
        rendered.Warps![panel].SourceUv(u / 10d, v / 6d, out double actualU, out double actualV);
        error = Math.Max(error, Math.Max(Math.Abs(actualU - expectedU), Math.Abs(actualV - expectedV)));
    }
    Check("virtual eye " + renderEye + " keeps all231 measured-seat UV samples", error < 1e-12);
    Check("virtual eye " + renderEye + " moves source only", Math.Abs(rendered.Warps![1].Panel.LowerLeft.Z + 660) < 1e-9 &&
        Math.Abs(rendered.Warps[1].SourceSurface.LowerLeft.Z - physical.Warps![1].SourceSurface.LowerLeft.Z - (660 - renderEye)) < 1e-8);
}
Check("Realistic FOV offset changes the rendered lens", TripleGeometry.EyeDistanceForView(398.5, 660, false, false, MountedView.Bumper, 70, 70, offsetDegrees: 8) < 660);

// This is the same lens ownership helper used by MountedCamera's native prefix
// and postfix. Exercise a speed-varying native smoothing input for500frames.
var lens = new CameraLens();
float shown = 70, native = 70;
double smoothingError = 0, peekError = 0;
for (int frame = 0; frame < 500; frame++)
{
    float restored = lens.Remove(shown);
    smoothingError = Math.Max(smoothingError, Math.Abs(restored - native));
    native = restored + ((frame < 250 ? 90 : 55) - restored) * .1f;
    shown = CameraLens.Fov(native, 6);
    lens.Applied(native, shown);
    peekError = Math.Max(peekError, Math.Abs(lens.Peek(shown) - native));
}
Check("500 frame lens peek does not consume ownership", peekError < .0001);
Check("native smoothing receives no accumulated camera offset", smoothingError < .0001);
Check("foreign lens writer is retained", lens.Remove(41) == 41);
Check("camera name and stock preset remain distinct", new CameraSlot(MountedView.Stock, 0).Key != new CameraSlot(MountedView.Stock, 1).Key);
foreach (string key in new[] { "Bonnet", "Bumper", "Chase0", "Chase2", "Isometric1" })
    Check("saved camera identity roundtrips " + key, CameraSlot.TryParse(key, out var slot) && slot.Key == key);
Check("unavailable invented camera is refused", !CameraSlot.TryParse("Cockpit", out _));
Check("negative stock camera is refused", !CameraSlot.TryParse("Chase-1", out _));
Check("late preset array keeps restoration pending", new CameraSlot(MountedView.Stock, 2).RestorePreset(0, 0) == null);
Check("same saved camera restores once the array is ready", new CameraSlot(MountedView.Stock, 2).RestorePreset(0, 3) == 2);
Check("late extra mount waits for a legal underlying preset", new CameraSlot(MountedView.Bumper).RestorePreset(-1, 3) == null);
Check("extra mount uses the ready native preset", new CameraSlot(MountedView.Bumper).RestorePreset(1, 3) == 1);

if (args.Length != 1) throw new InvalidOperationException("One fresh private fixture directory is required.");
string root = Path.GetFullPath(args[0]);
if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Fixture directory already exists.");
Directory.CreateDirectory(root);
var bonnet = new CameraSlot(MountedView.Bonnet);
var bumper = new CameraSlot(MountedView.Bumper);
var chase0 = new CameraSlot(MountedView.Stock, 0);
var chase1 = new CameraSlot(MountedView.Stock, 1);
ConfigFile Config(string path) { var cfg = new ConfigFile(path, false); cfg.SaveOnConfigSet = false; return cfg; }
CameraFramingStore Open(string path) => new(Config(path));
byte[] Bytes(string path) => File.ReadAllBytes(path);
bool Same(byte[] a, byte[] b) => a.SequenceEqual(b);
bool Refuses(Action action)
{
    try { action(); return false; }
    catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException) { return true; }
}
string fresh = Path.Combine(root, "fresh.cfg");
var initial = Open(fresh);
Check("fresh bonnet starts Game", !initial.Realistic(bonnet));
Check("fresh bumper starts Realistic", initial.Realistic(bumper));
Check("fresh chase starts Game", !initial.Realistic(chase0));
Check("reading defaults does not create a settings file", !File.Exists(fresh));

// The camera-set store no longer derives from MatchGameFov or ChaseUsesGameFov: such a file gets the defaults and keeps its bytes.
foreach (bool legacy in new[] { false, true })
foreach (bool oldChase in new[] { false, true })
{
    string path = Path.Combine(root, "global-fov-" + legacy + "-" + oldChase + ".cfg");
    File.WriteAllText(path, "[Triple]\r\nMatchGameFov = " + legacy + "\r\nChaseUsesGameFov = " + oldChase + "\r\n");
    byte[] before = Bytes(path);
    var store = Open(path);
    Check("camera sets ignore the global FOV keys " + legacy + oldChase, !store.Realistic(bonnet) && store.Realistic(bumper) && !store.Realistic(chase0));
    Check("the global FOV keys keep all bytes " + legacy + oldChase, Same(before, Bytes(path)));
}

string cfgPath = Path.Combine(root, "camera.cfg");
string text = "# keep café\r\n[Triple]\nBonnetRealistic  =  false  \r\nBumperRealistic = true\nChaseRealistic = false\r\nCompareView = 1\nUnknown = keep\r\n[FFB]\r\nEnabled = false\r\nStrength = 37";
byte[] original = new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes(text)).ToArray();
File.WriteAllBytes(cfgPath, original);
var cameras = Open(cfgPath);
int? developer = cameras.Press(bonnet, true, null, true);
developer = cameras.Press(bonnet, true, developer, true);
developer = cameras.Press(bonnet, true, developer, true);
Check("Shift F7 uses all three transient developer steps", developer == TripleGeometry.AngleCurveStep);
Check("Shift F7 never changes any settings byte", Same(original, Bytes(cfgPath)));
Check("F7 returns to normal corrected output", cameras.Press(bonnet, false, developer, true) == null);
byte[] expectedChoice = new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes(text.Replace("BonnetRealistic  =  false  ", "BonnetRealistic  =  true  "))).ToArray();
Check("F7 changes only its own scalar and keeps BOM mixed endings and FFB", Same(expectedChoice, Bytes(cfgPath)));
Check("bonnet choice does not change bumper or chase", cameras.Realistic(bonnet) && cameras.Realistic(bumper) && !cameras.Realistic(chase0));
cameras.Press(chase0, false, null, false);
Check("each stock preset remembers its own set", cameras.Realistic(chase0) && !cameras.Realistic(chase1));
Check("choice survives a real configuration restart", Open(cfgPath).Realistic(bonnet) && Open(cfgPath).Realistic(chase0));

cameras.SetFov(bonnet, true, 34, 39);
Check("FOV is saved as an offset on its calculated start", cameras.FovOffset(bonnet, true) == 5);
Check("other set keeps its own FOV", cameras.FovOffset(bonnet, false) == 0);
Check("FOV offset survives restart and is shared across cars", Open(cfgPath).FovOffset(bonnet, true) == 5);
var custom = new CameraOffset(12, -4, 20, 150);
cameras.SetPosition(bonnet, 1, true, custom);
Check("position tweaks have no arbitrary editor bounds", cameras.Position(bonnet, 1, true) == custom);
Check("positions are separate for each car", cameras.Position(bonnet, 2, true) == default);
Check("positions are separate for each set", cameras.Position(bonnet, 1, false) == default);
cameras.SetPosition(bonnet, 2, true, new(1, 2, 3, 4));
cameras.SetFov(bonnet, false, 70, 74);
cameras.Reset(bonnet, 1, true);
Check("reset returns active position and FOV to calculation", cameras.Position(bonnet, 1, true) == default && cameras.FovOffset(bonnet, true) == 0);
Check("reset keeps the other car and other set", cameras.Position(bonnet, 2, true) == new CameraOffset(1, 2, 3, 4) && cameras.FovOffset(bonnet, false) == 4);
cameras.Remember(chase1);
Check("last camera survives a configuration restart", Open(cfgPath).LastCamera == chase1.Key);

byte[] beforeInvalid = Bytes(cfgPath);
Check("nonfinite position is refused", Refuses(() => cameras.SetPosition(bonnet, 1, true, new(float.NaN, 0, 0, 0))));
Check("invalid FOV is refused", Refuses(() => cameras.SetFov(bonnet, true, 34, double.NaN)));
Check("refused values leave the file exact", Same(beforeInvalid, Bytes(cfgPath)));
bool previousPick = cameras.Realistic(bonnet);
using (var locked = new FileStream(cfgPath, FileMode.Open, FileAccess.Read, FileShare.Read))
{
    Check("locked replacement refuses", Refuses(() => cameras.SetRealistic(bonnet, !previousPick)));
    Check("failed save retains cache and original bytes", cameras.Realistic(bonnet) == previousPick && Same(beforeInvalid, Bytes(cfgPath)));
}
Check("failed save cleans only its owned temporary", Directory.GetFiles(root, "camera.cfg.tmp-*").Length == 0);
File.AppendAllText(cfgPath, "\r\n# external keeper\r\n");
cameras.SetRealistic(bonnet, !previousPick);
Check("unrelated external edits survive the next owned save", File.ReadAllText(cfgPath).EndsWith("# external keeper\r\n", StringComparison.Ordinal));
string malformed = Path.Combine(root, "malformed.cfg");
File.WriteAllText(malformed, "[Triple]\nBonnetRealistic = false\nBonnetRealistic = true\n");
byte[] malformedBytes = Bytes(malformed);
Check("ambiguous camera choice refuses", Refuses(() => Open(malformed)));
Check("ambiguous input is not rewritten", Same(malformedBytes, Bytes(malformed)));

// Actual Bindings.Load migration: only untouched defaults move to the new
// direction. Loading never republishes JSON or replaces user bindings.
var oldKeys = CameraTuning.DefaultKeys(); oldKeys["Camera wider"] = "NumpadPlus"; oldKeys["Camera narrower"] = "NumpadMinus";
string bindingsPath = Path.Combine(root, "bindings.json");
File.WriteAllText(bindingsPath, JsonSerializer.Serialize(new Bindings { CameraKeys = new(oldKeys) }));
byte[] oldBindingBytes = Bytes(bindingsPath);
var loaded = Bindings.Load(bindingsPath);
Check("untouched old camera defaults migrate in memory", loaded.CameraKeysMigrated && loaded.CameraKeys["Camera narrower"] == "NumpadPlus");
Check("loading migration preserves the original JSON", Same(oldBindingBytes, Bytes(bindingsPath)));
oldKeys["Camera up"] = "A";
File.WriteAllText(bindingsPath, JsonSerializer.Serialize(new Bindings { CameraKeys = new(oldKeys) }));
loaded = Bindings.Load(bindingsPath);
Check("custom keyboard layout stays exact", loaded.CameraKeys["Camera up"] == "A" && loaded.CameraKeys["Camera wider"] == "NumpadPlus");
Check("on-screen key hint uses the saved custom key", CameraTuning.KeyHint(loaded, "Camera up") == "A");
oldKeys["Camera up"] = CameraTuning.DefaultKeys()["Camera up"];
File.WriteAllText(bindingsPath, JsonSerializer.Serialize(new Bindings { CameraKeys = new(oldKeys), Buttons = new() { ["Camera wider"] = new(Guid.NewGuid(), 5) } }));
loaded = Bindings.Load(bindingsPath);
Check("custom wheel camera binding stays exact", !loaded.CameraKeysMigrated && loaded.CameraKeys["Camera wider"] == "NumpadPlus" && loaded.Buttons.ContainsKey("Camera wider"));

Console.WriteLine(JsonSerializer.Serialize(new
{
    schema = "woden.camera-sets-regression@1",
    pass = failures == 0,
    checks = results.Count,
    failures,
    productionFunctions = true,
    engineOrDeviceLoaded = false,
    assertions = results
}, new JsonSerializerOptions { WriteIndented = true }));
return failures == 0 ? 0 : 1;
