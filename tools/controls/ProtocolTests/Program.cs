using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
var forward=car with{Schema=6,Mode="shim-force-capture"};Check(Protocol.Admit(Bytes(forward),now,nonce)==forward);
foreach(var r in new[]{forward with{Schema=5},forward with{Mode="shim-car-trial"},car with{Schema=6},forward with{WorkloadDirectory=null}})
    Refuse(()=>Protocol.Admit(Bytes(r),now,nonce));
Check(Protocol.ForceCapture(forward)&&Protocol.CarTrial(forward)&&Protocol.LatchedTrial(forward)&&!Protocol.MenuTrial(forward));
var hud=forward with{Mode="shim-hud-ordinary"};
Check(Protocol.Admit(Bytes(hud),now,nonce)==hud && Protocol.HudOrdinary(hud) && Protocol.ForceCapture(hud) && Protocol.CarTrial(hud) && Protocol.LatchedTrial(hud));
foreach(var r in new[]{hud with{Schema=5},hud with{Schema=7},hud with{WorkloadDirectory=null},hud with{ControlsSha256=null}})
    Refuse(()=>Protocol.Admit(Bytes(r),now,nonce));
var hudStop=shim with{Schema=8,Mode="shim-hud-stop-only"};
Check(Protocol.Admit(Bytes(hudStop),now,nonce)==hudStop && Protocol.HudStopOnly(hudStop) &&
    Protocol.RecordingTrial(hudStop) && Protocol.ShimAdmission(hudStop) && Protocol.LatchedTrial(hudStop) &&
    !Protocol.CarTrial(hudStop) && !Protocol.ForceCapture(hudStop));
foreach(var r in new[]{hudStop with{Schema=7},hudStop with{Schema=6},hudStop with{StageReference=car.StageReference},
    hudStop with{WorkloadDirectory=car.WorkloadDirectory},hudStop with{ControlsSha256=null},
    hudStop with{ProfileSha256=hash}})Refuse(()=>Protocol.Admit(Bytes(r),now,nonce));
foreach(var op in new[]{"raw","panel","shot"})Refuse(()=>Protocol.Command(Bytes(command with{Operation=op,Raw=null}),nonce,0,now.AddSeconds(1),now,true,Protocol.LatchedTrial(hudStop)));
foreach(var op in new[]{"status","stop"})Check(Protocol.Command(Bytes(command with{Operation=op,Raw=null}),nonce,0,now.AddSeconds(1),now,true,Protocol.LatchedTrial(hudStop)).Operation==op);
var appliedCar=car with{Schema=7,Mode="shim-applied-car-trial",ProfileSha256=hash,WorkloadSha256=hash,ProfileRevision=hash,ApplyReadbackSha256=hash};
Check(Protocol.Admit(Bytes(appliedCar),now,nonce)==appliedCar && Protocol.DynamicCarTrial(appliedCar) &&
    Protocol.CarTrial(appliedCar) && Protocol.ShimAdmission(appliedCar) && Protocol.LatchedTrial(appliedCar) && !Protocol.ForceCapture(appliedCar));
var appliedForward=appliedCar with{Mode="shim-applied-forward-capture"};
Check(Protocol.Admit(Bytes(appliedForward),now,nonce)==appliedForward && Protocol.AppliedForwardCapture(appliedForward) &&
    Protocol.DynamicCarTrial(appliedForward) && Protocol.ForceCapture(appliedForward) && Protocol.CarTrial(appliedForward));
foreach(var r in new[]{appliedForward with{Schema=6},appliedForward with{ProfileSha256=null},appliedForward with{ApplyReadbackSha256=null}})
    Refuse(()=>Protocol.Admit(Bytes(r),now,nonce));
