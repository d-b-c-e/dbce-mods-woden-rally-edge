# Native wheel prompts: incomplete source investigation, October 22

## October 23: one stock prompt chain decoded (selu)

**The missing scene/widget prerequisite is resolved for one prompt.** Stock
prompts are not separate widgets or sprites: they are font characters inside
ordinary UI Text strings. Offline only: no game, device, input, display or
owner-file access beyond read-only reads of the supported install
(GameAssembly F422894D, metadata 3B5D7167 as recorded below). No product code
changed; GLY/STD-020 remains unimplemented.

| Link | Evidence |
| --- | --- |
| Glyph | `Language` statics `BUTTONA..BUTTONSTART` are set once by `Language..cctor` (18073DA10) to "①".."⑩" (A, B, X, Y, LB, LT, RB, RT, Select, Start); `TEMPBUTTON` = "④". The Nougat font (sharedassets0 pathID 1105) and SuperWodenFontCommon both map U+2460..2469; rasterizing Nougat draws them as the pad A/B/X/Y, LB/LT/RB/RT, View and Menu buttons. |
| Device choice | `GamePadSystem.LastInput` (static, enum Gamepad=0, Keyboard=1, Wheel=2). The writer swaps in "「Enter」/「Esc」/「Space」" only for Keyboard, so **Wheel shows the gamepad glyphs**. |
| Scene object | level3 `Menus/MenuTree/ARC_RoundInfo`, `ArcadeHeaderText` pathID 51577, serialized `Header` → `ARC_RoundInfo/Arc_HeaderUp/text`, `Mode` = 0. Siblings: `ARC_ROUTE SELECT` (Mode 1), `ARC_CarSelect` (Mode 2). |
| Writer | `ArcadeHeaderText.OnEnable` (180A9B300), Mode 0: `BUTTONA + Language.Confirm + " - " + BUTTONB + Language.Back` → `SpecialFunctions.SetText(s, 3, 0)` (word casing only) → static `MenuCameraScript.Infotext` (180476E50) → `GameObject.Find("Infotext")`, `GetComponent<Text>`, then `Text.set_text("■" + s)` through vtable +0x5E8 (slot 75 = `set_text`, class vtable base 0x138). |
| Widget | `Menu Camera/CANVAS/InferiorInfo/tx/Infotext` (level3 GameObject 6117, active; serialized text "■INFO:"; also carries `TypeWriter`, `Outline_Plus`, `Animator`). The same path exists in level0–level9 (four inactive). |
| Refresh | Once per `OnEnable` of the header object; there is no per-frame re-read of `LastInput` in this writer. |

The rendered English result is "■ⒶConfirm - ⒷBack" in the bottom info bar.
Other `Language.BUTTON*` readers found by the same scan, not traced further:
`ArcadeMode.OnEnable` (BUTTONX with "[Space]"), `PhotoMode.Update`
(X, B, LT, RT, LB, RB, A with "[Esc]/[Enter]/[X]/[Mouse]"), and eight
`Rally_Edge_Event_Info_Screen` methods (BUTTONY via TEMPBUTTON). The vtable
base also resolves the earlier unresolved `EnhancedButton.Update` slot 2A8 to
`Graphic.set_color` (slot 23), consistent with its selection-color reading.

All 47 serialized `EnhancedButton`s decode byte-exactly from their 14 public
fields. None references a prompt Text, `ControllerButtons` is empty and
`MyControls` is null in every instance, and only two have `SelectWithButton`
(event `InfoButton` key A, `StageInfo Button` key Y). EnhancedButton is
therefore not the prompt producer. The `Name Input/BUTTONCONTAINER` A/B/X/Y
Texts are on-screen keyboard letters, not prompts.

Method: type trees are stripped and TypeTreeGeneratorAPI 0.0.10 cannot load
metadata v39, so MonoBehaviours were parsed by hand (UnityPy 1.25.4 in a
scratch venv; a layout is accepted only when it consumes every byte). Code
references come from LibCpp2IL + Iced global resolution. Scripts, outputs,
the glyph raster and SHA-256 sums are retained privately in the hub at
`artifacts/woden-prompt-chain-selu-20261023`; no game data is committed here.

