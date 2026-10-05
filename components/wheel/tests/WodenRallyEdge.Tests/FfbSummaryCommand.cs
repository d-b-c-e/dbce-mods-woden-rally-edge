using System.Text.Json;

namespace WodenRallyEdge;

// CLI boundary only: expected refusals and failed assertions become nonzero exits,
// rather than escaping the early --ffb-summary branch into Windows crash reporting.
// Write retains its exceptions and CreateNew contract for in-process fixture callers.
internal static class FfbSummaryCommand
{
    internal static int Run(string[] args, TextWriter errors, Func<string, string?, int>? write = null)
    {
        if (args.Length is < 2 or > 3 || args[0] != "--ffb-summary" ||
            string.IsNullOrWhiteSpace(args[1]) || (args.Length == 3 && string.IsNullOrWhiteSpace(args[2])))
            return Report(errors, 2, "ffb-summary-usage", "Expected --ffb-summary <new-output-path> [baseline-path].");
        try {
            int result = (write ?? ((path, baseline) => FfbRegressionChecks.Write(path, baseline)))(args[1], args.Length == 3 ? args[2] : null);
            return result == 0 ? 0 : Report(errors, 1, "ffb-summary-failed", "Summary generation did not succeed.");
        } catch (IOException) {
            return Report(errors, 2, "ffb-summary-artifact-refused", "Summary artifact operation was refused.");
        } catch (UnauthorizedAccessException) {
            return Report(errors, 2, "ffb-summary-access-refused", "Summary artifact access was refused.");
        } catch (JsonException) {
            return Report(errors, 2, "ffb-summary-invalid-baseline", "Baseline JSON is invalid.");
        } catch (ArgumentException) {
            return Report(errors, 2, "ffb-summary-invalid-argument", "Summary argument is invalid.");
        } catch (NotSupportedException) {
            return Report(errors, 2, "ffb-summary-invalid-argument", "Summary argument is unsupported.");
        } catch (Exception ex) {
            string? reason = ex.Message switch {
                "strength RMS ordering changed" => "Strength RMS ordering assertion failed.",
                "reference identity/model/dependency differs" => "Reference identity, model or dependency differs.",
                "reference metrics or command/input hashes changed" => "Reference metrics or command/input hashes changed.",
                "strength comparisons changed" => "Strength comparisons changed.",
                "summary options, units or evidence boundary changed" => "Summary options, units or evidence boundary changed.",
                _ => null
            };
            return Report(errors, 1, reason == null ? "ffb-summary-failed" : "ffb-summary-regression-failed",
                reason ?? "Summary generation or comparison failed.");
        }
    }
    private static int Report(TextWriter errors, int exitCode, string code, string message)
    {
        // Fixed-size diagnostics contain no raw exception, stack, argument or path.
        // A broken diagnostic stream must still return the failing exit code.
        try { errors.WriteLine("ERROR " + code + ": " + message); } catch (Exception) { }
        return exitCode;
    }
}
