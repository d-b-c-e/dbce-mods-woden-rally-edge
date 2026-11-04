using System.Text;
using System.Text.Json;
using WodenRallyEdge.Core;

if (args.Length != 2) return 2;
string directory = Path.GetFullPath(args[0]); Directory.CreateDirectory(directory);
string path = Path.Combine(directory, "bindings.json");
int checks = 0, failures = 0;
void Check(bool pass, string label) { checks++; if (!pass) failures++; Console.Error.WriteLine((pass ? "ok " : "FAIL ") + label); }
var previous = new AxisBinding(Guid.Parse("12345678-1234-1234-1234-123456789abc"), 5, new(0,65535));
var edited = new Bindings { Brake = previous with { Calibration = previous.Calibration with { Deadzone = .03 } } };
string original = "{\r\n  \"Version\":1,\n  \"Brake\" : {\"DeviceGuid\":\"12345678-1234-1234-1234-123456789abc\",\"Axis\":5,\"Calibration\":{\"Rest\":0,\"End\":65535,\"Centre\":null,\"Deadzone\" : 0e0,\"Extension\":{\"Deadzone\":0}},\"Inverted\":false},\r\n  \"Buttons\":{},\"CameraKeys\":{\"Camera up\":\"Numpad8\"},\"Extension\":{\"note\":\"café\",\"Deadzone\":0}\n}";
byte[] Bytes(string text, bool bom = false) => (bom ? new byte[] { 239,187,191 } : Array.Empty<byte>()).Concat(Encoding.UTF8.GetBytes(text)).ToArray();
void ClearBackups() { foreach (string backup in Directory.GetFiles(directory, "bindings.json.bak*")) File.Delete(backup); }
void NoTemps() => Check(Directory.GetFiles(directory, "*.tmp-*").Length == 0, "only owned temp cleaned");
bool Refuses(Action save) { try { save(); return false; } catch (IOException) { return true; } catch (JsonException) { return true; } }

foreach (var variant in args[1] == "regression" ? new[] { 0 } : new[] { 0, 1, 2, 3 }) {
    string text = variant == 2 ? original.Replace("\r\n", "\n") : variant == 3 ? original.Replace("\"Deadzone\" :", "\"D\\u0065adzone\" :") : original;
    byte[] before = Bytes(text, variant == 1), after = Bytes(text.Replace(" : 0e0", " : 0.03"), variant == 1);
    ClearBackups(); File.WriteAllBytes(path, before);
    edited.SaveDeadzone(path, "Brake", previous);
    Check(File.ReadAllBytes(path).SequenceEqual(after), "existing deadzone changes only its numeric token/BOM/EOL/EOF");
    Check(File.ReadAllBytes(path + ".bak").SequenceEqual(before), "first legacy backup is exact original");
    NoTemps();
}
if (args[1] != "regression") {
    byte[] before = Bytes(original);
    ClearBackups(); File.WriteAllBytes(path, before); File.WriteAllText(path + ".bak", "prior backup");
    edited.SaveDeadzone(path, "Brake", previous);
    Check(File.ReadAllText(path + ".bak") == "prior backup", "existing backup never overwritten");
    Check(Directory.GetFiles(directory, "bindings.json.bak.*").Single() is string backup && File.ReadAllBytes(backup).SequenceEqual(before), "new backup suffix retains exact original");
    var current = edited.Brake!;
    long ticks = File.GetLastWriteTimeUtc(path).Ticks;
    edited.SaveDeadzone(path, "Brake", current);
    Check(File.GetLastWriteTimeUtc(path).Ticks == ticks, "no-op retains timestamp and lexical representation");
    Check(Directory.GetFiles(directory, "bindings.json.bak*").Length == 2, "no-op creates no backup");
    string missing = original.Replace("\"Deadzone\" : 0e0,", "");
    File.WriteAllBytes(path, Bytes(missing)); ClearBackups(); edited.SaveDeadzone(path, "Brake", previous);
    string expected = missing.Replace("\"Extension\":{\"Deadzone\":0}},", "\"Extension\":{\"Deadzone\":0},\"Deadzone\":0.03},");
    Check(File.ReadAllBytes(path).SequenceEqual(Bytes(expected)), "missing owned property inserts without rewriting other bytes");
    var invalid = new[] {
        original.Replace("\"Version\":1,", "\"brake\":{},\"Version\":1,"),
        original.Replace("\"Calibration\":{", "\"calibration\":{},\"Calibration\":{"),
        original.Replace("\"Deadzone\" : 0e0,", "\"Deadzone\":0,\"deadzone\":0,"),
        original + " trailing", original.Replace("\"Axis\":5", "\"Axis\":4"),
        original.Replace("\"End\":65535", "\"End\":60000"),
        original.Replace("\"Brake\" :", "\"Throttle\" :")
    };
    foreach (string raw in invalid) {
        byte[] bytes = Bytes(raw); File.WriteAllBytes(path, bytes); ClearBackups();
        Check(Refuses(() => edited.SaveDeadzone(path, "Brake", previous)), "ambiguous/malformed/stale saved binding refuses");
        Check(File.ReadAllBytes(path).SequenceEqual(bytes), "refused file byte exact"); NoTemps();
    }
    File.WriteAllBytes(path, before); ClearBackups();
    var wrong = new Bindings { Brake = previous with { Axis = 4, Calibration = previous.Calibration with { Deadzone = .03 } } };
    Check(Refuses(() => wrong.SaveDeadzone(path, "Brake", previous)) && File.ReadAllBytes(path).SequenceEqual(before), "deadzone writer cannot change binding identity");
    using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
        Check(Refuses(() => edited.SaveDeadzone(path, "Brake", previous)), "locked destination refuses");
        Check(File.ReadAllBytes(path).SequenceEqual(before), "locked save retains original");
    }
    NoTemps();
    File.WriteAllBytes(path, new byte[] { 255,254,123,0,125,0 });
    Check(Refuses(() => edited.SaveDeadzone(path, "Brake", previous)) && File.ReadAllBytes(path).SequenceEqual(new byte[] { 255,254,123,0,125,0 }), "unsupported UTF16 refuses unchanged");
    File.WriteAllBytes(path, new byte[1024 * 1024 + 1]);
    Check(Refuses(() => edited.SaveDeadzone(path, "Brake", previous)), "oversized source refuses");
}
Console.WriteLine(JsonSerializer.Serialize(new { pass = failures == 0, checks, failures, mode = args[1], devices = false }));
return failures == 0 ? 0 : 1;