Implication for GLY: a wheel prompt needs no scene hook. Either rewrite the
"①".."⑩" characters in the string that reaches `MenuCameraScript.Infotext`
while the wheel is the active input, or temporarily set the `Language.BUTTON*`
statics before the stock writer runs and restore "①".."⑩" afterwards. The next
stock `OnEnable` write is the natural restoration point. Not yet established:
whether `TypeWriter` re-sets the text after `Infotext`, whether the mod's wheel
reader ever makes `LastInput` report Wheel, and the rendered result. These
need source design plus fixtures, then a separately authorized muted run.

## October 23: review corrections (lufa 339197)

- **Stock pad ↔ keyboard switches.** The on-screen refresh identity is now the wheel labels,
  or `"stock " + GamePadSystem.LastInput`. A direct pad↔keyboard switch on screen, with no
  wheel press in between, therefore redraws the stock text for the new branch; before, both
  stock branches shared one identity and the pad text stayed. Each arcade screen has its own
  `ArcadeHeaderText` with a fixed serialized `Mode`. Moving between screens enables another
  header, which runs its own stock `OnEnable` and becomes the tracked one.
- **Fail-closed blast radius.** The `ArcadeHeaderText.OnEnable` and `MenuCameraScript.Infotext`
  hooks no longer carry `[HarmonyPatch]`. `WheelPrompts.Apply` patches them after the
  required `PatchAll`, next to `SpanWindow.Apply`. If either hook fails, the half-applied one
  is unpatched, one warning is logged and stock prompts stay. Controls, force and F6 keep
  their required fail-closed path. Native pad reads are scanned only once the pair is applied.
- **Inert fallback (lufa 340745).** `Apply` clears `Applied` and `Scoped` before registering and
  again on failure. It records each target before calling `Patch`, so a `Patch` that throws after
  installing is still rolled back. While `Applied` is false, a hook that survives that best-effort
  rollback does nothing: the header prefix neither scopes nor tracks, the `Infotext` prefix runs
  stock, and `Update` drops any tracked header without refreshing it.

## October 23: remaining BUTTON* writers audited; header refresh on device switch (selu)

Static audit (hub `artifacts/woden-prompt-chain-selu-20261023/out/remaining-writers.asm`).
None of the other stock writers has a proven wheel route for its glyphs, so all stay stock:

| Writer | Prompt | Why no wheel label |
| --- | --- | --- |
| `ArcadeMode.OnEnable` (180813AB0) | `TEMPBUTTON` = `BUTTONX` ③ or "[Space]", then `Infotext(Language.PressYforInfo)` | X/Y are not dispatched by any wheel route |
| `Rally_Edge_Event_Info_Screen` (OnEnable + 7 helpers) | `TEMPBUTTON` = `BUTTONY` ④ or 「Space」 | Y (`StageInfo Button` EnhancedButton key Y) has no wheel route |
| `PhotoMode.Update` (180AD2070, every frame) | Watermark ③, Tilt LT/RT, Zoom LB/RB, TakePhoto ①, Exit ②; keyboard "[X]/[Esc]/Pg↑/↓/[Mouse]/[Enter]" | Only ①/② are dispatchable. In photo mode, wheel Confirm/Back go through `MenuNavigation`'s EventSystem dispatch (paused), which is not shown to reach photo mode's take/exit consumers. |

The arcade header remains the one coherent consumer. Its three modes (route select, round
info, car select) all go through `Infotext`. Wheel Confirm on all three was observed live on
2026-10-10 (`docs/2026-10-10-profile-controls.md`). Wheel Back there is not runtime-evidenced.

`ArcadeHeaderText.OnEnable` only builds and writes text: `SpecialFunctions.SetText`,
`Infotext`, and the Header title. It has no `SetActive`, sound or state write. The stock
header redraws only on enable, so a device switch on screen used to leave stale text.
`WheelPrompts.Update`, called from `Panel.Update` right after `MenuNavigation`, now re-runs
that stock `OnEnable` when the wanted text changes while the recorded header is still
`isActiveAndEnabled`. The wanted text is "" for stock, or the wheel labels. It changes on a
wheel/native switch, Shim ownership, or a binding or reading-device change. The refresh
replays the stock Infotext animation. A hidden header is forgotten, and a failing refresh
logs once and stops.

