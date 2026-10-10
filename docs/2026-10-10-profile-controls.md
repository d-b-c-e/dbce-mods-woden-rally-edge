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
The plugin builds without warnings.

### Installed controls.5 and native menu checks, 06:35–06:43 CT

`c5e8aed` was packaged as `0.2.14-controls.5`, with 52 Core suites / 39,138
assertions, 762 production UI assertions, 3,896 DeviceHub checks and 114
PS5.1 exact-package installer checks. All 13 installed payloads match; all
five owner configuration files were retained byte-exact. controls.3 and
controls.4 were validated but never installed. Install evidence:
`results/install-controls.5-20261010`; package evidence:
`components/wheel/dist/WodenRallyEdge-0.2.14-controls.5.zip.json`.

Both `results/raw-controls-20261010-04` and `-05` ran the production Apply
chain on frozen Wheelkit `8c7243e`, including deliberately wrong steering
and Confirm, fixture/live preview and applied-byte parity, and original-profile
raw commands. The loaded configuration retains every applied value; only the
four previously documented defaults are added. Bindings remain byte-exact.

- Saved Confirm 31 dismisses DailyMessage; Start 35 advances Title to MapScreen.
  Both native callbacks log restoration of `ButtonStart=False` afterward.
- The 500 ms right-hat sample moves the game's selected object from Garage
  through Workshop to Arcade (native UI repeat). A fresh Confirm opens Arcade
  route selection; another fresh Confirm opens car selection.
- **Car selection remains a gap:** its selected EventSystem object is the
  now-inactive route button. Another Confirm reaches the real wheel consumer
  but cannot submit this custom screen. No guessed menu presses or driving
  inputs follow. MenuOwnership's normal `Waiting for stock input producer`
  status describes F6 masking, not a failed ordinary navigation dispatch;
  the probe's MenuNavigation status and selected-object trace are the evidence.

Run04: three raw commands / 259 injected reads; normal close and exact owner
files/preferences restoration at `2026-10-10T11:39:48.1050596Z`.
Run05: six raw commands / 508 injected reads; normal close and exact restoration
at `2026-10-10T11:43:24.8276831Z`. Native force is fenced for each whole process,
with independent FFB/telemetry mute. No physical output or owner acceptance.
Startup and ordinary menu input are observed; car selection and driving input
qualification remain open. The temporary probe/native candidate were restored.

### Custom-screen candidate after run05

`CustomMenuHooks.cs` extends the callback-scoped input lease to two verified
consumers, with the original native callback and its side effects retained:

| Consumer | Private native evidence | Inputs and scope |
|---|---|---|
| ArcadeCarSelect.FixedUpdate | RVA `0x761A60`, MenuControls at `+0x40`; ready `+0x24`; debounce `+0x50`; A `+0x42`, B `+0x44`, dpad `+0x34..37` | Confirm, Back, four directions; single-player/player zero, active ready car-selection phase, not transitioning. One fresh press per action, no frame-rate repeats. |
| StagePresentation.Update | RVA `0xA93CB0`; MyControls `+0x98`, ready `+0xA0`, result flag `+0xF0` | Confirm only, ready single-player introduction; results/leaderboard branch excluded. |

Private disassembly and field inventory: `artifacts/menu-consumer-audit`.
No game code or generated assemblies are committed. No global MenuControls
producer hook, synthetic pad, scene jump or native selection setter is used.
An inactive/ambiguous/missing reader, focus or panel/capture loss, active native
input, multiplayer/demo, transition or native debounce resets the 100 ms
neutral gate. A press during debounce is discarded and needs release/repress.
Original keyboard/controller input still goes through the original callback.
No native wheel tune, calibration, physics or saved settings change.

The ordinary Unity dispatcher is suppressed only during the verified native
car-selection phase. It resumes immediately for transmission selection, but a
held Confirm cannot submit the new screen. Postfix and finalizer restore the
borrowed field; an unacknowledged restore blocks subsequent custom-menu
callbacks until exact restoration succeeds. 807 production UI assertions cover
these boundaries, six input fields, native exception preservation, restored
pre-existing true values, and failed-restoration retry. The following run
subsequently qualified the stated subset.

### controls.6 production Apply and native input run, 07:02-07:07 CT

Source `ac308a6`, package `0.2.14-controls.6` (ZIP SHA256
`4C4A806F22E2094DEB0C84E98B44466551EA50466B9290B15A6BF2E535108555`)
passed the same 52 Core suites / 39,138 assertions, 3,896 DeviceHub checks,
807 production UI assertions and 114 PS5.1 installer checks. Claude's source
review (hcom 4661) cleared the callback scope/restoration for a muted live run.
All 13 installed payloads matched; five owner configuration files were unchanged.
Install receipt: `results/install-controls.6-20261010`.

`results/raw-controls-20261010-06` again freezes the production Wheelkit writer
at `8c7243e`, repairs deliberately wrong steering/Confirm, and proves isolated
fixture/live preview and applied-byte parity. No adapter output supplies the
raw commands: those derive independently from the original owner profile.

- Confirm 31 -> DailyMessage dismissed; Start 35 -> title to map; hat right ->
  Arcade; fresh Confirm -> route B -> car selection.
- One right press changes Aalia Strx to Raven Duckson; one left returns to
  Aalia Strx. Each native callback logs exact borrowed-field restoration.
- Confirm opens transmission selection and stays there after the held sample;
  a fresh Confirm selects automatic and reaches round information. Confirm
  enters Spain SS1's introduction; a fresh Confirm reaches the real start line.
- Real car-consumer observations record steering `-0.49998474` / `0.5`, throttle
  `0.5` / `1`, brake `0.5` and handbrake `0.5000076`. The half-value plateaus
  each last 29-31 observer rows; values return to their physical baselines.
  Camera button 32 logs a stock-preset cycle, but the timed close precedes its
  screenshot, so rendered camera behavior remains unqualified by this run.

Eighteen raw commands and 1,518 injected reads were observed. The native force
fence and independent FFB/telemetry mute remain active throughout; zero force
writes and no physical output. Normal bounded exit, exact owner files and raw
registry restoration completed `2026-10-10T12:07:48.0620520Z`; lease released.
Applied config values survived BepInEx loading with only the four allowed new
defaults; applied bindings stayed byte-exact. The temporary native/probe files
were restored. The strict byte check still rejects BepInEx's formatting rewrite;
the separately recorded semantic check plus strict binding check pass.

This proves the tested profile path through menus and sampled driving inputs,
not every screen/action/device. Back, vertical car choices, interruption matrix,
auxiliary sequential/H-pattern shifts, rendered camera and physical owner feel
remain open. The run did not finish a stage, send wheel forces or tune FFB.