var ordinaryForward=appliedForward with{Mode="shim-applied-forward-ordinary"};
Check(Protocol.Admit(Bytes(ordinaryForward),now,nonce)==ordinaryForward && Protocol.OrdinaryForward(ordinaryForward) &&
    Protocol.AppliedForwardCapture(ordinaryForward) && Protocol.ForceCapture(ordinaryForward) && Protocol.LatchedTrial(ordinaryForward) &&
    !Protocol.OrdinaryForward(appliedForward));
foreach(var r in new[]{ordinaryForward with{Schema=6},ordinaryForward with{Mode="shim-applied-car-trial",WorkloadSha256=null},
    ordinaryForward with{ControlsSha256=null},ordinaryForward with{ProfileSha256=null},ordinaryForward with{StageReference=null}})
    Refuse(()=>Protocol.Admit(Bytes(r),now,nonce));
foreach(var r in new[]{appliedCar with{Schema=5},appliedCar with{Mode="shim-car-trial"},appliedCar with{ProfileSha256=null},
    appliedCar with{WorkloadSha256="bad"},appliedCar with{ProfileRevision=null},appliedCar with{ApplyReadbackSha256=null},car with{ProfileSha256=hash}})
    Refuse(()=>Protocol.Admit(Bytes(r),now,nonce));

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
if(args.Length is 4 or 5 && args[0]!="--applied") {
    var controls=File.ReadAllBytes(args[0]);var profile=File.ReadAllBytes(args[1]);var workload=File.ReadAllBytes(args[2]);
    var plan=CarWorkload.Read(controls,profile,workload);
    Check(plan.Pulses.Length==10 && plan.Neutral.Command.Split('\n').Length==6 && plan.Neutral.Command.Contains("value=1743"));
    Check(plan.Pulses.Select(p=>p.Action).SequenceEqual(new[]{"shiftUp","shiftDown","handbrake","handbrake","steer","steer","throttle","throttle","brake","brake"}));
    Check(plan.Pulses.Select(p=>p.Expected).SequenceEqual(new[]{1d,1d,.5,1,-.5,.5,.5,1,.5,1}));
    Check(plan.Forward.Length==3&&plan.Forward[1].Action=="throttle"&&plan.Forward[1].Expected==.5&&plan.Forward[1].DurationMs==8000);
    Check(plan.Forward[1].Command.Split('\n').Length==6&&plan.Forward[1].Command.Split('\n').All(l=>l.Split(' ').Contains("ms=8000"))&&plan.Forward[1].Command.Contains("value=1743"));
    foreach(int which in new[]{0,1,2}) {var all=new[]{controls.ToArray(),profile.ToArray(),workload.ToArray()};all[which][10]^=1;Refuse(()=>CarWorkload.Read(all[0],all[1],all[2]));}
    using var output=new StreamWriter(new FileStream(args[3],FileMode.CreateNew,FileAccess.Write));
    foreach(var p in args.Length==5&&args[4]=="forward"?plan.Forward:new[]{plan.Neutral}.Concat(plan.Pulses.SelectMany(p=>new[]{p,plan.Neutral}))) {
        output.WriteLine(p.Action+" "+p.Expected.ToString("R",System.Globalization.CultureInfo.InvariantCulture));output.WriteLine(p.Command);output.WriteLine("---");
    }
    Console.WriteLine($"PASS: immutable independent sources and six-action raw batch fixture exported; {count} checks.");
}

