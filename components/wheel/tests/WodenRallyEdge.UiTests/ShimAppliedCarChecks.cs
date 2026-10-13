using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnityEngine;
using Woden.ControlsTesting;

namespace WodenRallyEdge;

internal static partial class ShimChecks
{
    static void TrialAppliedHPattern(string dir,Action<bool,string> check)
    {
        string? evidence=Environment.GetEnvironmentVariable("WODEN_APPLIED_H_DIR");
        if(evidence==null)return;
        byte[] controlsBytes=File.ReadAllBytes(Path.Combine(evidence,"accepted-controls.ini"));
        byte[] profileBytes=File.ReadAllBytes(Path.Combine(evidence,"apply","profile.json"));
        byte[] workloadBytes=File.ReadAllBytes(Path.Combine(evidence,"raw-workload.json"));
        string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
        string revision=Encoding.UTF8.GetString(controlsBytes).Split('\n').Single(s=>s.StartsWith("Revision = ",StringComparison.Ordinal))[11..].Trim();
        var plan=AppliedCarWorkload.Read(controlsBytes,profileBytes,workloadBytes,Hash(controlsBytes),Hash(profileBytes),Hash(workloadBytes),revision);
        check(plan.Transmission=="H-pattern","generated applied car plan is H-pattern");
        using var doc=JsonDocument.Parse(profileBytes);
        var binding=doc.RootElement.GetProperty("Profile").GetProperty("Bindings").GetProperty("throttle");
        int rest=binding.GetProperty("Rest").GetInt32(),max=binding.GetProperty("Maximum").GetInt32();
        Panel.Close(false);Reset(dir);_mode="HPattern";
        File.WriteAllBytes(Path.Combine(dir,"dbce-controls.ini"),controlsBytes);
        var values=Enumerable.Range(0,18).Select(i=>new GamePadSystem.Actions("applied"+i,0,false)).ToArray();
        var pad=new GamePadSystem.Game_Pad{PadActions=values};
        var producer=new GamePadSystem();producer.Game_Pads.Add(pad);
        var car=new MainCar();car.Aids!.Automatic=false;car.TransmissionSystem!.Changing=false;car.MyGear=1;car.Gears=6;
        var gameControls=new Controls{field_Private_MainCar_0=car,field_Private_Game_Pad_0=pad,field_Private_GamePadSystem_0=producer};
        car.MyControls=gameControls;Runtime.Local=car;ShimInput.BeforeStock(producer);Input.Held=false;Input.Keys.Clear();Input.Down.Clear();
        foreach(var pulse in new[]{plan.Pulses.Single(p=>p.Action=="throttle"&&p.Expected==.5),plan.Pulses.Single(p=>p.Action=="throttleCut")})
        {
            var lines=pulse.Command.Split('\n');
            int gateIndex=Array.IndexOf(plan.Actions,"gear1"),throttleIndex=Array.IndexOf(plan.Actions,"throttle");
            int Raw(string line)=>int.Parse(Regex.Match(line,@"\bvalue=(-?[0-9]+)\b").Groups[1].Value);
            int rawGate=Raw(lines[gateIndex]),rawThrottle=Raw(lines[throttleIndex]);
            float normalized=(float)(rawThrottle-rest)/(max-rest);
            Frame("steer",0);Frame("throttle",normalized);Frame("brake",0);Frame("handbrake",0);
            Frame("gear1",rawGate);Frame("gear2",0);
            Next();
            var lease=Runtime.Wheel!.Apply(gameControls);
            check(ShimInput.Ready&&lease!=null&&Math.Abs(ShimInput.Axis("throttle")-.5f)<.02f,
                "generated applied raw throttle reaches the shared shim action before native consumer");
            check(rawGate==(pulse.Action=="throttle"?1:0) &&
                Math.Abs(values[7].value-(pulse.Action=="throttle"?normalized:0))<.02,
                "ApplyShim/Pedals preserves gated throttle and cuts the same raw throttle with a neutral gate");
            lease!.Restore();
            check(values.All(v=>v.value==0),"generated H-pattern input lease restores the native action table");
        }
        ShimInput.Stop("applied H-pattern fixture complete");
    }
}
