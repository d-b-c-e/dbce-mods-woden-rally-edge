# Settings parity source result

Candidate base: 15bc663389564d8ab8c22cb5054e473ba47f5eca; Fleet attempt 01M5J1ZJ0J5WJDAW0YSNQ02W43; agent melo.

PASS: source-linked UiTests Release build, zero warnings/errors; executable 6,810 assertions plus 95 managed shim/lease checks. Actual Panel.cs/Settings.cs linked against fake Unity/devices. All five renamed labels are reached in the existing full harness. Snapshot writes now use the unique run directory, preserving earlier output. Run01 (6,782 assertions) is retained; run02 adds both explicit camera-label checks.

Route: existing vendor/playback/Stage-RigLease.ps1, acquired atomically and released in the same PS7 process with finally. Both result JSONs record release and absent slot. Existing PS7 at C:/Program Files/WindowsApps/Microsoft.PowerShell_7.6.7.0_x64__8wekyb3d8bbwe/pwsh.exe was used. Initial plain pwsh invocation failed command lookup before any build or lease; no installation or global setting change was made. Exact reproducible runner: Run-Ui.ps1 (choose a fresh two-digit Run argument to retain logs).

Logs: ui-build-02.log, ui-run-02.log, ui-result-02.json; earlier -01 files retained. inventory-check.json confirms all 62 keys and 37 current ledger IDs represented, with final source hashes. fixtures-02.json records generated fixture file hashes under artifacts/ux-ui-fixture/3c87de2908c2475ab80bf63c4ae2d273; earlier ab224b4061a643d8b96828555228c3e5 retained. Existing top-level fixture snapshots were not replaced.

Only five Panel strings changed. No config keys/defaults/values, force tune, active/draft/next-start behavior, placement, shared binding presentation or native prompt GLY changes. The inventory includes current Wheelkit source references and unresolved differences. STD-004 wording reconciled; STD-017 source-audited partial; STD-030 remains partial. STD-016/020 unchecked and STD-031/037 pending retained.

Not run: installer/package, native ABI, game, input, display, device or force. No installed/runtime/physical/full-parity claim. No network, package installation, public push or toolkit/Wheelkit source change.
