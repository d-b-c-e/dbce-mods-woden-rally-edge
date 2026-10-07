# Changelog

## [Unreleased]

## 0.2.14-beta.2 — 2026-10-06

Runtime `b47ba15`, driven and approved by the owner.

### Fixed

- The bumper view sits just in front of each car's body (`[Camera] BumperAheadMetres`, default 0.05 m). It used one fixed distance for every car, which put it inside longer cars. Moving it forward or back is saved relative to the body's front, so it carries to other cars.
- Triple screens: the default real-geometry view for bonnet and bumper (`[Triple] MatchGameFov = false`) is what the owner now uses. Turning it on shows each camera's own field of view on the centre screen, and with side screens angled 70° the three screens then span well over 180°.

## 0.2.14-beta.1 — 2026-10-06, first public early-tester build

Runtime `8678f06` (the build the owner drove; later commits change documentation only).

### Added

- Triple screens on three separate monitors without Surround: the mod opens one borderless
  window across them on a normal Steam launch (`[Triple] SpanSeparateMonitors`, on by default;
  STD-015). Surround keeps working as before.
- The countdown-timer assist keeps the race it was used in off the Steam leaderboards
  (`SteamLeaderBoard.UpdateScore` is refused for that race; stock play uploads as normal; STD-018).
- Recording and playback of a stage for automated tests, with physical wheel and network output
  muted during playback.

### Changed

- Camera shortcut step sizes are settings (Advanced → Cameras, toolkit default 0.02 m / 1° / 2° per press; was a fixed 0.05 m).
- Camera numpad defaults follow the family layout (owner, 2026-10-04): 8/2 forward/back, 9/3 up/down, 4/6 left/right, 7/1 tilt forward/back, +/− FOV, 5 reset. Untouched earlier default sets (either old tilt order, or the interim iRacing set) move on load; customised sets are kept.
- Triple screens under Surround (docs/TRIPLE.md): the desktop size joins the game's resolution list, also when the saved index points past the stock list (switching Surround and independent monitors); menu pillarbox hidden on three-screen windows; Forza RPM (nominal scale), gear and HUD speed.

### Fixed

- 0.2.12 candidate: preserve the verified baseline while running separately hashed force-tuning configs into new observations bound to the original case. Reject changed source/baseline artifacts, invalid identifiers/ticks and structurally shifted comparisons before reporting numeric deltas.
- 0.2.11 candidate: correlate one-launch owner recordings to exact source/config/profile identities, fix the hard-coded recording version, and capture force-model validity, reasons, reset epochs and lifecycle gates in stream order.
- Add a bounded owner command that never kills a timed-out game, validates normal finalization and reports exact saved paths/hashes. Add pure model-3 reprocessing into the shared force-observation v1 contract, with exact case-byte identity and refusal of incomplete, dropped, unsupported or idle-only captures.
- 0.2.10: guard startup message/title shortcuts before they consume a Settings press, share the existing device poll once per frame, and include legacy held keys in the close release check. Startup/title live acceptance remains pending.
- Installer revision 3: resolve the package folder after PowerShell parameter binding so the normal Install.bat route works on Windows PowerShell 5.1, including spaced paths and another working directory.
- 0.2.9: retain stock input ownership while settings/capture is active, wait for controls to be released, and add logged F6 paths with an IMGUI fallback. A limited force-disabled menu check passed; broader gameplay acceptance remains open.

## 0.2.8 — private development build

- Persistent Simple/Advanced views with complete basic binding/calibration in Simple and optional countdown assist in Advanced Driving.
- Independent additive handbrake axis/button input, strict Steering-following/explicit FFB dropdown, saved Off preserved.
- Camera shortcut conflict/cancel/defaults, legacy mapping preservation, bounded held-repeat and release gates; pose tuning in Advanced.
- Atomic dashboard connection edits and master toggle preserve active recording; active capture Stop, scale and local support summary.
- Transactional binding/Clear/default saves retain the old effective assignment and exact Retry/Cancel proposal after failure; calibration Save rejects disconnected devices. Camera batch defaults preflight every conflict.
- Bindable Confirm, Back and menu directions for the panel and existing Unity UI handlers; unsupported stock screens retain a visible fallback.
- Player Install/Uninstall entry points with pinned prerequisites, verified updates, backups, rollback, settings preservation and ownership-based removal.
- Source-linked managed UI fixtures; 0.2.7 game-window repair, force model and camera fitting retained. Physical/Unity acceptance remains open.


## Earlier private development builds

### 0.2.7 development build

- Fix the confirmed zero-FFB startup path by capturing the owned Unity window before device enumeration and passing that explicit handle to the pinned toolkit. Missing windows wait without reader churn; exit-guard failures still refuse output. Preserve the original failure through cleanup.
- Raise the fitted bonnet default 0.15 m and move it forward 0.05 m based on the owner's saved correction on another vehicle. Use the observed height/forward position for the fallback; preserve all existing manual views, bumper settings and FFB tuning.
- Add actual adapter/window readiness and camera migration regressions. Record the native 0.5 foreground-window overwrite defect in the toolkit knowledge base; dependency binaries remain pinned.

### 0.2.6 development build

