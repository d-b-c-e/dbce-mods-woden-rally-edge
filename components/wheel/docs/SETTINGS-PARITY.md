# Woden settings coverage

Source inventory, 2026-10-22, based on checkout `15bc663389564d8ab8c22cb5054e473ba47f5eca` plus this wording-only candidate. This is a source/fixture inventory, not full parity or installed/runtime acceptance.

Authorities: `../src/WodenRallyEdge.Plugin/Settings.cs` (`Sync`, `Validate` and migrations), `Panel.cs` (pages and actions), `TripleView.cs`, `SpanWindow.cs`, `DesktopResolution.cs`, `MenuBorders.cs`; Core `CameraTuning.cs`, `SettingsPresentation.cs`, `NativeGearboxPreference.cs` supply bounds/defaults. Keys below are exact persisted section/key names; action identifiers are explicitly non-persisted. All config keys below belong to `BepInEx/config/dbce.wodenrallyedgewheel.cfg` unless stated otherwise.

Classification: **E** editable in F6; **R** read-only presentation; **T** runtime-only action/state; **X** intentionally excluded from F6 (config/setup, internal migration or launch tooling). X does not mean unsupported. Boolean range is Off/On. Defaults are new-config defaults, never an instruction to reset an owner setting. A dash means no scalar unit/default applies.

Timing: **live/save** means the existing in-memory value changes now and `Panel.Dirty` schedules the existing debounced save (about 0.7 s), with existing close/save-failure behavior. It does not bypass driving/focus/panel/force gates. **next start** means changing disk does not replace active launch state. External config edits normally require a restart: there is no general live config-file reload. Config-only rows described as consumed live mean their bound entry is read during frames, not that an external editor can hot-reload it.

## Persisted Settings options

