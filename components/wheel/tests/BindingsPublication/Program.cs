using System.Text;
using System.Text.Json;
using WodenRallyEdge.Core;

if (args.Length != 2) return 2;
string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
int checks = 0, failures = 0;
void Check(bool pass, string label) { checks++; if (!pass) failures++; Console.Error.WriteLine((pass ? "ok " : "FAIL ") + label); }
bool Refuses(Action save) { try { save(); return false; } catch (IOException) { return true; } catch (UnauthorizedAccessException) { return true; } }
var axis = new AxisBinding(Guid.Parse("12345678-1234-1234-1234-123456789abc"), 5, new(0,65535));
var beforeBinding = new Bindings { Brake = axis };
var afterBinding = new Bindings { Brake = axis with { Calibration = axis.Calibration with { Deadzone = .03 } } };
string path = Path.Combine(root, "bindings.json");
beforeBinding.Save(path); byte[] original = File.ReadAllBytes(path);
byte[] priorBackup = Encoding.UTF8.GetBytes("prior valuable backup\r\n");
File.WriteAllBytes(path + ".bak", priorBackup);
afterBinding.Save(path);
Check(File.ReadAllBytes(path + ".bak").SequenceEqual(priorBackup), "full save never overwrites a prior backup");
Check(Bindings.Load(path).Brake == afterBinding.Brake, "full save publishes the requested binding");
Check(!Directory.GetFiles(root).Any(p => Path.GetFileName(p).Contains(".tmp-", StringComparison.Ordinal)), "successful save leaves no owned temp");

#if !BASELINE
if (args[1] != "regression") {
    string Suffix(string p) => Directory.GetFiles(Path.GetDirectoryName(p)!).Single(f => Path.GetFileName(f).StartsWith(Path.GetFileName(p) + ".bak.", StringComparison.Ordinal));
    Check(File.ReadAllBytes(Suffix(path)).SequenceEqual(original), "suffixed backup retains exact pre-save bytes");
    string first = Path.Combine(root, "first.json");
    beforeBinding.SaveNew(first);
    Check(Bindings.Load(first).Brake == axis, "creation-only path makes a readable first file");
    Check(!File.Exists(first + ".bak"), "first creation needs no previous-file backup");
    Check(File.ReadAllBytes(first)[0] == (byte)'{', "creation retains prior UTF8-without-BOM serializer contract");
    byte[] rival = File.ReadAllBytes(first);
    Check(Refuses(() => afterBinding.SaveNew(first)), "creation-only publication refuses a rival file");
    Check(File.ReadAllBytes(first).SequenceEqual(rival), "refused rival bytes remain exact");
    Check(!File.Exists(first + ".bak"), "refused creation adds no backup");
    string directory = Path.Combine(root, "directory-target"); Directory.CreateDirectory(directory);
    Check(Refuses(() => beforeBinding.SaveNew(directory)) && Directory.Exists(directory), "directory target refuses without replacement");
    Check(Directory.GetFileSystemEntries(directory).Length == 0, "directory target receives no staged data");
    string deadzone = Path.Combine(root, "new-deadzone.json");
    afterBinding.SaveDeadzone(deadzone, "Brake", axis);
    Check(Bindings.Load(deadzone).Brake == afterBinding.Brake, "actual missing-file deadzone fallback publishes its valid proposal");
    Check(!File.Exists(deadzone + ".bak"), "missing-file deadzone creates no legacy backup");
    string nested = Path.Combine(root, "nested", "normal-first.json"); beforeBinding.Save(nested);
    Check(Bindings.Load(nested).Brake == axis, "ordinary first Save retains directory creation/readback");
    Check(!File.Exists(nested + ".bak"), "ordinary first Save creates no spurious backup");
    byte[] firstBytes = File.ReadAllBytes(nested); afterBinding.Save(nested);
    Check(File.ReadAllBytes(nested + ".bak").SequenceEqual(firstBytes), "first overwrite retains legacy .bak bytes");
    byte[] secondBytes = File.ReadAllBytes(nested); beforeBinding.Save(nested);
    Check(File.ReadAllBytes(nested + ".bak").SequenceEqual(firstBytes), "later overwrite keeps earlier legacy backup");
    Check(File.ReadAllBytes(Suffix(nested)).SequenceEqual(secondBytes), "later overwrite makes a fresh exact backup");
    string same = Path.Combine(root, "same-value.json"); beforeBinding.Save(same);
    byte[] sameBefore = File.ReadAllBytes(same); beforeBinding.Save(same);
    Check(File.ReadAllBytes(same + ".bak").SequenceEqual(sameBefore), "ordinary same-value full Save retains legacy backup behavior");
    string locked = Path.Combine(root, "locked.json"); beforeBinding.Save(locked);
    byte[] lockedBefore = File.ReadAllBytes(locked);
    using (var held = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read)) {
        Check(Refuses(() => afterBinding.Save(locked)), "locked replacement reports failure");
        Check(File.ReadAllBytes(locked).SequenceEqual(lockedBefore), "locked replacement keeps exact original");
    }
    Check(!Directory.GetFiles(root).Any(p => Path.GetFileName(p).StartsWith("locked.json.tmp-", StringComparison.Ordinal)), "failed replacement cleans only its temp");
    string backupLocked = Path.Combine(root, "backup-locked.json"); beforeBinding.Save(backupLocked);
    byte[] backupOriginal = File.ReadAllBytes(backupLocked); File.WriteAllBytes(backupLocked + ".bak", priorBackup);
    using (var held = new FileStream(backupLocked + ".bak", FileMode.Open, FileAccess.Read, FileShare.Read)) afterBinding.Save(backupLocked);
    Check(File.ReadAllBytes(backupLocked + ".bak").SequenceEqual(priorBackup), "locked previous backup remains untouched");
    Check(Bindings.Load(backupLocked).Brake == afterBinding.Brake, "new backup suffix allows save without opening old backup for write");
    Check(File.ReadAllBytes(Suffix(backupLocked)).SequenceEqual(backupOriginal), "backup-lock route still retains exact replaced original");
    string readOnly = Path.Combine(root, "readonly.json"); beforeBinding.Save(readOnly);
    byte[] readOnlyBefore = File.ReadAllBytes(readOnly); File.SetAttributes(readOnly, FileAttributes.ReadOnly);
    try {
        Check(Refuses(() => afterBinding.Save(readOnly)), "readonly target refuses");
        Check(File.ReadAllBytes(readOnly).SequenceEqual(readOnlyBefore), "readonly bytes remain exact");
    } finally { File.SetAttributes(readOnly, FileAttributes.Normal); }
    string unrelated = path + ".tmp-unrelated"; File.WriteAllText(unrelated, "keep stale temp");
    beforeBinding.Save(path);
    Check(File.ReadAllText(unrelated) == "keep stale temp", "publisher never reclaims an unrelated pre-existing temp");
    Check(!Directory.GetFiles(root).Any(p => p != unrelated && Path.GetFileName(p).Contains(".tmp-", StringComparison.Ordinal)), "successful and failed saves close owned temps");
}
#endif
Console.WriteLine(JsonSerializer.Serialize(new { pass = failures == 0, checks, failures, mode = args[1], devices = false }));
return failures == 0 ? 0 : 1;
