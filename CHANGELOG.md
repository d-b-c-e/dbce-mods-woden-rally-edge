# Changelog

## [Unreleased]

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