Checks: Release build `-warnaserror` 0/0. UiTests 6,867 assertions plus 95 Shim, with 11
new on-screen refresh cases: wheel press, pad press, keyboard press, device stops reading,
Shim, return, hidden header, failure, and no refresh without a change. With the refresh
disabled, the suite fails at the wheel on-screen assertion. Not installed, run or rendered.

## October 23: arcade header source implementation (selu)

Source and fixtures only; not installed, run, rendered or owner-accepted.

- **Hook.** `WheelPromptHooks.cs`: an `ArcadeHeaderText.OnEnable` prefix/finalizer opens a
  scope for that one refresh. The `MenuCameraScript.Infotext` prefix rewrites the string
  inside that scope, then re-invokes the stock writer once (re-entry guarded) instead of
  relying on by-ref string marshaling. The stock writer still sets both the Text and its
  `TypeWriter.Inputstring`, which retypes the Text, so the typewriter shows the same text.
  No `Language` static or widget is changed. The next stock refresh restores pad/keyboard
  wording, and nothing needs restoring at disable/teardown.
- **Rewrite.** `WheelPromptText` (Core) replaces ①/「Enter」 → Confirm, ②/「Esc」 → Back and
  ⑩ → Pause with 「Button N」/「HAT N …」 from the shared `DigitalInput.Label`. A
  replacement needs a valid Native binding on a device that `IsReading`. Unbound or
  unreadable actions, and every other glyph (X/Y/shoulders/triggers/Select), stay stock.
- **Wheel-active state.** `GamePadSystem.LastInput` cannot provide it: the game writes only
  Gamepad=0 or Keyboard=1 (nine writers) and never Wheel=2, and its readers compare against
  those values. The mod therefore records its own `MenuInputSource`:
  - wheel when `MenuNavigation` delivers, or a `CustomMenuLease` or `StartupButtonLease`
    is taken;
  - native when the existing `ReadInputs` postfix sees `MenuOwnership.Held(pad)`. That
    check is scanned only in menus or pause, not while driving.
- **Shim.** Shared-profile (Shim) ownership keeps stock glyphs, because Shim wheel input
  arrives through the game's pads.
- **Checks.** Release build `-warnaserror` 0/0. UiTests: 6,856 assertions plus 95 Shim
  checks, including 19 new `WheelPromptChecks` assertions. These cover wheel vs pad vs
  keyboard, a later native press, refresh replacement, unbound or unreadable bindings,
  driving vs pause scans, scope, native failure cleanup, Shim, and all three wheel delivery
  points. With `Active` forced false, the suite fails at the wheel assertion. Core Tests:
  94 suites / 51,700 assertions.
- **Open boundaries.**
  - Whether a Harmony detour on the static `Infotext` sees this native-to-native call, and
    whether the re-invocation reaches the original once.
  - Whether the owner's wheel ever appears as a Unity gamepad or in `Game_Pads`. If it
    does, its own presses would count as native.
  - The rendered 「Button N」 width in the bottom bar at triple layouts.
  - The other `Language.BUTTON*` writers (PhotoMode, ArcadeMode, event info), which stay
    stock.

## Finite offline prerequisite 01M5JTFN6N750578VKCN0RTMS0 (lido)

**Negative result; prerequisite complete, parent still blocked.** This attempt
`01M5JTNGP7NGB37YY44F78X8B5` inspected existing local serialized-input availability
and retained indirect-call evidence from clean
`1518854bd6f0cb899f0b3ea4fc8d82516cd5e074`. Retained parent
`01M5HZX0K1CDPFPQ149F1C2E9Y` must remain in engineering-wait. Nothing below
supersedes the prior negative evidence, baseline results or restoration gates.

### Inputs and bounded availability

Read-only SHA-256 receipts, October 22. Game-relative paths below resolve under
`D:/Program Files (x86)/Steam/steamapps/common/Super Woden Rally Edge`;
`Data/` abbreviates `Super Woden Rally Edge_Data/` only in this table.

