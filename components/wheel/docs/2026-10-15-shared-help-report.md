# Woden standalone Help report adapter

Source-only successor to `c536bb46` / compiled close fix `f61da7b`. The retained
passing close-build outputs remain unchanged. This adapter has not been built,
run, packaged or installed and does not hold the imminent input diagnosis.

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
source `--help-report-only` covers the three actions, default-off log selection,
redacted title, stale-preview/review invalidation, no settings dirtying and
preparation failure, without opening a browser or exporting files.
Preview pages expose the full content without requiring a traversal before
confirmation; no log content is silently truncated from the prepared snapshot.

Source syntax and project XML checks only for this checkpoint. Actual host
compilation/fixture execution and rendered review/scrolling remain pending.
Panel's close/cancel/input and MenuOwnership diagnostics are unchanged from
the passing close source. No force, calibration, controls or owner files were
changed. The center-camera-hint candidate remains separate.