| Canonical key | F6 label / location | Class | Units, range, new default | Dependency and timing |
|---|---|---|---|---|
| Interface/View | Simple / Advanced; header | E | choice; Simple | Presentation only; saved immediately; capture/connection/closing edit locks retained |
| Interface/Page | Setup, Controls, FFB, Cameras, Driving, Telemetry, Help | E | choice; Setup | Driving only Advanced; invalid/hidden saved page falls back to Setup; immediate presentation save |
| Interface/ScalePercent | UI scale (%); Help | E | %, 85..150; 100 | live/save; Default scale: 100% action |
| Input/Backend | Setup/Controls backend status; no selector | X | Native/Shim; Native | Launch-owned backend; next start; Shim needs valid shared profile and its existing availability guards |
| Input/NativeGearbox | Gearbox at start; Controls (also Shim Setup) | E | Keep/Manual/Automatic; Keep | next start, once at first loaded main menu; Keep leaves game choice; car-select can change afterward; tooling/replay excluded |
| General/PlayerIndex | Local player index; Advanced Controls | E | index 0..3; 0 | live/save for selected local player; does not reapply the one-shot gearbox preference |
| Wheel/Enabled | Wheel controls; Setup | E | bool; Off | live/save; changing suspends force; input still needs calibrated exact bindings |
| Difficulty/CountdownAssistEnabled | Countdown assist; Advanced Driving | E | bool; Off | live/save; owned single-player time limit only; leaderboard guard remains; no physics/stage clock change |
| Difficulty/CountdownSpeedPercent | Countdown speed (%); Advanced Driving | E | %, 25..100; 75 | live/save; effective only with assist On; Default speed: 75% |
| Telemetry/Enabled | Telemetry; Telemetry | E | bool; On | live via ConfigureNetwork and save; recording independent |
| Telemetry/ForzaPort | Forza UDP port (0 = off); Advanced Telemetry | E | integer 0..65535; 8000 | draft until Apply connection succeeds; 0 disables channel; equal positive detailed port refused |
| Telemetry/DetailPort | Detailed JSON port (0 = off); Advanced Telemetry | E | integer 0..65535; 8001 | same draft/Apply rule; both ports may be 0 |
| Telemetry/DetailHz | Detailed stream rate (Hz); Advanced Telemetry | E | integer Hz 1..60; 20 | same draft/Apply rule; Cancel restores active values |
| Diagnostics/RecordSession | No start-recording control | X | bool; Off | Legacy preference, not a menu start command; explicit prelaunch recording tooling owns capture |
| Camera/BonnetEnabled | Bonnet camera; Cameras | E | bool; On | live/save; includes mount in camera cycle, does not force selection |
| Camera/BumperEnabled | Bumper camera; Cameras | E | bool; On | live/save; same cycle rule |
| Camera/Side | Side position (m); Advanced Cameras, Bonnet pose | E | m -2..2; 0 | live/save; edited bonnet pose disables Fit to car |
| Camera/Height | Height (m); same | E | m 0.1..3; 0.7367809 | live/save; saved manual pose retained; auto-fit uses body measurement |
| Camera/Forward | Forward position (m); same | E | m -2..4; 0.9380049 | live/save; same fit dependency |
| Camera/PitchDegrees | Tilt down (degrees); same | E | degrees -30..30; 8 | live/save; positive looks down |
| Camera/Fov | Vertical field of view (degrees); same | E | degrees 30..110; 70 | live/save; locked during active Physical rig triples; adjustable/single view uses lens; auto-fit bonnet can supply fitted lens |
| Camera/BumperSide | Side position (m); Advanced Cameras, Bumper pose | E | m -2..2; 0 | live/save; bumper mount only |
| Camera/BumperHeight | Height (m); same | E | m 0.1..3; 0.35 | live/save; bumper mount only |
| Camera/BumperForward | Forward position (m); same | E | m -2..4; 2.2 | live/save; fallback when body unmeasurable; edits also update saved body-ahead distance |
| Camera/BumperPitchDegrees | Tilt down (degrees); same | E | degrees -30..30; 0 | live/save; bumper mount only |
| Camera/BumperFov | Vertical field of view (degrees); same | E | degrees 30..110; 70 | live/save; same Physical rig FOV lock |
| Camera/BumperAheadMetres | No separate slider | X | m -1..2; 0.05 | Body-relative bumper placement; updated by bumper pose editing/shortcuts/reset; live/save through those routes |
| Camera/AutoFitBonnet | Fit to car; Advanced Cameras, Bonnet pose | E | bool; On | live/save; switching Off starts from currently shown fitted pose; manual pose edits turn fit Off |
| Camera/MoveStepMetres | Move step per press (m); Advanced Cameras | E | m 0.005..0.25; 0.02 | live/save; active mount shortcuts only |
| Camera/TiltStepDegrees | Tilt step per press (degrees symbol); same | E | degrees 0.1..10; 1 | live/save; active mount shortcuts only |
| Camera/FovStepDegrees | FOV step per press (degrees symbol); same | E | degrees 0.5..10; 2 | live/save; FOV lock still applies |
| Camera/DefaultsVersion | No player control | X | migration integer; field starts 0, constructor advances to 2 | One-time legacy default migration; custom positions retained |
| Display/ShowFrameRate | Frame rate display; Cameras | E | bool; Off | live/save; current fps and 10 s 1% low, center screen |
| ForceFeedback/Enabled | FFB: Off / On; FFB | E | bool; On | SetEnabled saves preference; driving and all safety gates remain; F8/Stop saves Off |
| ForceFeedback/DeviceGuid | FFB device; FFB | E | exact GUID; empty | live reconnect/save; exact device only, missing saved selection retained, no fallback |
| ForceFeedback/FollowSteering | Use steering wheel; FFB picker | E | bool; On | live reconnect/save; requires selected Steering identity; explicit GUID independent |
| ForceFeedback/SelectionVersion | No player control | X | migration integer; starts 0, advances to 1 | Legacy explicit GUID implies follow Off; no retuning |
| ForceFeedback/StrengthPercent | Steering strength (%); FFB | E | %, 0..100; 50 | live/save; Grip tire force only, Classic also scales damping; Default steering strength: 50% |
| ForceFeedback/CrashEnabled | Crash effects; FFB | E | bool; On | live/save; independent crash push/rattle |
| ForceFeedback/CrashStrengthPercent | Crash strength (%); FFB | E | %, 0..100; 50 | shown when crash On; live/save; independent of steering strength and Classic peak cap |
| ForceFeedback/Model | Saved-model explanation; no picker | X | Grip/Classic; Grip | Saved Classic retained, including legacy FFB file without Model; Reset FFB tuning explicitly selects Grip |
| ForceFeedback/GripLoadRatio | Reference (x front load); Advanced FFB | E | ratio 0.2..10; 2 | Grip only; live/save; higher lighter; physical normalization pending |
| ForceFeedback/GripSmoothing | Smoothing; Advanced FFB | E | per-update factor 0..0.95; 0.2 | Grip only; live/save |
| ForceFeedback/PeakPercent | Peak output cap (%); Advanced FFB | E | %, 0..50; 25 | Classic only; live/save |
| ForceFeedback/LoadReference | Reference front load; Advanced FFB | E | Unity force units 100..50000; 6000 | Classic only; live/save; uncalibrated |
| ForceFeedback/SlipScale | Slip response scale; Advanced FFB | E | game slip units 0.02..3; 0.35 | Classic only; live/save |
| ForceFeedback/SmoothingMs | Smoothing (ms); Advanced FFB | E | ms 0..200; 35 | Classic UI only; live/save |
| ForceFeedback/Damping | Steering damping; Advanced FFB | E | factor 0..0.5; 0.05 | live/save; Grip independent gain, Classic scaled with strength; steering binding required |
| ForceFeedback/Invert | Invert force direction; Advanced FFB | E | bool; Off | live/save; reverse output sign; stop car before changing |