if(args.Length==5 && args[0]=="--applied") {
    byte[] controls=File.ReadAllBytes(args[1]),profile=File.ReadAllBytes(args[2]),workload=File.ReadAllBytes(args[3]);
    string Hash(byte[] bytes)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    string revision=Encoding.UTF8.GetString(controls).Split('\n').Single(l=>l.StartsWith("Revision = ",StringComparison.Ordinal))[11..].Trim();
    AppliedCarWorkload Read(byte[] c,byte[] p,byte[] w)=>AppliedCarWorkload.Read(c,p,w,Hash(c),Hash(p),Hash(w),revision);
    var applied=Read(controls,profile,workload);
    Check(applied.Transmission==args[4] && applied.Revision==revision && applied.Actions.Length==6 && applied.Devices.Count is >0 and <=4);
    Check(applied.Neutral.Command.Split('\n').Length==6 && applied.Pulses.Length==10 && applied.Pulses.All(p=>p.DurationMs==700));
    Check(applied.Forward.Length==3 && applied.Forward[1] is {Action:"throttle",Expected:.5,DurationMs:8000} &&
        applied.Forward[1].Command.Split('\n').Length==6 &&
        applied.Forward[1].Command.Split('\n').All(line=>line.Split(' ').Contains("ms=8000")));
    Check(applied.Gaps.Any(g=>g.StartsWith("clutch:",StringComparison.Ordinal)) &&
        applied.Pulses.All(p=>p.Action is not ("clutch" or "select" or "gear7" or "gear8")));
    Check(applied.Pulses.Take(2).Select(p=>p.Action).SequenceEqual(args[4]=="Sequential"?["shiftUp","shiftDown"]:["gear2","gear1"]));
    if(args[4]=="H-pattern") {
        int gate=Array.IndexOf(applied.Actions,"gear1");
        var throttle=applied.Pulses.Single(p=>p.Action=="throttle"&&p.Expected==.5);
        var cut=applied.Pulses.Single(p=>p.Action=="throttleCut");
        Check(gate>=0 && throttle.Command.Split('\n')[gate].Contains(" value=1 ") && cut.Command.Split('\n')[gate].Contains(" value=0 "));
        Check(applied.Forward[1].Command.Split('\n')[gate].Contains(" value=1 ") &&
            applied.Forward[1].Command.Split('\n')[Array.IndexOf(applied.Actions,"throttle")].Contains(" ms=8000 "));
        Check(Observe("throttleCut",.5).Passed && !Observe("throttleCut",.5,v=>v["game.throttle"]=.5).Passed);
        // A valid alternate applied profile may use a hat for a forward gate
        // and a digital pedal; the dynamic plan must not depend on this rig's
        // current button/axis objects.
        var alteredProfile=JsonNode.Parse(profile)!;
        var alteredWorkload=JsonNode.Parse(workload)!;
        var bindings=alteredProfile["Profile"]!["Bindings"]!;
        bindings["gear1"]!["Kind"]="Hat";bindings["gear1"]!["Index"]=0;bindings["gear1"]!["Direction"]=9000;
        bindings["throttle"]!["Kind"]="Button";bindings["throttle"]!["Index"]=11;bindings["throttle"]!["Direction"]=1;
        string changedControls=Encoding.UTF8.GetString(controls);
        changedControls=Regex.Replace(changedControls,@"(?m)^gear1 = button 0 ","gear1 = hat 0 9000 ");
        string axisLine=changedControls.Split('\n').Single(s=>s.StartsWith("throttle = axis 2 ",StringComparison.Ordinal)).TrimEnd('\r');
        string digitalLine=axisLine[..axisLine.IndexOf(" range=",StringComparison.Ordinal)].Replace("axis 2 ","button 11 ",StringComparison.Ordinal);
        changedControls=changedControls.Replace(axisLine,digitalLine,StringComparison.Ordinal);
        var actionRows=alteredWorkload["Actions"]!.AsArray();
        foreach(var sample in actionRows.Single(a=>a!["Action"]!.GetValue<string>()=="gear1")!["Samples"]!.AsArray()) {
            bool held=sample!["Expected"]!.GetValue<double>()>0;
            sample["Raw"]=held?9000:-1;
            sample["Command"]=Regex.Replace(sample["Command"]!.GetValue<string>(),@"button 0 (.*) value=[01] ms=500",
                "hat 0 9000 $1 value="+(held?"9000":"-1")+" ms=500");
        }
        var throttleRows=actionRows.Single(a=>a!["Action"]!.GetValue<string>()=="throttle")!["Samples"]!.AsArray();
        throttleRows.Clear();
        foreach(double level in new[]{0d,1d,0d}) {
            int raw=level>0?1:0;
            var instance=bindings["throttle"]!["InstanceGuid"]!.GetValue<string>();
            throttleRows.Add(new JsonObject{["Expected"]=level,["Raw"]=raw,["Command"]=$"inject raw button 11 dev={{{instance}}} value={raw} ms=500"});
        }
        var alternate=Read(Encoding.UTF8.GetBytes(changedControls),JsonSerializer.SerializeToUtf8Bytes(alteredProfile),JsonSerializer.SerializeToUtf8Bytes(alteredWorkload));
        Check(alternate.Transmission=="H-pattern"&&alternate.Pulses.Any(p=>p.Action=="throttleCut"&&p.Expected==1)&&
            alternate.Pulses.Length<=10);
    }
    Refuse(()=>AppliedCarWorkload.Read(controls,profile,workload,new string('0',64),Hash(profile),Hash(workload),revision));
    Refuse(()=>AppliedCarWorkload.Read(controls,profile,workload,Hash(controls),Hash(profile),Hash(workload),new string('0',64)));
    byte[] badControl=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(controls).Replace("steer = axis 0 ","steer = axis 1 ",StringComparison.Ordinal));
    if(!badControl.SequenceEqual(controls))Refuse(()=>Read(badControl,profile,workload));
    byte[] badWorkload=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(workload).Replace("ms=500","ms=501",StringComparison.Ordinal));
    Refuse(()=>Read(controls,profile,badWorkload));
    Console.WriteLine($"PASS: applied {applied.Transmission} car plan, 10 bounded pulses, unsupported gaps and hash/binding/command refusals; {count} checks.");
}