| Input | SHA-256 |
| --- | --- |
| GameAssembly.dll | F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C |
| Data/il2cpp_data/Metadata/global-metadata.dat | 3B5D7167AEA83FEBDC625F1635FC7CB414FF13CA609D78CC32158133C3877815 |
| Data/level0 | 72568EBFC97CAFABBA71C46EB578F041686E926BCC5A6186EA2F6D2FD60E367B |
| Data/level1 | 270E79AABDE580E62746BDC7F92F68209421AA7376D4B70C21C68690585A436A |
| Data/resources.assets | EE3957A98C2511C6A0AC7DB4F5F7701C4D6477A79D65C3B4A926AE5A785E4304 |
| Data/sharedassets0.assets | B3128A48839C42E69F5F053BE3AAE4FCFBD690C29C9C240AF1E1818F03AB91A4 |
| artifacts/wheel-prompts/EnhancedButton.Update.asm | 155716C7902945629B7A5C079FCECBFA40B0FD647A2B49BF0CEABEABCD61F0EA |
| artifacts/wheel-prompts/EnhancedButton.MoveNext.asm | F138A4FBDE06DB174F5FDB43D75302A72CA8645CD9A688465B57414D0D9F50B4 |
| artifacts/wheel-prompts/GamePadSystem.all.asm | 4F822A4072B5832B07EF036FC73F302688C5171F8DE4A7894BCC60A421675558 |

The installed Data directory contains `level0` through `level37`,
`globalgamemanagers`, `globalgamemanagers.assets`, `resources.assets` and
`sharedassets0.assets` through `sharedassets37.assets`. Raw assets are present;
their presence does not identify which scene contains the prompt. The four
asset receipts above identify available samples, not decoded or qualified scenes.

No `.unity`, `.prefab`, scene-JSON or asset-JSON export was found in this
checkout's artifacts or the canonical Woden checkout's artifacts by the bounded
filename search including ignored files; the seven matching JSON paths were
NuGet `obj/project.assets.json` files. The existing `ux-ui-fixture` material is mod-panel fixture
output, not an exported native scene reference graph. Existing
`artifacts/wheel-prompts/Audit.csproj` and `Program.cs` consume GameAssembly and
metadata through LibCpp2IL/Iced; they do not decode Unity serialized objects.
Their hashes are respectively
`08E8D84CEA8C138EE013285CB5D67A715A4451493012E25CC009E016E87160B2` and
`09D6A4611C0FBB5E3742F035E9D1D07492DDFB1362B46FCDF205025F48120CE5`.
UnityPy was not available in the current Python environment. No AssetRipper or
AssetStudio command was found on PATH; the bounded filename inventory of the
canonical Woden tree, local dotnet tools and `E:/Tools`, including ignored
files, found only `AssetRipper.Primitives.dll` and `AssetRipper.CIL.dll` matches,
not an asset-reader executable or AssetsTools/UnityPy/AssetStudio match.
This is a local search boundary,
not proof that no suitable tooling exists anywhere on the machine.

REA `binary_session` reported no open target. Its available Windows Ghidra
profile explicitly excludes DLL targets and creates temporary analysis files;
no target was opened, no analysis database created and no setup attempted.
No available Unity serialized-object decoder was established by these checks.

### Virtual consumers: identities without a widget association

Existing disassembly supplies concrete unresolved call sites, not a hook:

* `EnhancedButton.Update` at `18057EA40`: indirect calls at `18057EBB2`,
  `18057ECB2`, `18057ED56` and `18057EDE6` use receiver-class slot `2A8`.
  The first site's receiver comes from the array at `this+70`; its argument
  is a four-float value assembled on the stack. This agrees with the retained
  selection-color evidence and does not supply a key-to-Text/Image link.
* At `18057F1EB`, the same Update dispatches slot `398` on the object loaded
  from `this+38` (`SelectWithButtonTarget` in the retained declarations).
  Its concrete receiver class and destination are not resolved in the export.
  Selection dispatch is not evidence that this call refreshes a glyph.
* The retained GamePadSystem export contains indirect sites `180BFE2AC`
  (slot `308`) and `180C006A7` / `180C006BB` / `180C006CE`
  (slots `228` / `1D8` / `1B8`). The export does not provide concrete
  receiver-vtable targets tying these sites to `ButtonNames`, a Text setter
  or an Image sprite assignment. They remain unresolved, not absent consumers.

