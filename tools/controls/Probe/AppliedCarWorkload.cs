using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Woden.ControlsTesting;

// Schema-7 car plans are derived from one frozen production Apply, not from the
// historical run07 constants. This parser is pure; it cannot open a device or
// submit input. The resident probe re-reads and checks the same bytes per batch.
public sealed class AppliedCarWorkload : ICarWorkload
{
    static readonly Regex Command=new(@"^inject raw (axis|button|hat) ([0-9]+)(?: ([0-9]+))? dev=(\{[0-9a-fA-F-]{36}\}) value=(-?[0-9]+) ms=500(?: range=(-?[0-9]+)\.\.(-?[0-9]+))?$",RegexOptions.CultureInvariant);
    static readonly string[] Driving={"steer","throttle","brake","handbrake"};
    static readonly string[] Unsupported={"clutch","select","gear7","gear8"};
    readonly Dictionary<string,CarPulse[]> samples;
    public string Transmission {get;}
    public string Revision {get;}
    public string[] Actions {get;}
    public IReadOnlyDictionary<Guid,Guid> Devices {get;}
    public string[] SelectedActions=>Actions;
    public IReadOnlyDictionary<Guid,Guid>? DevicePins=>Devices;
    public IReadOnlyList<string> Gaps {get;}
    AppliedCarWorkload(string transmission,string revision,string[] actions,Dictionary<string,CarPulse[]> samples,
        Dictionary<Guid,Guid> devices,List<string> gaps)
    {Transmission=transmission;Revision=revision;Actions=actions;this.samples=samples;Devices=devices;Gaps=gaps;}

