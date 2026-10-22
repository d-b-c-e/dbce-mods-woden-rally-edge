# Woden standalone Help report adapter

## October 22 focused host qualification

Clean source candidate `5211ec5083c851fc8e98d14f3179fcfafd469253` qualifies the existing
small underlined links from `daac6446481c9478ca9439b2d229f9ab928f3580` on baseline
`9153edfc66fc6d9e8d2be003292a99b47e486ec0`. Historical Help13 / affected build
PASS at `4f153da5d0d1efebd84e2e97db6ac2fd55b43b4c` is retained, not rerun.
Shared SupportReport remains exactly `ab638898f52ee482718e371594457ae493549551`,
SHA-256 `CA005A794086D66E3F7660EED692FB5583DE87B112BCA748FF60385A5944033A`.

Affected Plugin Release and source-linked UiTests compile passed with zero
warnings/errors (`--no-restore`, no network). The focused `--help-report-only`
route passed **35 assertions**. Two private adapter delegates retain the existing
production shell/root defaults and allow the fixture to intercept every shell
action and select only its own log/output root. No host defect required a behavior
change, and the Woden repository / loaded-version descriptor remains unchanged.

Coverage includes actual link clicks to both repository pages, local Submit form
opening, disabled unreviewed draft refusal, deliberate reviewed issue dispatch,
exact sanitized short URL and long attachment, title/description/log invalidation,
log mutation after review, retained export on retry, folder dispatch, rejected
insecure/non-GitHub URLs, Win32/invalid-shell failure status and export-failure
retry from the retained review. All actions and failures leave settings unsaved
and FFB disabled. Prior exported files survive review invalidation.

Evidence paths were confirmed ignored before writing: `artifacts/help-report`
and `artifacts/ux-ui-fixture`. Final clean documentation successor and its exact HEAD are recorded in
`result-05.json`. Plugin pass: `plugin-build-05.log`; final fixture:
`fixture-build-05.log`, `fixture-run-05.log`, `result-05.json`, all under the former.
Runs01/02 retain fixture visibility failures; run03 passed33 before the final
two UI/refusal checks were added. These were harness corrections, not product
failures. Each build/fixture batch acquired the existing shared lease directly
as `nado`; every result records release true and slot absent. Run04 passed35; final run05 rebuilds the clean committed successor and records
its timestamps and source identity. The runner's existing Program.cs banner says
"no browser/export"; this extension does write sanitized **fixture-only** exports.
No real browser, network, owner log, game, device, input, force or display action
occurred. No common toolkit, full gameplay or unrelated suite was repeated.
No install, package, publication or runtime acceptance follows from this result.

## Adapter behavior

Help begins with **Submit GitHub Issue**, **Mod on GitHub** and
**Wheelkit on GitHub**. The packaged descriptor targets
`d-b-c-e/dbce-mods-woden-rally-edge` and `d-b-c-e/dbce-wheelkit`, using
the loaded assembly informational version, with `Plugin.Version` as fallback.
No runtime git discovery occurs.

The report form uses the existing stripped renderer's Button, Label and
TextArea primitives. It gathers a title/description and an optional, default-off
`BepInEx/LogOutput.log` excerpt. Only that known text log can be selected; there
is no directory scan, config/binding/save/recording collection or broad support
ZIP. Existing **Create support file (local)** remains a separate action.

Review prepares the immutable common report and shows every part through paged
redacted preview text, including the title. Changing a text field or log choice
clears both snapshot and review confirmation. Failed preparation also clears
the previous snapshot. Report edits do not call Dirty/Save or persist a draft.
The player confirms review and deliberately opens the GitHub draft. Opening
transmits the reviewed prefilled query text but creates no issue.

For a long report, that action writes one sanitized snapshot to a new
`BepInEx/WodenSupport/report-<id>/support-report.txt` and offers **Open report
folder**. The issue already contains a useful description/setup before the
player attaches that single file. Its actual local path is shown for recovery
if the folder opener fails. Attaching uploads immediately; final issue
submission stays in GitHub. No auth/token/network client is added to the mod.
The generic shell opener accepts only HTTPS github.com URLs produced from
validated packaged metadata or the shared browser draft.

Shared source pin/license: `Toolkit/Support/PROVENANCE.md`. Root assigned Lola
to run the common toolkit `tests/SupportReport` fixture once in the Art slot;
Woden reuses that common result and compiles its own host. Focused host fixture
source `--help-report-only` now covers the host boundaries above with inert shell
interception and fixture-only logs/exports, alongside the retained default-off
selection, redacted title, invalidation, no settings dirtying and preparation checks.
Preview pages expose the full content without requiring a traversal before
confirmation; no log content is silently truncated from the prepared snapshot.

Host compilation and the focused fixture are qualified above; actual rendered
review/scrolling, real shell/browser behavior and in-game acceptance remain pending.
Panel's close/cancel/input and MenuOwnership diagnostics are unchanged from
the passing close source. No force, calibration, controls or owner files were
changed. The center-camera-hint candidate remains separate.