No new metadata/direct-call survey or baseline verification was run. Field
names, slot numbers, the existing color arrays and GamePadListener rebinding
fields are explicitly insufficient. No supported scene/object path, serialized
fileID/pathID pair, component reference, key-to-widget association or actual
prompt refresh producer was established. Consequently there is no demonstrated
native method suitable for a presentation patch.

### Exact missing input and stopping boundary

The missing input is a **decoded, supported-build serialized reference slice**
for one actual stock prompt: source asset hash and object pathID, hierarchy path,
MonoScript/component identity, EnhancedButton key (or ButtonNames index), and
resolved Text/Image component PPtr including external-file references. It must
be accompanied by the concrete receiver class plus virtual/interface target
identity at the refresh call, and the data path from that key to text/sprite.
The existing raw assets and code exports do not supply that resolved slice.

The smallest later offline observation is one such prompt's reference chain
and its writer, obtained using an existing compatible decoder/export supplied
to source coordination. If static resolution cannot supply it, a separately
authorized bounded runtime observation must identify that same single widget,
its key and actual writer across one stock refresh/input-mode transition;
this attempt grants no runtime authority. Repeating field/direct-call surveys
or baseline checks is not the next step.

Restoration cannot yet be implemented or qualified: the real widget and writer
are unknown. A later hook must snapshot that object's current stock text/sprite
and affected presentation state, preserve subsequent stock refreshes, and
restore on input handoff, loss/replacement, disable and teardown through the
real producer, as required below. This attempt mutated no asset, widget, input,
profile or owner state, so there is no runtime state to restore. Only this
document changes; no proprietary/generated asset is added to Git. No download,
setup, game, attachment, UI, device, build or test was performed. This finite
negative result closes this prerequisite, not the blocked parent implementation
or any source-fixture, rendered, runtime or owner-acceptance gate.

## Continuation 01M5J1XPBADV9SW6C877XCRT3C (lale)

Started from clean `8a7d37e8ab30279be748c95844291620b346d23e`.
Current ancestry checks for `15bc663389564d8ab8c22cb5054e473ba47f5eca`
and `7b6808d` both exited zero. The existing merge and camera/clutch work
were preserved. The supported GameAssembly hash below was verified again.

Additional static metadata narrows the unresolved target:

* EnhancedButton's cached Text array at 60 and Image array at 70 pair with
  Color arrays at 68 and 78. Update's retained assembly uses them for selection
  color updates; they are not established per-key prompt objects.
* GamePadListener has RebindSystem, ActionName, Waiting, ControllerIndex,
  ActionIndex and separate Actiontext/Padtext/Keytext fields at 60/68/70.
  These are candidates for rebinding presentation, not evidence that an
  EnhancedButton prompt uses them. Its action enum is a separate type.
* ButtonContainer contains spacing, row count, child transforms and offset;
  TextBg contains Text, background RectTransform and padding; UiFunctions
  contains sub-images/texts, transparency and blink/fade state. None of these
  declarations supplies a demonstrated association with Gamepadkeys.
* A bounded direct-call scan of EnhancedButton, GamePadSystem,
  GamePadListener, TextBg, ButtonContainer, UiFunctions, MainUiScript and
  UI_Menu found no direct calls to the metadata Text setter at 182851060.
  This scan does **not** resolve virtual calls or serialized scene references
  and cannot establish that these types never update text.

Commands: `dotnet run --project artifacts/wheel-prompts/Audit.csproj -c Release --no-restore`
was run for each metadata/direct-call survey, with the existing LibCpp2IL/Iced
references and no dependency installation/network. All three exited zero.
Ignored proprietary/generated evidence stays outside Git:

| Result | SHA-256 |
| --- | --- |
| artifacts/wheel-prompts/prompt-types-lale.txt | 31766A8B4F9154D32966639426B26CF79EDEF6CA6E8E5026F2F675EEDFF3B2B7 |
| artifacts/wheel-prompts/prompt-widgets-lale.txt | 09B18A3899827E11D326D6EE01315265F718E9EB456A0F11E6899EA43F71E03C |
| artifacts/wheel-prompts/text-calls-lale.txt | FFD3B5A3F89E9004136AB3D962FCFE1DB216FDF96186E5165672D8745F84BD3F |

