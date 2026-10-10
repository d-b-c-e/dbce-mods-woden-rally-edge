# Wheelkit binding capability

The packaged installer now declares `controlsProfileSchema: 1` and
`adapter: woden-bindings-1` on successful installation. Uninstall omits both.
Wheelkit checks the receipt and hashes of the Core and plugin payloads before
writing controls. This changes installer metadata, not input or force behavior.
Existing installed receipts remain unknown until a reviewed package is installed.

The canonical store stays `BepInEx/config/wheel-bindings.json`, version 1, used
by `Bindings.Load` and F6. The companion setting is `[Wheel] Enabled` in
`dbce.wodenrallyedgewheel.cfg`. There is no second profile input reader.

Wheelkit's candidate maps axes with Woden's actual Rest/End/Centre/Inverted
fields, preserves deadzones and unknown settings, and maps digital actions to
the existing button names. It enables direct input only with all three valid
driving axes. Switching away from H-pattern clears saved gate bindings because
any remaining gate activates Woden's out-of-gear throttle cut. These removals
are previewed; unrelated buttons/camera keys remain.

The native automatic/manual preference is still a separate game setting;
Wheelkit reports it as unsupported. Clutch, select, explicit neutral and gears
above six also remain gaps. A written JSON file does not prove game response.

Candidate verification uses the production Wheelkit Apply/backup/restore path,
independent expected JSON, and this repository's actual `Bindings.Load` and
normalization methods. A wrong camera index and a retained old gear gate must
fail the file contract. See Wheelkit `docs/CONFIGURATION-TESTING.md` for commands.
The clean `94ebfeb` candidate `0.2.14-controls.1` passed 52 suites / 39,138
assertions, 705 UI assertions and 113 exact-package installer checks under
Windows PowerShell 5.1. ZIP SHA-256:
`C3743796C57B17897AF86F5561483C8BB30F1AE75C5DC0C320FDBC087CAAEB8B`.
Private logs: `results/prepare-controls.1.log` and
`results/test-installer-controls.1.log`; player stage under
`components/wheel/dist/player-0.2.14-controls.1`.

Installation and a post-Apply muted game/input check are pending. This is a
local candidate, not a new public release. No owner settings or forces changed.
# Circular profile hats follow-up

Reviewed by Claude (hcom 3586); clean **0.2.14-controls.2** from `cc17cad`
is prepared, not installed. Core 52 suites / 39,138 assertions, UI 705,
production DeviceHub 3,896, and **114 PS5.1 exact-package installer checks** pass.
Archive `components/wheel/dist/WodenRallyEdge-0.2.14-controls.2.zip`, SHA-256
`08487AE6350DB7F22DB01EE77929337E7EAFD67F11FC0FB3EF262D390387346E`.
Logs: `results/prepare-controls.2.log`, `results/test-installer-controls.2.log`.
Wheelkit writer/consumer bridge is `60549e9` (501 tests). No live profile Apply
or physical wheel qualification is claimed by this package.

The first writer's index-only hat check missed a semantic difference: the pinned
legacy native reader emits one exact eight-way bit, whereas STD-033 includes
both cardinal neighbours of a diagonal. Source now adds an explicit
`HatNeighbours` binding flag, default false for all existing F6 bindings. Only
profile-written hats opt in. The installer advertises
`controlsProfileHatNeighbours=true`; Wheelkit also verifies native/Core/plugin
receipt hashes. The controls.1 package predates this correction and is not a
qualified profile-hat candidate.

DeviceHub binds `ReadDeviceStateWithPov` from the exact module already loaded and
pinned by its existing wrapper. It never loads a second library or opens a new
force device. One coherent read supplies axes, physical buttons and four raw
POVs. Legacy exact buttons and circular profile buttons have separate edge
queues; north-to-diagonal remains held instead of retriggering. Failed reads
clear both routes and do not fall back to a second read. Missing raw capability
leaves existing F6 controls available and profile hats inactive.

Toolkit `83b3f93` supplies the shared circular predicate (all 36,000 angles for
each of eight binding directions checked), and `58651c0` supplies the optional
reader. Existing native and managed binary pins remain unchanged. Actual
DeviceHub with a fake coherent delegate passes 3,896 checks, without loading
the native library, opening hardware or launching a game. The new test is a
package gate. Runtime raw-POV delivery and owner acceptance still need a run.

Woden's integer calibration stores the steering midpoint rounded down to 32767
for 0..65535, less than 0.004% from the shared half-unit midpoint. Pedal rest/end
normalization is preserved. Native transmission instructions now correctly say
manual for both sequential and H-pattern profiles.

## Installed 02:06 CT, October 10

The exact controls.2 package above is now installed through its own
`Manage-Install.ps1` under Windows PowerShell 5.1, with the game closed and the
shared rig lease held. All 13 owned payload hashes match the receipt. All five
owner configuration files are byte-identical before/after; no profile was
applied and no game or device was opened. Lease released afterward.

