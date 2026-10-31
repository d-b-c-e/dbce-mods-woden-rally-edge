using System.Text;

namespace WodenRallyEdge.Core;

/// <summary>
/// Writes one <c>Key = value</c> line of a BepInEx .cfg in place and replaces the file atomically: every other line,
/// comment, unknown key and absent default stays as it is. Used for the F7 comparison pick, which must not rewrite the
/// whole settings file. Throws on failure; the old file is then unchanged.
/// </summary>
public static class ConfigLine
{
    public static void Write(string path, string section, string key, string value)
    {
        string full = Path.GetFullPath(path);
        var lines = new List<string>(File.Exists(full) ? File.ReadAllLines(full) : Array.Empty<string>());
        string current = ""; int sectionEnd = -1, found = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            string t = lines[i].Trim();
            if (t.StartsWith('[') && t.EndsWith(']')) current = t[1..^1];
            if (current != section) continue;
            sectionEnd = i + 1;
            int eq = t.IndexOf('=');
            if (eq >= 0 && t[..eq].Trim() == key) { found = i; break; }
        }
        string line = key + " = " + value;
        if (found >= 0) lines[found] = line;
        else if (sectionEnd >= 0) lines.Insert(sectionEnd, line);
        else { lines.Add(""); lines.Add("[" + section + "]"); lines.Add(line); }
        string temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temp, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
            if (File.Exists(full)) File.Replace(temp, full, null); else File.Move(temp, full);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
