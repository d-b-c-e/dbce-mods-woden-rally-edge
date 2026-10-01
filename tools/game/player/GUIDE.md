# Player setup, recovery and evidence limits

Return to [package instructions](README.md). The package targets Windows x64,
Super Woden Rally Edge Steam app 3218630, build 21802346, Unity 6000.3.6f1.
The retained installer enforces the supported GameAssembly hash and pinned
BepInEx BE 788 prerequisite. It refuses a running game and never kills it.

Install or update using `Install.bat -GameDir <game-directory>`. A fresh missing
loader may need a download; the existing loader is verified and retained.
`-LoaderArchive <archive>` supplies the exact pinned loader offline. Updates
require a complete prior receipt whose nine payload hashes match. Missing,
modified or unowned payloads block replacement before mutation. Existing legacy
payloads without a receipt require separate adoption review; a backup alone
does not establish ownership. Do not edit receipts to bypass refusal.

F6 contains the existing calibration, binding and telemetry pages. Wheel input
defaults Off; retained FFB preference defaults On, telemetry On and recording
Off for fresh settings. Existing settings are preserved. F8 saves FFB Off; only
explicitly choosing On resumes that preference. No package validation or metadata
operation opens a wheel or enables force. Device calibration and candidate
sign/load/focus/lifecycle acceptance require a coordinated attended session.

Uninstall with `Uninstall.bat -GameDir <game-directory>`. Only unchanged
receipt-owned payloads are removed. Shared loader, unknown files, captures and
settings remain. Explicit `-RemoveUserData` additionally removes only named
Woden config/binding/request files; private recordings still remain.

Before writes the installer hashes backups under `WodenWheelBackups`. Caught
failures roll back verified transaction bytes while the game is closed. If the
game opens or an external writer changes files, automatic restoration stops and
a recovery report retains unknown bytes. There is no rollback command or
standalone no-write target-check flag. Power interruption/conflicts require
manual review of the report and verified backup; never restore over newer user
settings or overwrite unrecognized files. No second renderer migration is supplied.

Capture is an existing request-driven diagnostic implementation. Its coordinated
owner helper is source-only, not in this ZIP; the menu can stop a recording but
does not initiate a new owner request. Captures are local-private JSONL, bounded
by existing 1200-second/64-MiB limits; drops, limits or missing completion footer
cannot be promoted to complete analysis evidence. Raw input and force requests
are diagnostic channels, not measured wheel torque or a gameplay replay tape.

Actual ForceSignal offline analysis exists in the retained Core and source-only
TelemetryInspector tooling. No standalone player analysis/replay command is
distributed. Source/case/config/profile/build/baseline identities must match;
trials use separate hashes and new observations. Offline analysis never initializes
devices or sends force. No game-input replay, physical playback or automatic
capture/corpus upload is supplied.

True triple rendering is unavailable: research source is excluded, with no
validated game ABI, views or presentation. Historical owner 0.2.12 observations
do not accept this candidate. Shared cadence impacts remain unadopted.

`delivery-manifest.json` is package delivery metadata, not installed receipt,
runtime status or optimizer discovery. `manifest.json` covers every package file
except itself, including delivery metadata. Delivery metadata hashes neither
itself nor the integrity inventory. ZIP SHA256 is reviewed externally. The
retained runtime/installer Git identities and new packaging identity are separate;
hash agreement is consistency, not compiler attestation or physical calibration.
