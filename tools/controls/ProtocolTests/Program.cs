using System.Text;
using System.Text.Json;
using Woden.ControlsTesting;

int count = 0;
void Check(bool ok) { count++; if (!ok) throw new Exception("Check failed " + count); }
void Refuse(Action action) { count++; try { action(); } catch (InvalidDataException) { return; } catch (JsonException) { return; } throw new Exception("Accepted invalid case " + count); }
byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Protocol.Json);
var now = DateTimeOffset.Parse("2026-10-10T09:00:00Z");
string nonce = new('a', 32), hash = new('b', 64);
var cold = new ColdRequest(2, nonce, now.AddMinutes(1), hash, hash, hash, hash, hash, Path.GetFullPath("fixture-evidence"));
Check(Protocol.Admit(Bytes(cold), now, nonce) == cold);
foreach (var r in new[] { cold with { Schema=1 }, cold with { Nonce=new('0',32) }, cold with { Nonce="bad" },
    cold with { ExpiresUtc=now }, cold with { ExpiresUtc=now.AddMinutes(6) }, cold with { PluginSha256="bad" },
    cold with { CoreSha256="bad" }, cold with { ConfigSha256="bad" }, cold with { BindingsSha256="bad" },
    cold with { NativeSha256="bad" }, cold with { OutputDirectory="relative" } })
    Refuse(() => Protocol.Admit(Bytes(r), now, nonce));
Refuse(() => Protocol.Admit(Bytes(cold), now, new('c',32)));
Refuse(() => Protocol.Admit(new byte[8193], now, nonce));
Refuse(() => Protocol.Admit(Encoding.UTF8.GetBytes("null"), now, nonce));
Refuse(() => Protocol.Admit(Encoding.UTF8.GetBytes("{\"schema\":1,\"schema\":1}"), now, nonce));
var command = new RawRequest(1, nonce, "raw", "inject raw button 3 dev={11111111-1111-1111-1111-111111111111} value=1 ms=120");
Check(Protocol.Command(Bytes(command), nonce, 0, now.AddSeconds(1), now) == command);
foreach (var r in new[] { command with { Sequence=0 }, command with { Sequence=2 }, command with { Nonce=new('c',32) },
    command with { Operation="action" }, command with { Raw="inject action steer 1 ms=100" }, command with { Raw="inject raw x\nstop" },
    command with { Raw="inject raw "+new string('x',512) }, command with { Raw=null }, command with { Operation="status" } })
    Refuse(() => Protocol.Command(Bytes(r), nonce, 0, now.AddSeconds(1), now));
Refuse(() => Protocol.Command(Bytes(command), nonce, 1, now.AddSeconds(1), now));
Refuse(() => Protocol.Command(Bytes(command), nonce, 0, now, now));
Refuse(() => Protocol.Command(Bytes(command with { Sequence=1025 }), nonce, 1024, now.AddSeconds(1), now));
foreach (var operation in new[] { "status", "stop" })
    Check(Protocol.Command(Bytes(command with { Operation=operation, Raw=null }), nonce, 0, now.AddSeconds(1), now).Operation == operation);
Console.WriteLine($"PASS: {count} controls protocol checks; no engine, file mutation, device or native calls.");

int configs=0;
byte[] Ini(string text)=>Encoding.UTF8.GetBytes(text);
string config="[ForceFeedback]\nEnabled = false\nStrengthPercent = 50\n\n[Wheel]\nEnabled = true\n";
string configHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Ini(config)));
void Good(string after,int additions) { configs++; if(ConfigContract.Verify(Ini(config),Ini(after),configHash).Length!=additions) throw new Exception("Wrong default count."); }
void Bad(string after) { configs++; Refuse(()=>ConfigContract.Verify(Ini(config),Ini(after),configHash)); }
Good(config,0);
Good("# rewritten header\r\n[Wheel]\r\nEnabled=true\r\n[ForceFeedback]\r\nStrengthPercent=50\r\nEnabled=false\r\nCrashEnabled=true\r\nGripLoadRatio=2\r\nGripSmoothing=0.2\r\n[Display]\r\nShowFrameRate=false\r\n",4);
Bad(config.Replace("Enabled = false","Enabled = true"));
Bad(config.Replace("StrengthPercent = 50","StrengthPercent = 51"));
Bad(config.Replace("StrengthPercent = 50\n",""));
Bad(config+"NewKey = true\n");
Bad(config.Replace("Enabled = false","Enabled = false\nCrashEnabled = false"));
Bad(config.Replace("Enabled = false","Enabled = false\nGripLoadRatio = 3"));
Bad(config.Replace("Enabled = false","Enabled = false\nEnabled = false"));
Bad(config+"[Wheel]\nEnabled = true\n");
Bad(config+"invalid line\n");
configs++;Refuse(()=>ConfigContract.Verify(Ini(config+"# tampered\n"),Ini(config),configHash));
configs++;Refuse(()=>ConfigContract.Parse(Ini("[Empty]\n")));
Console.WriteLine($"PASS: {configs} config admission cases: pinned snapshot, unchanged values, exact default additions and refusals.");
