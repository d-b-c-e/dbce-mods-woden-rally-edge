# Bounded summary CLI failures — source-only candidate

The early `--ffb-summary` Program branch called FfbRegressionChecks.Write before the normal executable harness's Test exception catcher. Consequently ordinary regression assertion failures, a deliberately tampered baseline and deliberate existing-artifact refusal escaped Main as unhandled managed exceptions and triggered Windows .NET crash reporting. This is a test CLI boundary defect, not evidence of native force or game failure.

Existing Oct 2 Application/.NET Runtime event IDs 1026, records 35031–35035, identify five WodenRallyEdge.Tests.exe failures in the old woden-ffb-regression-candidate checkout. Central daylight time is UTC minus five hours for these events:

| Central time | UTC time | Attribution |
| --- | --- | --- |
| 03:09:02.0613120 | 08:09:02.0613120 | Actual provisional `strength RMS ordering changed` assertion failure; unsupported monotonic RMS assumption was subsequently removed. This was not a deliberate negative control. |
| 03:14:15.0738071 | 08:14:15.0738071 | Deliberately altered baseline rejected; matching initial tamper log. |
| 03:14:15.9076190 | 08:14:15.9076190 | Deliberate attempt to overwrite an existing reference artifact refused; matching initial overwrite log. |
| 03:14:51.2647384 | 08:14:51.2647384 | Deliberately altered baseline rejected during verified repeat. |
| 03:14:52.0436049 | 08:14:52.0436049 | Existing artifact overwrite refused during verified repeat. |

The retained tampered JSON has first-row RMS 0.1 versus the reference's genuine zero-strength RMS 0, with matching input and command hashes. The original negative wrapper had a Select-String positional error; its reported overall PASS is not authoritative. The verified repeat explicitly checked the refusal messages and reference byte preservation. The existing regression handoff documents correction of the provisional RMS assertion and subsequent successful reference suites. The first exception's complete stdout was not retained as a separate failure log; its exact event stack, adjacent first build log and handoff establish its attribution.

FfbSummaryCommand now owns the early CLI dispatch. Exactly two or three arguments are required; malformed requests cannot fall through into the full harness. It catches ordinary managed summary/comparison failures and returns a nonzero code with a bounded fixed diagnostic on stderr. Success is 0; failed assertions or unexpected managed generation/comparison failures are 1; usage, artifact I/O/access refusal, invalid baseline JSON and invalid arguments are 2. Known regression reasons are mapped to fixed text; raw exception messages, paths and stacks are not printed. A failed diagnostic stream still preserves a failing exit code.

FfbRegressionChecks.Write and its CreateNew contract remain byte-identical to canonical 6908443. In-process tests still receive the original exceptions; the arithmetic, golden reference, output hashes, settings and native/FFB/game behavior are unchanged. This is a test-executable-only change. It does not change Windows reporting policy, dismiss existing dialogs, disable assertions or convert failures into success. It handles ordinary managed errors at this CLI boundary; it cannot guarantee handling process termination, stack overflow or other nonrecoverable runtime failures.

Pure injected-writer test source covers successful argument dispatch, malformed command requests, the three historical messages, ordinary file/access/JSON/argument/unexpected exceptions, fixed diagnostic bounds and a broken error writer. It does not run generation or open devices. No build, test executable or reproduction was run for this candidate, as requested while attribution is being reconciled. Only static source inspection and git diff whitespace checks were performed. Compilation and the planned pure tests remain pending parent direction, followed by separately approved CLI/subprocess checks if needed to verify ordinary exit status without new crash events. Do not run old negative commands again through the unprotected binary.
