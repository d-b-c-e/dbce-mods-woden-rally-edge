using System.Text;
using System.Text.RegularExpressions;

namespace WodenRallyEdge.Core;

/// <summary>
/// Writes one <c>Key = value</c> line of a BepInEx .cfg in place and replaces the file atomically: every other line,
/// comment, unknown key and absent default stays as it is. Used for the F7 comparison pick, which must not rewrite the
/// whole settings file. Throws on failure; the old file is then unchanged.
/// </summary>
public static class ConfigLine
{
    public static void Write(string path, string section, string key, string value)
        => WriteBatch(path, section, (key, value));

    // Plan every requested value against one snapshot; publish once, after all
    // keys have passed validation. Never call the single-key disk writer in a loop.
    public static void WriteBatch(string path, string section, params (string Key, string Value)[] changes)
    {
        string full = Path.GetFullPath(path);
        if (string.IsNullOrWhiteSpace(section) || section.Trim() != section || section.IndexOfAny(new[] { '[', ']', '\r', '\n', '\0' }) >= 0)
            throw new InvalidDataException("Invalid settings section");
        var requested = changes.ToArray();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var change in requested) {
            if (string.IsNullOrWhiteSpace(change.Key) || change.Key.Trim() != change.Key ||
                change.Key.IndexOfAny(new[] { '=', '[', ']', '\r', '\n', '\0' }) >= 0 || !keys.Add(change.Key))
                throw new InvalidDataException("Invalid or duplicate requested settings key");
            if (change.Value == null || change.Value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new InvalidDataException("Multiline settings value");
        }
        if (requested.Length == 0) return;
        byte[] before = File.Exists(full) ? File.ReadAllBytes(full) : Array.Empty<byte>();
        byte[] after = before;
        foreach (var change in requested) after = Edit(after, section, change.Key, change.Value);
        string temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        bool owned = false;
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                owned = true; stream.Write(after, 0, after.Length); stream.Flush(true);
            }
            if (File.Exists(full)) File.Replace(temp, full, null); else File.Move(temp, full);
            owned = false;
        }
        finally
        {
            if (owned) try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static byte[] Edit(byte[] before, string section, string key, string value)
    {
        int bom = before.Length >= 3 && before[0] == 0xef && before[1] == 0xbb && before[2] == 0xbf ? 3 : 0;
        var utf8 = new UTF8Encoding(false, true);
        string text = utf8.GetString(before, bom, before.Length - bom);
        if (text.IndexOf('\0') >= 0) throw new InvalidDataException("Unsupported settings encoding");
        var lines = new List<string>();
        foreach (Match line in Regex.Matches(text, @"[^\r\n]*(?:\r\n|\r|\n|$)")) if (line.Length > 0) lines.Add(line.Value);
        string current = ""; string? newline = null; int sectionEnd = -1, found = -1, sections = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            string t = lines[i].Trim(), ending = Ending(lines[i]);
            if (newline == null && ending.Length > 0) newline = ending;
            if (t.StartsWith('['))
            {
                if (!t.EndsWith(']')) throw new InvalidDataException("Malformed settings section");
                current = t[1..^1];
                if (current == section && ++sections > 1) throw new InvalidDataException("Duplicate settings section");
            }
            if (current != section) continue;
            sectionEnd = i + 1;
            int eq = t.IndexOf('=');
            if (eq < 0 || t[..eq].Trim() != key) continue;
            if (found >= 0) throw new InvalidDataException("Duplicate settings key");
            found = i;
        }
        newline ??= Environment.NewLine;
        if (found >= 0)
        {
            string line = lines[found], ending = Ending(line);
            int start = line.IndexOf('=') + 1, end = line.Length - ending.Length;
            while (start < end && (line[start] == ' ' || line[start] == '\t')) start++;
            while (end > start && (line[end - 1] == ' ' || line[end - 1] == '\t')) end--;
            lines[found] = line[..start] + value + line[end..];
        }
        else
        {
            int insert = sectionEnd >= 0 ? sectionEnd : lines.Count;
            if (insert > 0 && Ending(lines[insert - 1]).Length == 0) lines[insert - 1] += newline;
            if (sectionEnd < 0) { lines.Add(newline); lines.Add("[" + section + "]" + newline); insert = lines.Count; }
            lines.Insert(insert, key + " = " + value + newline);
        }
        byte[] body = utf8.GetBytes(string.Concat(lines)), after = new byte[bom + body.Length];
        Array.Copy(before, after, bom); Array.Copy(body, 0, after, bom, body.Length);
        return after;
    }

    private static string Ending(string line) => line.EndsWith("\r\n") ? "\r\n" : line.EndsWith('\n') ? "\n" : line.EndsWith('\r') ? "\r" : "";
}
