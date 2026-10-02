using System.Text;
using System.Text.Json;

namespace WodenRallyEdge;

internal static class FfbSummaryCommandChecks
{
    private sealed class BrokenErrors : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string? value) => throw new IOException("private diagnostic stream");
    }
    internal static void Run(Action<bool, string> check)
    {
        int calls = 0;
        using var errors = new StringWriter();
        int result = FfbSummaryCommand.Run(new[] { "--ffb-summary", "new.json", "baseline.json" }, errors, (path, baseline) => {
            calls++; check(path == "new.json" && baseline == "baseline.json", "exact CLI writer arguments retained"); return 0;
        });
        check(result == 0 && calls == 1 && errors.ToString() == "", "successful CLI returns normally and dispatches once");
        foreach (var args in new[] { new[] { "--ffb-summary" }, new[] { "--ffb-summary", " " }, new[] { "--ffb-summary", "new", " " }, new[] { "--ffb-summary", "new", "baseline", "extra" } }) {
            errors.GetStringBuilder().Clear();
            result = FfbSummaryCommand.Run(args, errors, (_, _) => { calls++; return 0; });
            check(result == 2 && calls == 1 && errors.ToString().Contains("ffb-summary-usage"), "malformed summary request cannot fall through into full harness");
        }
        foreach (var (exception, status, code) in new (Exception, int, string)[] {
            (new Exception("strength RMS ordering changed"), 1, "ffb-summary-regression-failed"),
            (new Exception("reference metrics or command/input hashes changed"), 1, "ffb-summary-regression-failed"),
            (new IOException("Preserve existing regression artifact; choose a new path"), 2, "ffb-summary-artifact-refused"),
            (new IOException(@"C:\private\write race"), 2, "ffb-summary-artifact-refused"),
            (new UnauthorizedAccessException("private access"), 2, "ffb-summary-access-refused"),
            (new JsonException("private baseline"), 2, "ffb-summary-invalid-baseline"),
            (new ArgumentException("private path"), 2, "ffb-summary-invalid-argument"),
            (new InvalidOperationException(@"C:\private\unexpected stack"), 1, "ffb-summary-failed")
        }) {
            errors.GetStringBuilder().Clear();
            result = FfbSummaryCommand.Run(new[] { "--ffb-summary", "new.json" }, errors, (_, _) => throw exception);
            string diagnostic = errors.ToString();
            check(result == status && diagnostic.Contains(code), "ordinary summary failure yields bounded diagnostic and nonzero exit");
            check(diagnostic.Length < 256 && !diagnostic.Contains("private") && !diagnostic.Contains("Unhandled") && !diagnostic.Contains(" at "), "no raw exception, private path or stack escapes CLI boundary");
        }
        using var broken = new BrokenErrors();
        check(FfbSummaryCommand.Run(new[] { "--ffb-summary", "new.json" }, broken, (_, _) => throw new IOException()) == 2,
            "broken diagnostic output still returns failing exit code");
    }
}
