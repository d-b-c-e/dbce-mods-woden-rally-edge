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
