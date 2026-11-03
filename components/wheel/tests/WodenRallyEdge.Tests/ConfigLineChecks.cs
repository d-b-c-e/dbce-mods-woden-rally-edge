using System.Text;
using WodenRallyEdge.Core;

static class ConfigLineChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.GetFullPath(Path.GetTempPath());
        string dir = Path.Combine(root, "woden-configline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "test.cfg");
            const string before = "# owner caf\u00e9\r\n[Force]\nEnabled = true\r\n[Triple]\n  CompareView  = 1  \r\nMatchGameFov = false\n[Future]\r\nThing=keep";
            foreach (bool bom in new[] { false, true })
            {
                var encoding = new UTF8Encoding(bom);
                byte[] Bytes(string text) => encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
                File.WriteAllBytes(path, Bytes(before));
                ConfigLine.Write(path, "Triple", "CompareView", "2");
                check(File.ReadAllBytes(path).SequenceEqual(Bytes(before.Replace("CompareView  = 1", "CompareView  = 2"))),
                    "single value changes; mixed separators, BOM state, spaces and missing final newline remain exact");
            }
            foreach (string ambiguous in new[] { "[Triple]\nCompareView = 1\nCompareView = 1\n", "[Triple]\nCompareView = 1\n[Triple]\nMatchGameFov = false\n" })
            {
                File.WriteAllText(path, ambiguous);
                bool refused = false;
                try { ConfigLine.Write(path, "Triple", "CompareView", "2"); } catch (InvalidDataException) { refused = true; }
                check(refused && File.ReadAllText(path) == ambiguous, "duplicate owned key or section refuses without writing");
            }
            byte[] invalid = { (byte)'[', (byte)'X', (byte)']', 10, 0xff };
            File.WriteAllBytes(path, invalid);
            bool encodingRefused = false;
            try { ConfigLine.Write(path, "Triple", "CompareView", "2"); } catch (DecoderFallbackException) { encodingRefused = true; }
            check(encodingRefused && File.ReadAllBytes(path).SequenceEqual(invalid), "unsupported encoding remains untouched");

            File.WriteAllText(path, before);
            string sentinel = path + ".tmp-unrelated";
            File.WriteAllText(sentinel, "keep this sibling");
            bool locked = false;
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                try { ConfigLine.Write(path, "Triple", "CompareView", "3"); } catch (IOException) { locked = true; }
            check(locked && File.ReadAllText(path) == before && File.ReadAllText(sentinel) == "keep this sibling",
                "replace failure preserves destination and unrelated sibling");
            check(Directory.GetFiles(dir, "*.tmp-*").SequenceEqual(new[] { sentinel }), "only unrelated sibling remains after cleanup");
        }
        finally
        {
            // This invocation created the GUID fixture directly under its configured temp root.
            if (Path.GetDirectoryName(dir) == root.TrimEnd(Path.DirectorySeparatorChar)) Directory.Delete(dir, true);
        }
    }
}
