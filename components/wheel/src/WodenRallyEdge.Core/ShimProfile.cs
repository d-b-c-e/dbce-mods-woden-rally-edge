using System.Security.Cryptography;
using System.Text;
using Dbce.Wheel.Input;

namespace WodenRallyEdge.Core;

// Extract bytes, not a second Controls parser. The native Core parser remains
// authoritative; the shared binding reader is used only for force-device identity.
public static class ShimProfile
{
    public static byte[] Section(byte[] all)
    {
        if (all.Length > 1024 * 1024) throw new InvalidDataException("Controls file exceeds 1 MiB");
        int at = all.Length >= 3 && all[0] == 0xef && all[1] == 0xbb && all[2] == 0xbf ? 3 : 0, start = -1, end = all.Length;
        while (at < all.Length)
        {
            int lineStart = at, next = Array.IndexOf(all, (byte)'\n', at);
            at = next < 0 ? all.Length : next + 1;
            string line = Encoding.ASCII.GetString(all, lineStart, at - lineStart).Trim();
            if (line == "[Controls]")
            {
                if (start >= 0) throw new InvalidDataException("Duplicate [Controls] section");
                start = lineStart;
            }
            else if (start >= 0 && end == all.Length && line.StartsWith('[')) end = lineStart;
        }
        if (start < 0) throw new InvalidDataException("No [Controls] section");
        return all[start..end];
    }

    public static Guid? SteeringDevice(byte[] section, string coreInfo)
    {
        var info = coreInfo.Split('\n').Where(l => l.Contains('=')).Select(l => l.TrimEnd('\r').Split('=', 2))
            .ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);
        string hash = Convert.ToHexString(SHA256.HashData(section));
        if (!info.TryGetValue("ok", out var ok) || ok != "1" || !info.TryGetValue("sha256", out var accepted) ||
            !string.Equals(hash, accepted, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Core did not accept the exact Controls bytes");
        foreach (string line in new UTF8Encoding(false, true).GetString(section).Split('\n'))
        {
            var p = line.Trim().Split('=', 2);
            if (p.Length != 2 || p[0].Trim() != "steer") continue;
            if (ControlBinding.TryParse(p[1].Trim(), out var binding, out _) && binding!.Fits("steer") == "") return binding.Device;
        }
        return null;
    }
}
