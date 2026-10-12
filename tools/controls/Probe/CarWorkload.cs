using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Woden.ControlsTesting;

// Private qualification uses independently retained original profile/workload
// bytes. These hashes identify evidence, not a new user profile or mapper.
public sealed record CarPulse(string Action,double Expected,string Command);
public sealed class CarWorkload
{
    public const string ControlsHash="5206EDFBD1B400E5D6A41D91AE836485BBE27D63992AD6F7483B3BABB0E72B79";
    public const string ProfileHash="9317FAE5C8B029E71713EB7DD665D650EA1452C0E1CFEDA4C5E1335E33452279";
    public const string WorkloadHash="4AB26FDA741482B8E229BF3097A2558B21C27E09D4CA45CF2B3E255A970BA03C";
    public const string Revision="439A4118E99353997C8DF8555CA6B6C4F07DB89590C2B0A5EA60BA02B130D36C";
    public const string StageHash="711747D3021A6E95B600E8386DC149C79102F837D10B981642210F2A634B813F";
    public static readonly string[] Actions={"steer","throttle","brake","handbrake","shiftUp","shiftDown"};
    public const int Milliseconds=700;
    readonly Dictionary<string,CarPulse[]> Samples;
    CarWorkload(Dictionary<string,CarPulse[]> samples)=>Samples=samples;
    public static CarWorkload Read(byte[] controls,byte[] profile,byte[] workload)
    {
        void Match(byte[] bytes,string hash) { if(bytes.Length>1024*1024 || Convert.ToHexString(SHA256.HashData(bytes))!=hash)throw new InvalidDataException("Original car workload/profile hash differs."); }
        Match(controls,ControlsHash);Match(profile,ProfileHash);Match(workload,WorkloadHash);
        using var p=JsonDocument.Parse(profile);using var w=JsonDocument.Parse(workload);
        if(w.RootElement.GetProperty("Schema").GetInt32()!=1 || w.RootElement.GetProperty("ProfileHash").GetString()!=Revision ||
            !Encoding.UTF8.GetString(controls).Split('\n').Any(l=>l.Trim()=="Revision = "+Revision) ||
            p.RootElement.GetProperty("Profile").GetProperty("Transmission").GetString()!="Sequential")
            throw new InvalidDataException("Independent profile revision/transmission differs.");
        var result=new Dictionary<string,CarPulse[]>();
        foreach(string id in Actions)
        {
            var a=w.RootElement.GetProperty("Actions").EnumerateArray().Single(a=>a.GetProperty("Action").GetString()==id);
            result.Add(id,a.GetProperty("Samples").EnumerateArray().Select(s=>new CarPulse(id,s.GetProperty("Expected").GetDouble(),
                s.GetProperty("Command").GetString()!.Replace("ms=500","ms="+Milliseconds))).ToArray());
        }
        return new(result);
    }
    public CarPulse Neutral => new("neutral",0,Batch(null));
    public CarPulse[] Pulses => Actions.SelectMany(id=>Samples[id].Where(s=>s.Expected!=0).Select(s=>s with{Command=Batch(s)})).ToArray();
    public CarPulse Settle => new("settle",1,Batch(Samples["brake"].Single(s=>s.Expected==1)));
    string Batch(CarPulse? selected) => string.Join("\n",Actions.Select(id=>selected?.Action==id ? selected.Command : Samples[id].First(s=>s.Expected==0).Command));
}

// Native observations only. Desired actions and WheelInput._last cannot satisfy
// this accumulator. Missing/late/paused data is unavailable, never zero-filled.
public sealed class CarPlateau
{
    public int Samples {get;private set;}
    public double First {get;private set;}=double.NaN;
    public double Last {get;private set;}=double.NaN;
    public bool Good {get;private set;}=true;
    public void Add(double at,string action,double expected,IDictionary<string,double> c)
    {
        bool Equal(string key,double value) => c.TryGetValue(key,out double v)&&double.IsFinite(v)&&Math.Abs(v-value)<.025;
        bool ok=Equal("game.automatic",0)&&Equal("game.locked",0)&&Equal("game.paused",0)&&Equal("game.respawning",0)&&Equal("game.replay",0);
        foreach(string id in new[]{"steer","throttle","brake"})
        {
            double value=action==id?expected:action=="settle"&&id=="brake"?1:0;
            ok &= Equal("controls."+id,value)&&Equal("game."+id,value);
        }
        ok &= Equal("game.handbrake",action=="handbrake"&&expected>0?1:0);
        if(Samples>0 && at-Last>.1)ok=false;
        if(Samples==0)First=at;
        Last=at;Samples++;Good &= ok;
    }
    public bool Passed=>Good&&Samples>=5&&Last-First>=.1;
}