Dictionary<string,double> AnalysisValues()=>new(){["analysis.force.contractVersion"]=1,["analysis.force.modelVersion"]=6,
    ["analysis.force.valid"]=1,["analysis.force.preview"]=.15,["ffb.gate"]=1,["ffb.sent"]=0,["ffb.deliveryAttempts"]=0,["ffb.connectionAttempts"]=0,
    ["ffb.grip.referenceKind"]=2,["ffb.grip.reference"]=10000,["motion.speed"]=5,["motion.velocity.local.z"]=5,
    ["wheel.fl.grounded"]=1,["wheel.fr.grounded"]=1,["wheel.fl.contactForce"]=5000,["wheel.fr.contactForce"]=5000};
foreach(string fault in new[]{"none","slow","reverse","airborne","invalid","provisional","pause","discontinuity","gap"}) {
    var coverage=new ForceCaptureCoverage();for(int i=0;i<130;i++) {var c=AnalysisValues();
        if(fault=="slow")c["motion.speed"]=1;if(fault=="reverse")c["motion.velocity.local.z"]=-5;if(fault=="airborne")c["wheel.fl.grounded"]=0;
        if(fault=="invalid")c["analysis.force.valid"]=0;if(fault=="provisional")c["ffb.grip.referenceKind"]=1;
        coverage.Observe(i*(fault=="gap"?.2:.02),fault!="pause",fault=="discontinuity",c);}
    Check(coverage.Passed==(fault=="none"));
}
foreach(string key in new[]{"analysis.force.contractVersion","analysis.force.modelVersion","ffb.gate","ffb.sent","ffb.deliveryAttempts","ffb.connectionAttempts"}) {
    var c=AnalysisValues();c[key]+=1;Refuse(()=>new ForceCaptureCoverage().Observe(1,true,false,c));}
var brief=new CarPlateau();for(int i=0;i<8;i++)brief.Add(i*.025,"throttle",.5,NativeValues("throttle",.5));
Check(brief.Passed&&!brief.PassedFor(new("throttle",.5,"fixture",8000)));
Console.WriteLine($"PASS: {count} protocol/workload/coverage checks, including explicit forward scope and measured reference eligibility.");

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