The missing prerequisite is a supported-build scene/widget association and
its actual text/glyph producer, obtained through static serialized-asset
evidence or a separately authorized bounded runtime survey. This task does
not authorize that runtime. Do not reoffer implementation from the same field
names alone. No production hook or test-only substitute was added; all feature
and production-linked fixture requirements remain incomplete. The candidate
for this continuation is the documentation commit reported by full hash to Fleet.

Required verification was rerun on the unchanged product source. Initial
`pwsh -NoProfile -File artifacts/wheel-prompts/Verify-Lale.ps1` failed before
lease acquisition because pwsh is not on this session's PATH. The existing
installed executable was then used without installation:

`& 'C:/Program Files/WindowsApps/Microsoft.PowerShell_7.6.7.0_x64__8wekyb3d8bbwe/pwsh.exe' -NoProfile -File artifacts/wheel-prompts/Verify-Lale.ps1`

The retained runner acquires the existing shared lease as lale, invokes
`tools/game/Verify-Source.ps1 -NuGetConfig artifacts/wheel-prompts/NuGet.Config`
with cleared package sources, and releases the exact lease in finally. Exit
was zero, with `Lease released: True`. Log
`artifacts/wheel-prompts/verify-source-lale.log` SHA-256 is
`7B7B030AD550AB0150895956060DE51CFC4B2486C7759EF0D2BA3306301583EA`.
Wheel build: zero warnings/errors; 94 suites / 51,700 assertions; UI 6,527
assertions plus 95 production Shim/lease checks; stage analysis 10,757;
triples 22; force-envelope 10,836. These remain baseline results, not feature
coverage or rendered/owner acceptance. No runtime suite was repeated.


GLY is not implemented or qualified by this checkpoint. No prompt hook or
parallel test implementation has been added. The October 7 roadmap entry was
an interop survey, not proof that its two suggested fields draw the prompts.

## Baseline and retained work

This isolated checkout started clean at
`11a45347e35be759c4ddf2492fd02b62ca9646c4`. Its existing merge
`3bfc210cb0f38656b5a3fddbb68531abcbb431ad` already contains both safety baseline
`15bc663389564d8ab8c22cb5054e473ba47f5eca` and camera history
`2d1c8cf` / `7b6808d`. Both ancestry checks returned zero. The clutch contract
in `11a4534` is retained. No reset, canonical checkout edit, installation or
publication accompanied this investigation.

## What the retained evidence establishes

The supported GameAssembly SHA-256 was read back as
`F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C`.
The previous attempt's ignored `artifacts/wheel-prompts` directory contains
interop declarations, metadata field offsets and native disassembly. These
proprietary/generated artifacts remain outside Git.

* `EnhancedButton.Gamepadkeys` declares A, B, X, Y, LB, RB, LT, RT, Back,
  Start, in that order. `ControllerButtonStr` is the enum at native offset
  D4; `ControllerButtons` is a string array at D8; `MyControls` is at C8.
* `EnhancedButton.Update`, native address `18057EA40`, reads the D4 selector
  and the corresponding `MenuControls` fields. For example A reads ButtonA
  at offset 42, B reads ButtonB at 44 and Start reads ButtonStart at 46.
  This is native button selection/activation, not an established text draw.
* In the retained `EnhancedButton.all.asm` survey the sole direct D8-offset
  occurrence is `18057EABF`, a byte read through the separate object at
  offset 80. It is not a read of this button's ControllerButtons array.
  This bounded search does not prove absence of every possible indirect or
  external consumer. It does mean that rewriting ControllerButtons and
  calling it a demonstrated prompt implementation would be unsupported.
* `GamePadSystem.LastInput` exposes Gamepad, Keyboard and Wheel. Its native
  observation is not proof that this mod's independent wheel reader was the
  last input. Presentation must not assign LastInput to manufacture activity.
* `GamePadSystem.ButtonNames` exists at offset 40. A connection from that
  array to an actual rendered label has not been established.

The existing production dispatchers establish the following menu semantics:

