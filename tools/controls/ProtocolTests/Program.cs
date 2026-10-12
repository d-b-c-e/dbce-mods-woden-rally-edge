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

var shim=cold with {Schema=3,Mode="shim-admission",ControlsSha256=hash,ShimSha256=hash};
Check(Protocol.Admit(Bytes(shim),now,nonce)==shim);
foreach(var r in new[]{shim with{Schema=2},shim with{Mode="raw"},shim with{Mode="anything"},shim with{ControlsSha256=null},shim with{ControlsSha256="bad"},shim with{ShimSha256=null},shim with{ShimSha256="bad"},cold with{ControlsSha256=hash}})
    Refuse(()=>Protocol.Admit(Bytes(r),now,nonce));
Refuse(()=>Protocol.Command(Bytes(command),nonce,0,now.AddSeconds(1),now,true));
foreach(var op in new[]{"status","stop","panel","shot"}) {
    var c=command with{Operation=op,Raw=null};
    Check(Protocol.Command(Bytes(c),nonce,0,now.AddSeconds(1),now,true)==c);
    if(op is "panel" or "shot") Refuse(()=>Protocol.Command(Bytes(c),nonce,0,now.AddSeconds(1),now));
}
Console.WriteLine($"PASS: {count} total protocol checks including isolated shim admission and raw refusal.");
var trial=shim with{Schema=4,Mode="shim-menu-trial"};
Check(Protocol.Admit(Bytes(trial),now,nonce)==trial);
foreach(var r in new[]{trial with{Schema=3},trial with{Schema=2},trial with{Mode="shim-admission"},trial with{ShimSha256=null}})
    Refuse(()=>Protocol.Admit(Bytes(r),now,nonce));
foreach(var op in new[]{"raw","panel","shot","action","start"})
    Refuse(()=>Protocol.Command(Bytes(command with{Operation=op,Raw=null}),nonce,0,now.AddSeconds(1),now,true,true));
foreach(var op in new[]{"status","stop"})Check(Protocol.Command(Bytes(command with{Operation=op,Raw=null}),nonce,0,now.AddSeconds(1),now,true,true).Operation==op);
Console.WriteLine($"PASS: {count} controls protocol checks including fixed-route schema 4 and operator-injection refusal.");
var car=shim with{Schema=5,Mode="shim-car-trial",StageReference=Path.GetFullPath("sealed-reference"),WorkloadDirectory=Path.GetFullPath("original-workload")};
Check(Protocol.Admit(Bytes(car),now,nonce)==car);
foreach(var r in new[]{car with{Schema=4},car with{Mode="shim-menu-trial"},car with{StageReference=null},car with{WorkloadDirectory="relative"},trial with{StageReference=car.StageReference}})
    Refuse(()=>Protocol.Admit(Bytes(r),now,nonce));
Check(Protocol.LatchedTrial(car)&&Protocol.ShimAdmission(car)&&!Protocol.MenuTrial(car));
Console.WriteLine($"PASS: {count} protocol checks including isolated schema 5 car trial.");

Dictionary<string,double> NativeValues(string action="neutral",double expected=0)=>new(){
    ["game.automatic"]=0,["game.locked"]=0,["game.paused"]=0,["game.respawning"]=0,["game.replay"]=0,
    ["controls.steer"]=action=="steer"?expected:0,["game.steer"]=action=="steer"?expected:0,
    ["controls.throttle"]=action=="throttle"?expected:0,["game.throttle"]=action=="throttle"?expected:0,
    ["controls.brake"]=action=="brake"?expected:0,["game.brake"]=action=="brake"?expected:0,
    ["game.handbrake"]=action=="handbrake"&&expected>0?1:0};
CarPlateau Observe(string id,double expected,Action<Dictionary<string,double>>? mutate=null,double gap=.025,int rows=8){var p=new CarPlateau();for(int i=0;i<rows;i++){var values=NativeValues(id,expected);mutate?.Invoke(values);p.Add(i*gap,id,expected,values);}return p;}
foreach(string id in new[]{"steer","throttle","brake","handbrake"})Check(Observe(id,.5).Passed);
foreach(string key in new[]{"controls.brake","game.brake","game.automatic","game.locked","game.paused","game.respawning","game.replay"})
    Check(!Observe("brake",.5,v=>v.Remove(key)).Passed);
Check(!Observe("brake",.5,v=>v["game.brake"]=0).Passed);
Check(!Observe("throttle",.5,v=>v["game.throttle"]=double.NaN).Passed);
Check(!Observe("steer",.5,gap:.15).Passed);
Check(!Observe("steer",.5,rows:4).Passed);
Check(!Observe("steer",.5,v=>v["game.automatic"]=1).Passed);
Console.WriteLine($"PASS: {count} total protocol/native plateau checks; requested action values alone cannot pass.");
if(args.Length==4) {
    var controls=File.ReadAllBytes(args[0]);var profile=File.ReadAllBytes(args[1]);var workload=File.ReadAllBytes(args[2]);
    var plan=CarWorkload.Read(controls,profile,workload);
    Check(plan.Pulses.Length==10 && plan.Neutral.Command.Split('\n').Length==6 && plan.Neutral.Command.Contains("value=1743"));
    foreach(int which in new[]{0,1,2}) {var all=new[]{controls.ToArray(),profile.ToArray(),workload.ToArray()};all[which][10]^=1;Refuse(()=>CarWorkload.Read(all[0],all[1],all[2]));}
    using var output=new StreamWriter(new FileStream(args[3],FileMode.CreateNew,FileAccess.Write));
    foreach(var p in new[]{plan.Neutral}.Concat(plan.Pulses.SelectMany(p=>new[]{p,plan.Neutral}))) {
        output.WriteLine(p.Action+" "+p.Expected.ToString("R",System.Globalization.CultureInfo.InvariantCulture));output.WriteLine(p.Command);output.WriteLine("---");
    }
    Console.WriteLine($"PASS: immutable independent sources and six-action raw batch fixture exported; {count} checks.");
}

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