- Allow calibrated wheel controls during the native WARMING/countdown state, including throttle, steering and bound buttons. Preserve the game's start-line braking/lock and existing pause/focus/replay/photo/respawn guards.
- Allow camera cycling, mounted views, rear look and camera tuning before green. Keep FFB gated to active racing independently of camera/input availability.
- Add countdown input tick evidence and regressions for action-table restoration and zero force before green. Keep the current FFB tune; investigate the owner's subjective report of lingering cornering force using a future attended capture.

- Add Setup → Difficulty with saved countdown assist On/Off and 25–100% countdown speed (Off / 75% defaults). Adjust only the owned single-player finish time limit before native expiry; preserve checkpoint bonuses and elapsed lap/stage clocks. Add hook/config regressions and countdown telemetry.

### 0.2.5 development build

- Restore the original FFB output gain after the owner corrected their wheelbase strength setting. Remove the 0.75 post-shaping multiplier; retain the 50% default, configured peak cap and all lifecycle gates. Telemetry identifies the restored curve as model 3.
- Keep the 0.2.4 camera/E-Brake improvements and preserve saved configuration/bindings.

### 0.2.4 development build

- Default FFB strength to 50%; multiply final conditioned output by 0.75, including capped peaks. Saved On/Off and all output gates remain.
- Move E-Brake into Controls → Axes and Buttons; binding either selects that route. Preserve both saved bindings and remove the separate Handbrake tab.
- Fit the default bonnet view to the car body, looking down over the hood; use a 0.03 m near clip while mounted. Add side position and FOV to manual bonnet/bumper tuning, restoring the game lens on release.
- Add camera key/wheel-button rebinding and the shared numpad tuning defaults. Preserve custom camera offsets, migrate untouched old defaults, and save adjustments automatically.
- Record the owner's 0.2.3 drive: felt force, 3,683 native writes with no failed calls; no claim that every stutter is resolved.

### 0.2.3 development build

- Fixed transient FFB suspension repeatedly closing/reopening devices and causing main-thread stalls before force delivery. Added connection/error state and timing diagnostics.
- Replaced session arming with saved FFB On/Off, default On for new settings. F8/Stop saves Off; all device/ownership/driving/watchdog gates remain.
- Added button/axis handbrake selection and calibration. Axis pull scales native rear braking and grip loss; stock throttle cut remains binary. Physical response unverified.
- Removed menu recording controls. Added an expiring one-launch recording request and Steam launcher with automatic capture finalization and unattended force suppression.
- Updated toolkit UX guidance locally for the owner's persistent FFB and external recording workflow.

### Documentation

- Added a session handoff with the installed build, preserved evidence, source
  entry points and next attended test; distinguished current and historical checks.

## [0.2.2-dev] — 2026-09-13

### Fixed

- Moved wheel input into the game's Controls action-reading boundary after the
  owner reported no driving response on 0.2.1. Correctly write and restore boxed
  action-array values; expose applied-input ticks for the retest.
- Preserve latched Stop FFB status through normal inactive-state updates.

### Added

- Bonnet and Bumper in the normal camera cycle, held Look behind, legal stock
  preset preservation, game-camera handback and corresponding FFB suppression.
- Camera-ownership telemetry and input counters, bringing the schema to 199
  definitions. Native cycle policy regressions bring checks to 14 suites / 405 assertions.

### Changed

- Camera shortcut step sizes are settings (Advanced → Cameras, toolkit default 0.02 m / 1° / 2° per press; was a fixed 0.05 m).
- F6 adopts Setup, Controls, FFB, Cameras, Telemetry and Help, common display
  labels, explicit Save calibration and visible Stop FFB/Close without changing
  existing binding keys. Remaining UX gaps are documented.

Installed with nine payload hashes verified and owner configuration preserved.
Effective controls, camera behavior and physical FFB still need an attended test.

## [0.2.1-dev] — 2026-09-12

### Fixed

- Replaced the unsafe generated WheelHit out wrapper with GC-rooted native value
  storage after the first level-entry crash. The owner confirmed successful level
  entry; logs showed sustained contact sampling and normal shutdown.

### Changed

- Camera shortcut step sizes are settings (Advanced → Cameras, toolkit default 0.02 m / 1° / 2° per press; was a fixed 0.05 m).
- Replaced confusing action-like toggles such as `ON Allow FFB` with labeled
  Off/On choices. Preserved the owner's edited settings and bindings on update.

## [0.2.0-dev] — 2026-09-12

### Added

- F6 settings, physical axis calibration/button capture, reconnect and saved
  bindings; provisional load/slip FFB with session arming, F8 stop and toolkit
  lifecycle protections; expanded raw-input/force/recording diagnostics.

F6 and device reads ran, but the first level entry crashed in the contact wrapper.
The 0.2.1 fix above addresses that defect; it is not a physical FFB acceptance result.

## 0.1.0-dev — 2026-09-12

Initial private development foundation for Super Woden Rally Edge. Added game-build-guarded BepInEx IL2CPP plugin, offline dependency generation, detailed telemetry schema and game sampler, reset-aware motion derivation, conservative Forza output, bounded detailed UDP and recording, session inspector, experimental calibrated physical axes and bonnet view, package/initial installer scripts and offline regressions.

Built against installed Steam build 21802346. No live Woden validation or physical force output yet. Wheel/camera/recording default off; telemetry defaults on. Toolkit release pin is v0.12.0; recording is separately pinned unpublished source.