| Native menu field / key | Existing dispatched wheel action | Shared profile ID |
| --- | --- | --- |
| ButtonA / A | Confirm | confirm |
| ButtonB / B | Back | back |
| ButtonStart / Start | Pause | start |
| DPad up/down/left/right | Menu up/down/left/right | navUp/navDown/navLeft/navRight |

`MenuNavigation` uses Unity submit/cancel/move handlers; `CustomMenuLease`
borrows ButtonA, ButtonB and DPad fields for verified native consumers.
`StartupWheelControls` admits Confirm or Pause at startup. The DPad keys are
not members of EnhancedButton.Gamepadkeys. Do not infer mappings for X/Y,
shoulders/triggers or Back from driving action indices. `InputLease` indexes
are driving actions, not Gamepadkeys ordinals.

## Required continuation

Identify the actual displayed prompt Text/Image object and its producer on
the supported build. A bounded scene/widget survey, or equivalent static
asset/consumer evidence, must establish how that object relates to the
EnhancedButton key and how it refreshes. The existing artifacts do not yet
establish that connection. Runtime is outside this source task's authorization.

Once the target is known, connect the real presentation source to the existing
UI executable fixture project. Native labels must resolve WheelInput.Bindings,
which changes only after successful TryCommit. Shim labels must resolve
ControlProfileDraft.Active through SharedBindingHost, never Binding(), Saved,
row Device (which can describe a draft) or a newly read on-disk profile.
Reuse the current button/hat numbering and device names, require current
accepted availability, and observe activity without calling edge consumers,
polling or opening readers. Unknown keys and non-live bindings retain stock.
Cover distinct devices, high indexes, eight-way hats, drafts, saved restart
edits, switching input, lost readers, replaced objects and exact restoration
through the real hook, including native callback failure and disable/teardown.

## Checks and limitations

The first required command,
`powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/game/Verify-Source.ps1`,
exited 1 because the isolated cached loader copy of 0Harmony.dll was missing.
The coordinator subsequently supplied the existing matching loader cache;
no dependency initialization, download or installation was performed here.

The initial inline PS7 lease command lost Purpose argument quoting, briefly
entered verification without a lease and was interrupted. Its partial result
is not a passing verification receipt. The corrected file-based runner set
ErrorActionPreference=Stop, acquired Enter-StageRigLease atomically, invoked
`tools/game/Verify-Source.ps1 -NuGetConfig artifacts/wheel-prompts/NuGet.Config`
with cleared package sources, and released the exact lease in finally.
It exited zero and printed `Lease released: True`.

Full log: `artifacts/wheel-prompts/verify-source-leased.log`, SHA-256
`DEF473D6646AD8BFD2BADFC231A49EBBD763002DAF86A685F08BC845F84403F8`.
Results: wheel build zero warnings/errors; 94 suites / 51,700 assertions;
UI 6,527 assertions plus 95 production Shim/lease checks; stage analysis
10,757 checks; triples 22 offline assertions; force-envelope 10,836 checks.
These checks validate the retained baseline, not a wheel-prompt feature.
No new feature fixture exists yet. No game, input, device, display, force,
installation or owner acceptance was exercised.

## Runtime checklist after a source candidate exists

1. Pin the candidate and package; arrange an authorized muted, save-safe
   attended run under the existing shared lease and restoration recipe.
2. Identify each native prompt on title/notice, car and transmission choice,
   stage presentation, main menus and pause/results. Unsupported prompts stay
   stock. Record the actual key, dispatched action and active binding identity.
3. Check Native and active Shim separately: ordinary button, high-index button,
   eight-way hat and a second device with the same button number. Verify readable
   device/assignment text and no clipping at ordinary and triple layouts.
4. Switch wheel to keyboard/controller and back. Rebind Native successfully;
   cancel/fail a draft; edit/save Shim for next start. Only active assignments
   appear, and keyboard/controller restores the exact current stock prompt.
5. Disconnect/refuse/pending input, unbind an action, change scene/recreate the
   object, and disable the plugin. Verify stock text/glyph, visibility and
   selection behavior restore exactly, with no duplicate labels or consumed
   input edges. Close normally and verify owner-state restoration.

Source, executable fixtures, actual menu rendering/layout, physical input
switching and owner acceptance remain separate gates. Existing installed-state
headers and unchecked/pending unrelated standards are unchanged.
