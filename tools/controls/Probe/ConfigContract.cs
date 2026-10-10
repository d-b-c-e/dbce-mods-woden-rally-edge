using System.Security.Cryptography;
using System.Text;

namespace Woden.ControlsTesting;

// BepInEx saves comments/order and missing defaults while the main plugin loads,
// before a dependent addon can inspect it. Preserve every supplied value and
// permit only the defaults observed in controls.2 (cc17cad), never arbitrary
// added settings. The request independently pins the exact plugin/Core bytes.
public static class ConfigContract
{
    static readonly Dictionary<string,string> AddedDefaults = new(StringComparer.Ordinal)
    {
        ["Display/ShowFrameRate"]="false",
        ["ForceFeedback/CrashEnabled"]="true",
        ["ForceFeedback/GripLoadRatio"]="2",
        ["ForceFeedback/GripSmoothing"]="0.2"
    };
    public static Dictionary<string,string> Parse(byte[] bytes)
    {
        if(bytes.Length==0 || bytes.Length>1024*1024) throw new InvalidDataException("Config size refused.");
        var text=new UTF8Encoding(false,true).GetString(bytes).TrimStart('\ufeff');
        var values=new Dictionary<string,string>(StringComparer.Ordinal);
        var sections=new HashSet<string>(StringComparer.Ordinal);
        string section="";
        foreach(var line in text.Split('\n'))
        {
            string t=line.Trim();
            if(t.Length==0 || t.StartsWith("#",StringComparison.Ordinal)) continue;
            if(t.Any(c=>char.IsControl(c))) throw new InvalidDataException("Config control character.");
            if(t.StartsWith("[",StringComparison.Ordinal))
            {
                if(!t.EndsWith("]",StringComparison.Ordinal) || t.Length<3) throw new InvalidDataException("Config section syntax.");
                section=t[1..^1].Trim();
                if(section.Length==0 || section.IndexOfAny(new[]{'[',']','/'})>=0 || !sections.Add(section)) throw new InvalidDataException("Ambiguous config section.");
                continue;
            }
            int eq=t.IndexOf('=');
            if(section.Length==0 || eq<=0) throw new InvalidDataException("Config setting syntax.");
            string key=t[..eq].Trim();
            if(key.Length==0 || key.Contains('/') || !values.TryAdd(section+"/"+key,t[(eq+1)..].Trim())) throw new InvalidDataException("Ambiguous config key.");
        }
        if(values.Count==0) throw new InvalidDataException("Config has no values.");
        return values;
    }
    public static string[] Verify(byte[] original,byte[] loaded,string expectedSha256)
    {
        using var sha=SHA256.Create();
        if(!Convert.ToHexString(sha.ComputeHash(original)).Equals(expectedSha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Config snapshot hash differs.");
        var before=Parse(original);var after=Parse(loaded);
        foreach(var item in before)
            if(!after.TryGetValue(item.Key,out var value) || value!=item.Value) throw new InvalidDataException("Config value changed: "+item.Key);
        var added=new List<string>();
        foreach(var item in after)
        {
            if(before.ContainsKey(item.Key)) continue;
            if(!AddedDefaults.TryGetValue(item.Key,out var expected) || item.Value!=expected) throw new InvalidDataException("Unexpected config addition: "+item.Key);
            added.Add(item.Key);
        }
        return added.ToArray();
    }
}