## Triple persistence owners

These are all 13 `[Triple]` binds in the four owners. Geometry is not editable in this F6 panel. No geometry defaults or saved values change in this task.

| Canonical key / owner | F6 label / classification | Units, range, new default | Dependency and timing |
|---|---|---|---|
| Triple/Mode; TripleView | Triple screens composite; E | Off/Auto/On; Auto | Rendering policy read each frame; Auto needs window width >= 2.9 x height. F6 shows Off/Surround/Separate monitors, retains hand-set On for non-Off selections. Window layout next start |
| Triple/SpanSeparateMonitors; SpanWindow | Triple screens composite; E | bool; On | Off -> (Off,false); Surround -> (Auto or retained On,false); Separate -> (Auto or retained On,true). Span substitution only during native startup resolution apply; matching primary-centered three-monitor row required; next start |
| Triple/MatchGameFov; TripleView | Triple FOV: Physical rig / Adjustable FOV; E | bool; Off (Physical rig) | live/save. Adjustable derives virtual eye distance; switching can initialize only active manually owned mount lens from shown angle, bounded 30..110. Physical mode locks mounted FOV shortcuts/slider |
| Triple/ChaseUsesGameFov; TripleView | No F6 control; X | bool; On | Frame-consumed; stock chase retains native lens even in Physical rig mode; config edits next start |
| Triple/ToggleKey; TripleView | No F6 editor; X | InputSystem key name / None; F11 | Launch-bound preference; key toggles temporary off state, not saved Mode |
| Triple/CenterHud; TripleView | No F6 editor; X | bool; On | Frame-consumed with active triples; native HUD/menus centered; config edits next start |
| Triple/PanelWidthMm; TripleView | No F6 editor; X | mm >0; 708.4 | Frame-consumed rig geometry; invalid dimensions throw/refuse projection, not clamped; config edits next start |
| Triple/PanelHeightMm; TripleView | No F6 editor; X | mm >0; 398.5 | Same; all panels share dimensions |
| Triple/EyeDistanceMm; TripleView | No F6 editor; X | mm >0; 660 | Physical rig; Adjustable or stock chase may derive virtual distance instead; config edits next start |
| Triple/SideAngle; TripleView | No F6 editor; X | degrees 0 inclusive..90 exclusive; 70 | Frame-consumed geometry; config edits next start |
| Triple/BezelMm; TripleView | No F6 editor; X | mm >=0; 8 | Frame-consumed geometry; config edits next start |
| Triple/HideMenuBorders; MenuBorders | No F6 editor; X | bool; On | Wide-window check every 0.5 s; suppresses menu border images; not a reversible F6 toggle; config edits next start |
| Triple/OfferDesktopResolution; DesktopResolution | No F6 editor; X | bool; On | Startup resolution-list extension only; native Options owns selected resolution; next start |

## Binding, action and status inventory

Actions below have no scalar config default/range unless specified. `action:` denotes a local UI action, not a newly invented persistent key. Native bindings use the existing bindings JSON; shared bindings use `dbce-controls.ini`. Canonical binding names stay unchanged even where their labels differ.