Backup: `WodenWheelBackups/before-install-20261010-020616-286a88aa` in the Steam
game folder. Private evidence: `results/install-controls.2-20261010-0210`
(directory label is approximate; install receipt time is authoritative).
The receipt advertises schema 1, `woden-bindings-1` and profile hat neighbours.
Public beta artifacts are unchanged. Muted replay regression and raw input
observation after production Apply remain separate pending checks.

## Cold playback regression, October 10, 02:50-02:51 CT

Installed controls.2 passed the existing Kenya reference via
`tools/game/Run-StageSession.ps1`, after the 300-second owner-idle gate and under
the shared rig lease. No resolution override was supplied. The runner reached
the stage unattended, played 3,601 poses (maximum position error
`6.103515625E-05` metres and rotation error `1.690385577616028E-05` degrees),
closed the game and restored owner files/preferences. Restore completion:
`2026-10-10T07:51:50.4080391Z`.

Private evidence: `results/controls2-cold-regression-20261010/`, including the
game log, playback result, owner-before/after copies and five game captures.
Frame 03 shows the driving scene across a 7680x1440 window with centre HUD and
the muted-playback banner. Its metadata reports triples On and focused false.
The log confirms the separate-monitor span at (-2560,0). Wheel and motion
outputs stayed muted. No new seam-quality or physical-input acceptance is
inferred from these images.

This is an installed-runtime regression, **not a post-Apply controls test**:
the runner used the sealed reference configuration. Production Apply followed
by independent raw-device/menu/pedal observations remains pending. The lease
was released and Claude notified when the run completed.

## Production Apply and raw startup check, 06:01 CT

`results/raw-controls-20261010-03` freezes Wheelkit `8c7243e` and the original
profile, stages wrong steering/Confirm indexes, then repairs them through
production Apply. Fixture/live previews and applied bytes match; independent
binding checks pass. The addon admits the loaded configuration with schema 2,
while native force and motion/progression output stay muted. The allowed new
CrashEnabled default does not enable force: Enabled remains false and the native
process latch independently refuses output.

The original profile's button 31 produces 84 injected device reads and a real
`WheelInput.Button("Confirm", false)` press/release. The daily notice remains
visible: it bypasses EventSystem, leaving the ordinary menu dispatcher nowhere
to send this binding. This is a runtime control defect, not an Apply failure.
The run closes normally and restores original files/preferences exactly at
`2026-10-10T11:03:00.0207300Z`. Driving remains unqualified. The earlier run02
ended before input because supervision compared local DateTime ticks with UTC;
`4cf7eaa` fixes that conversion without extending the cold expiry.

The source follow-up uses the existing, verified native DailyMessage/title
transition with the normal saved Confirm/Start bindings. Each screen requires
100 ms neutral and a new press; held input cannot skip the next screen. Focus,
panel/capture ownership, failed/ambiguous readers, Back/Escape, native readiness
and unexpected title destinations prevent a wheel transition. Native keyboard
and attract handling continue normally. A failed load restores readiness only
if the native transition has not begun. 759 production UI assertions pass,
including native keyboard retention, ownership, held-input handback and failures.
This is a source candidate pending packaging, cross-review and live check.

The developer observer now records changed raw/normalized/button/menu states
and a fresh baseline for each command, rather than repeating identical values
every poll. Actual car input rows and heartbeat remain per-sample/periodic.
108 production callback assertions cover deduplication, press/release retention,
new-command baseline and bounded keys as well as prior lifecycle failures.

### Native callback correction after cross-review

Claude's review (hcom 4525) identified side effects omitted by the first bridge.
`controls.3` was built and passed 114 PS5.1 installer checks, but was **not
installed**. It is superseded by the callback-scoped bridge.

Re-reading the guarded game's private disassembly at
`artifacts/opening-edge-audit` confirms DailyMessage.Update RVA 0x456090 tests
its ready flag and MenuControls A/Start or legacy anyKey. The accepted branch
clears readiness, updates animation/text, plays StartSfx and calls LoadScene
with the title and both boolean arguments false. TitleScreenScript.FixedUpdate
RVA 0xAFB560 separately increments the attract timer, then checks MenuControls
Start or Return, excludes Escape/Back, and checks Starting. It sets Starting,
hides the prompt, plays StartSfx, may choose another destination from native
state, calls LoadScene with loading=true/third=false, then may start Steam's
public-IP coroutine. These are native side effects, not replicated mod logic.

The revised bridge temporarily sets **only** the existing MenuControls.ButtonStart
for one original callback after a valid saved wheel press. Postfix and finalizer
restore the prior value, including after an original exception. The mod no
longer writes readiness, Starting or a scene. The native demo-start flag at
instance offset 0x58 (generated field_Private_Boolean_0) and the imminent timer
boundary also exclude a wheel press. Keyboard/attract behavior remains native.

Saved controls are read directly through DeviceHub, avoiding camera-key fallback.
The ordinary Unity dispatcher blocks startup Confirm until release, so a press
cannot carry into the next menu. 762 production UI assertions cover native
callback execution, preserved exceptions/state, scoped flag restoration,
started/imminent demo refusal and held Confirm -> release -> new-menu submit.
The plugin builds without warnings. Live menu verification remains pending.
