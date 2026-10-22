# Native wheel prompts: incomplete source investigation, October 22

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