| Canonical identifier(s) | Labels / location | Class | Range/default/dependency and semantics |
|---|---|---|---|
| bindings: Steer, Throttle, Brake, Handbrake axes | Steering, Throttle, Brake, Handbrake (axis); Native Controls | E | Exact device/axis/calibration; unbound default. Steering normalized -1..1, pedals 0..1. Bind/Calibrate provisional until Save calibration; Clear commits removal; failure retains previous active binding |
| action: axis deadzone / inversion | Deadzone, Deadzone (%), Invert calibration | E | Deadzone 0..25%; new pedal capture 3%, steering 0%; saved values/endpoints/identity retained. Inversion bool Off for new binding; Save deadzone or Save calibration commits; Cancel/timeout preserves old; no implicit recapture |
| bindings: Gear up, Gear down, Handbrake, Camera, Rear view, Respawn | Shift up, Shift down, Handbrake (button), Change camera, Look behind, Reset car; Driving buttons | E | Exact digital binding; unbound default. Bind/Clear immediate commit after successful capture; handbrake axis/button additive; native keyboard stays game-owned |
| bindings: Pause, Confirm, Back, Menu up, Menu down, Menu left, Menu right | Same names; Menu buttons | E | Unbound default; exact digital binding, same capture/commit rules; direction press and release gates retained |
| bindings: Settings panel, Panic stop | Settings, Stop FFB; Mod buttons | E | Unbound default; F6/F8 reserved keyboard routes; same binding rules |
| bindings: Lights, Horn, Records, Next song | Same names; Extra game buttons | E | Unbound default; native game actions, same binding rules |
| bindings: Gear 1, Gear 2, Gear 3, Gear 4, Gear 5, Gear 6, Gear R | Same names; Shifter bindings | E | Unbound default; exact held gates, native Manual required; out-of-gear handling retained; no clutch route |
| CameraKeys: Camera, Rear view | Change camera, Look behind binding | E | Keyboard default None; wheel buttons independent; Bind/Clear saved on successful commit |
| CameraKeys: Camera up/down/forward/back/left/right/pitch up/pitch down/wider/narrower/reset | Move up/down/forward/back/left/right, Tilt up/down, Widen FOV, Narrow FOV, Reset view; Adjustment bindings | E | Defaults Numpad9/3/8/2/4/6/1/7/Plus/Minus/5 in that order; active mount only, focus/panel/release gates, physical FOV lock; conflict/chord refusal retained |
| action: Restore numpad defaults | Cameras, expanded Adjustment bindings | T | Explicit batch reset of adjustment bindings only; preflight conflicts; preserves camera-cycle binding and saved poses |
| shared controls: steer/throttle/brake/handbrake; shiftUp/shiftDown; neutral/reverse; confirm/back/start; navUp/navDown/navLeft/navRight; camera/lookBack/reset/horn/modMenu/muteFfb; gear1..gear6 | Steering/Accelerator/Brake/Handbrake; Shift up/down; Neutral/Reverse; Confirm/Back/Start / pause; Navigate up/down/left/right; Change camera/Look behind/Reset car/Horn/Settings/Stop FFB; Gear1..Gear6; Active / Next start / Draft | E/R | Unbound unless supplied by launch profile; steering -1..1, other axes 0..1, digital 0/1. Sequential and H-pattern rows require corresponding saved Transmission; focused main-menu and qualified reader required; not Native JSON. Bind from selects an already-open device; Bind/Clear/Invert axis or Use normal axis change draft only; no Find devices or backend switch |
| shared controls: clutch/select/gear7/gear8 | Clutch / Select / Gear7 / Gear8; Controls | R | Unsupported, not delivered by current shared adapter; existing saved bindings retained; no edit/active delivery claim |
| action: shared Save bindings / Discard changes / Cancel capture | Controls | T | Editability, row notes, selected device and capture guards retained; Save persists next-start revision, active unchanged; Discard reverts draft; failed Save stays visible and retryable |
| status: shared source/access/capture | Shared controls, access reason, saved-for-next-start, unsaved, Save failure, timeout | R | No settings writes; current host state, unavailable devices never substituted |
| action: Native capture Save / Retry save / Cancel | Calibration/button capture | T | Save calibration, Save deadzone or Retry save; Cancel leaves previous binding active; fresh-neutral/timeout checks retained |
| action: Refresh devices / Refresh / retry | Setup, FFB, Advanced Help | T | Refresh discovery/reconnect through existing runtime guards; no persisted tuning changes |
| status: live input bars and ready-next-step | Setup/Controls | R | Current exact input, availability, calibration/endpoints and shifter/menu status; no persistence |
| action: Bind next required axis / Optional controls and buttons | Setup | T | Native backend, binds missing required control or opens Controls; no automatic calibration save |
| action: FFB Off/On / Stop FFB / F8 | FFB/header/footer | T | Saves Enabled through existing force path; Stop saves Off, never auto-resumes force |
| action: Default steering strength: 50% | FFB | T | Explicit strength reset only; live/save |
| action: Reset FFB tuning | Advanced FFB | T | Explicit Grip, ratio 2, smoothing 0.2, strength 50, peak 25, ms 35, damping 0.05, reference 6000, slip 0.35, invert Off; preserves Enabled/device and crash settings |
| status: FFB target/force/Grip/crash/calls/failures | FFB | R | Current runtime output observations/reasons, no physical torque acceptance |
| action: Triple screens / Triple FOV / Use Adjustable FOV | Cameras | T | Writes exact keys described above through existing save timing; physical FOV switch does not reset other pose/tune |
| action: Bonnet pose / Bumper pose | Advanced Cameras | T | Editor selection only, no persisted selector; no immediate view cycle |
| action: Reset this view | Advanced Cameras | T | Explicit selected mount defaults; bonnet fit On, bumper ahead 0.05; other mount unchanged; live/save |
| action: Default steps: 0.02 m, 1 degree, 2 degrees | Advanced Cameras | T | Explicit three step defaults; live/save |
| status: projection angles/FOV lock/active mounted camera/frame rate | Cameras | R | Current center angles or unavailable, active mount text and 10 s fps summary; no persistence |
| action: Default speed: 75% | Advanced Driving | T | Explicit assist-speed reset; does not enable assist; live/save |
| status: assist/leaderboard guard | Advanced Driving | R | Current runtime reasons and estimated 60 timer-second driving duration; no clock/force acceptance |
| action: Apply connection / Cancel / Restore connection defaults | Advanced Telemetry | T | Validated Apply alone replaces active outputs; Cancel reads active config; Restore edits draft to 8000/8001/20 only, still needs Apply |
| action: Stop recording | Telemetry | T | Only while recording active; stops explicit prelaunch capture, no in-menu recording start |
| status: telemetry worker/active destinations/counters/recording | Telemetry | R | Active versus draft retained; Sending UDP has no receiver acknowledgement; drops/send errors/overwritten ticks read-only |
| action: Submit GitHub Issue / report fields / Include log / Review report / Cancel report | Help (Panel.Support.cs) | T | Existing draft/report workflow; title 200 characters, description 10000, recent log <=24 KB; opt-in log; local review before outgoing draft; no automatic issue submission |
| action: Previous page / Next page / reviewed confirmation / Open reviewed GitHub draft / Open report folder | Expanded Help report | T | Prepared preview/review required; local folder action only after report exists; browser draft sends reviewed text; task does not exercise browser/network |
| action: Mod on GitHub / Wheelkit on GitHub | Help | T | Existing links; no config writes |
| action: Create support file (local) | Help | T | Existing local WodenSupport text report; explicit invocation only |
| action: Default scale: 100% / Device details / Next device | Help | T | Scale resets only on explicit click; details/selected device page are local presentation; raw axes/GUID/capabilities/status are R |
| action: Simple / Advanced / page tabs / Details (Advanced) / Connection settings (Advanced) / custom tuning review | Header and page links | T | Navigation only; persistent View/Page, capture/connection edit locks; no retuning |
| action: F6 / Close / Cancel close / Keep settings open / Discard and close / Keep editing | Persistent header / close prompt | T | Existing release-to-close and unsaved shared draft decisions; no active/draft/restart behavior changes |
| action: Up / Down / scroll / Page Up/Down / Tab / Enter / menu directions | Persistent navigation | T | Scroll/focus/activation only; visible bounds/accessibility retained |
| status: footer save/error/hotkey/capture / version/help | Header/footer/Help | R | Saved, saving, failure, release-close and shared Save warning precedence retained |
| status: Shim pilot bars/transmission/launch profile | Shim Setup | R | Active profile from launch, unavailable clutch/select/gears 7-8 explained; no silent fallback to Native |