    public static AppliedCarWorkload Read(byte[] controls,byte[] profile,byte[] workload,
        string controlsHash,string profileHash,string workloadHash,string revision)
    {
        static void Pinned(byte[] bytes,string hash)
        {
            if(bytes.Length is 0 or >1048576 || hash.Length!=64 ||
                !Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Frozen applied car source hash differs.");
        }
        Pinned(controls,controlsHash);Pinned(profile,profileHash);Pinned(workload,workloadHash);
        using var p=JsonDocument.Parse(profile);using var w=JsonDocument.Parse(workload);
        var selected=p.RootElement.GetProperty("Profile");
        string transmission=selected.GetProperty("Transmission").GetString()??"";
        if(transmission is not ("Sequential" or "H-pattern"))throw new InvalidDataException("Applied car transmission is unsupported.");
        if(w.RootElement.GetProperty("Schema").GetInt32()!=1 ||
            !string.Equals(w.RootElement.GetProperty("ProfileHash").GetString(),revision,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Frozen writer revision differs from applied profile.");
        string ini=Encoding.UTF8.GetString(controls);
        if(!Regex.IsMatch(ini,@"(?m)^Schema = 1\r?$",RegexOptions.CultureInvariant) ||
            !ini.Split('\n').Any(line=>line.Trim()=="Revision = "+revision) ||
            !ini.Split('\n').Any(line=>line.Trim()=="Transmission = "+transmission) ||
            !ini.Split('\n').Any(line=>line.Trim()=="Profile = "+selected.GetProperty("Name").GetString()))
            throw new InvalidDataException("Applied Controls identity/transmission differs.");
        var bindings=selected.GetProperty("Bindings");
        string[] actions=Driving.Concat(transmission=="Sequential"?new[]{"shiftUp","shiftDown"}:new[]{"gear1","gear2"}).ToArray();
        var all=w.RootElement.GetProperty("Actions").EnumerateArray().ToArray();
        var parsed=new Dictionary<string,CarPulse[]>(StringComparer.Ordinal);
        var devices=new Dictionary<Guid,Guid>();
        foreach(string id in actions)
        {
            if(!bindings.TryGetProperty(id,out var binding))throw new InvalidDataException("Required applied action is unbound: "+id);
            string kind=binding.GetProperty("Kind").GetString()??"";
            int index=binding.GetProperty("Index").GetInt32();
            Guid instance=binding.GetProperty("InstanceGuid").GetGuid(),product=binding.GetProperty("ProductGuid").GetGuid();
            if(instance==Guid.Empty || product==Guid.Empty || index<0 || index>=(kind=="Axis"?8:kind=="Button"?128:kind=="Hat"?4:0) ||
                (id=="steer" ? kind!="Axis" : id is "throttle" or "brake" or "handbrake" ? kind is not ("Axis" or "Button") : kind is not ("Button" or "Hat")))
                throw new InvalidDataException("Unsupported applied object: "+id);
            if(devices.TryGetValue(instance,out var prior) && prior!=product)throw new InvalidDataException("Device product changed within profile.");
            devices[instance]=product;
            var line=ini.Split('\n').Where(s=>s.StartsWith(id+" = ",StringComparison.Ordinal)).ToArray();
            int direction=binding.GetProperty("Direction").GetInt32();
            if(kind=="Hat" && direction is <0 or >=36000 || kind=="Button" && direction!=1)
                throw new InvalidDataException("Invalid applied digital direction: "+id);
            if(line.Length!=1 || !line[0].Contains(" dev="+instance.ToString("B"),StringComparison.OrdinalIgnoreCase) ||
                !line[0].Contains(" prod="+product.ToString("B"),StringComparison.OrdinalIgnoreCase) ||
                !line[0].StartsWith(id+" = "+kind.ToLowerInvariant()+" "+index+" "+(kind=="Hat"?direction+" ":""),StringComparison.Ordinal))
                throw new InvalidDataException("Applied Controls binding differs from profile: "+id);
            int min=0,max=0,rest=0;
            bool inverted=false;
            if(kind=="Axis")
            {
                min=binding.GetProperty("Minimum").GetInt32();max=binding.GetProperty("Maximum").GetInt32();
                rest=binding.GetProperty("Rest").GetInt32();
                inverted=binding.GetProperty("Inverted").GetBoolean();
                if(min>=max || rest<min || rest>max || direction is not (-1 or 1) ||
                    !line[0].Contains($" range={min}..{max} rest={rest} travel={(direction<0?"-":"+")}1 ",StringComparison.Ordinal))
                    throw new InvalidDataException("Applied axis range/rest differs: "+id);
            }
            var rows=all.Where(a=>a.GetProperty("Action").GetString()==id).ToArray();
            if(rows.Length!=1)throw new InvalidDataException("Frozen writer action is missing/duplicate: "+id);
            var pulses=new List<CarPulse>();
            foreach(var sample in rows[0].GetProperty("Samples").EnumerateArray())
            {
                double expected=sample.GetProperty("Expected").GetDouble();
                if(!double.IsFinite(expected) || expected< (id=="steer"?-1:0) || expected>1)
                    throw new InvalidDataException("Unsafe expected value: "+id);
                string raw=sample.GetProperty("Command").GetString()??"";
                var m=Command.Match(raw);
                if(!m.Success || m.Groups[1].Value!=kind.ToLowerInvariant() || int.Parse(m.Groups[2].Value)!=index ||
                    (kind=="Hat" ? m.Groups[3].Value!=direction.ToString() : m.Groups[3].Success) ||
                    Guid.Parse(m.Groups[4].Value)!=instance || !int.TryParse(m.Groups[5].Value,out int value) ||
                    (kind=="Axis" ? m.Groups[6].Value!=min.ToString() || m.Groups[7].Value!=max.ToString() || value<min || value>max :
                        m.Groups[6].Success || kind=="Button" && value is not (0 or 1) || kind=="Hat" && value!=-1 && value!=direction))
                    throw new InvalidDataException("Frozen raw command differs from applied binding: "+id);
                if(sample.GetProperty("Raw").GetInt32()!=value)throw new InvalidDataException("Frozen raw value differs from command: "+id);
                double coordinate=kind is "Button" or "Hat" ? expected : id=="steer" ?
                    ((double)min+max)/2+(inverted?-expected:expected)*((double)max-min)/2 :
                    rest+(inverted?1-expected:expected)*((double)(direction==1?max:min)-rest);
                int normalized=kind=="Button" ? (expected>=.5?1:0) : kind=="Hat" ? (expected>=.5?direction:-1) :
                    (int)Math.Clamp(Math.Round(coordinate,MidpointRounding.AwayFromZero),min,max);
                if(value!=normalized)throw new InvalidDataException("Frozen raw sample differs from applied profile calibration: "+id);
                pulses.Add(new CarPulse(id,expected,raw.Replace("ms=500","ms=700",StringComparison.Ordinal)));
            }
            if(!pulses.Any(s=>s.Expected==0) || !pulses.Any(s=>s.Expected>0))
                throw new InvalidDataException("Action lacks neutral and positive samples: "+id);
            parsed.Add(id,pulses.ToArray());
        }
        var gaps=Unsupported.Where(id=>bindings.TryGetProperty(id,out _)).Select(id=>id+": not delivered by the bounded car trial").ToList();
        return new(transmission,revision,actions,parsed,devices,gaps);
    }
    string Batch(CarPulse? selected,bool holdForwardGate=true)=>string.Join("\n",Actions.Select(id=>
        selected?.Action==id?selected.Command:
        Transmission=="H-pattern" && holdForwardGate && selected!=null && id=="gear1" && selected.Action is not ("gear1" or "gear2") ?
            samples["gear1"].Single(s=>s.Expected==1).Command : samples[id].First(s=>s.Expected==0).Command));
    public CarPulse Neutral=>new("neutral",0,Batch(null));
    public CarPulse Settle=>new("settle",1,Batch(samples["brake"].Single(s=>s.Expected==1)));
    // The existing applied profile supplies both the throttle sample and (for
    // H-pattern) the held gear1 command. No alternate calibration is invented.
    public CarPulse[] Forward
    {
        get
        {
            var throttle=samples["throttle"].Single(s=>s.Expected==.5);
            return [Neutral,new CarPulse("throttle",.5,Batch(throttle).Replace("ms=700","ms=8000",StringComparison.Ordinal),8000),Neutral];
        }
    }
    public CarPulse[] Pulses
    {
        get
        {
            string[] order=Transmission=="Sequential"?["shiftUp","shiftDown","handbrake","steer","throttle","brake"]:
                ["gear2","gear1","handbrake","steer","throttle","brake"];
            var plan=order.SelectMany(id=>samples[id].Where(s=>s.Expected!=0 &&
                (Transmission!="H-pattern" || id!="handbrake" || s.Expected==1)).Select(s=>s with{Command=Batch(s)})).ToList();
            if(Transmission=="H-pattern")
            {
                var cut=samples["throttle"].First(s=>s.Expected>0);
                plan.Add(new CarPulse("throttleCut",cut.Expected,Batch(cut,false)));
            }
            if(plan.Count>10)throw new InvalidDataException("Car plan exceeds bounded pulse count.");
            return plan.ToArray();
        }
    }
}