## Page audit and toolkit reconciliation

Source offers real options/actions on every visible page: Setup (wheel toggle, required-axis next step), Controls (Native or shared editing and gearbox preference), FFB (enable/selection/strength/crash), Cameras (layout/FOV/mounts/fps), Telemetry (toggle/status/connection and conditional recording stop), Help (scale/support/report), and Advanced-only Driving (countdown assist). A disconnected device does not make these pages empty. `SettingsPresentation.Page` normalizes invalid pages and Simple Driving to Setup. This satisfies the current source inventory portion of STD-017; fixtures are simulated Unity, not rendered-game acceptance. STD-004 wording is aligned only for the five confirmed strings; STD-030 remains partial for its existing runtime/input gates.

## Existing Wheelkit coverage and concrete differences

Retained hub reference: `E:/Source/dbce-project-mgmt/artifacts/woden-settings-parity-20261013/PARITY.md`, item 3 and merged items 1-2 (`aa7af85`; earlier 229/229 focused, 1744/1744 Core and port follow-up 159/159). These historical checks are preserved, not rerun here. Current source cross-check: `E:/Source/toolkits/dbce-wheelkit/tools/Catalog-Integrations.ps1`, Woden block and `Geometry`, plus catalog-v6 receipt-gated gearbox append. No Wheelkit/toolkit source was edited or its app/installed catalog qualified here.

Wheelkit generator covers FFB Enabled/Strength/CrashEnabled/CrashStrength/Damping/Invert, composite TripleScreens and hidden Mode/Span keys, BonnetEnabled/BumperEnabled, ShowFrameRate, five rig geometry fields, Telemetry Enabled/ForzaPort with hidden DetailPort pair validation, and receipt-gated NativeGearbox. Its app and standalone setup share the catalog per retained review. The five F6 labels now match Crash effects, Invert force direction, Frame rate display, Bonnet camera and Bumper camera. Existing American wording in touched Panel lines is already present; no broader text or shared binding rewrite is needed.

Remaining differences are deliberate or unresolved, not a claim of full parity:

- F6 retains explicit Triple Mode=On while changing non-Off layout; Wheelkit composite writes Auto. F6 can change rendering policy live but span/window placement remains next start. External setup applies disk values for next launch.
- F6 now offers Triple FOV Physical rig/Adjustable FOV and Use Adjustable FOV. The inspected Woden generator has no MatchGameFov item. The retained October 13 note saying config-only is historical.
- Wheelkit offers measured rig geometry; F6 intentionally does not. Woden validates positive dimensions/eye distance, nonnegative bezel and side angle <90; generic generator fields have narrower UI ranges (width/eye 100..3000 mm, height 100..2000 mm, bezel 0..100 mm) and SideAngle max 90, whose endpoint Woden rejects. This task does not change either range.
- F6-only or config-owned: wheel toggle/player/backend, Native live calibration/binding actions, shared Active/Draft/Next-start workflow, countdown assist, camera poses/fit/steps/body-ahead setting, model-specific FFB controls/reset, UI scale/presentation, detailed stream rate/Apply/Cancel and runtime recording/support/status actions. Wheelkit's shared rig-profile Apply is a different route from F6 Native JSON editing.
- F6 says Steering damping versus Wheelkit Damping, and Forza UDP port (0 = off) versus Telemetry port. Wording beyond the five confirmed changes stays unchanged. Grip Smoothing is not a Woden catalog item.
- Both source paths allow Forza port 0..65535 and reject equal positive output ports; Wheelkit keeps DetailPort hidden for that constraint while F6 edits both as one draft. File validation does not prove active receiver delivery.
- Config-only FFB Model, Triple HUD/chase/toggle/border/resolution controls, migration markers and legacy recording preference are not new player controls. Saved Classic/default migrations and owner's force tune are preserved.

## Verification boundary

Only Panel strings and affected existing source-linked UI fixture expectations change. The harness links actual Panel.cs/Settings.cs; fake Unity/devices cannot qualify the installed game, focus, physical buttons, camera scanout, force feel or public parity. Per-run snapshots now stay under their unique `artifacts/ux-ui-fixture/<run-id>` directory to preserve earlier snapshots. Build/run logs and slot custody evidence are under `artifacts/settings-parity`. No installer, package, game, input, display, device or force run is authorized by this inventory. STD-016/020 remain unchecked; STD-031/037 remain pending. Shared binding presentation/native prompt GLY work is unchanged.

Final source-linked verification: Release build `ui-build-02.log` succeeded with zero warnings/errors; `ui-run-02.log` passed 6,810 UI assertions plus 95 managed shim/lease checks. `ui-result-02.json` records exact-slot release and absence. Run 01 (6,782 assertions) is retained; run 02 adds explicit reachability checks for both camera labels. Private native ABI was not run. Fixture outputs: `artifacts/ux-ui-fixture/3c87de2908c2475ab80bf63c4ae2d273`; earlier run `ab224b4061a643d8b96828555228c3e5` is retained.
