# TripleView physical-camera FOV preservation — isolated successor

Local integration readiness (PM457638): this candidate retains the reviewed
56e8ec0 production and fixture blobs, on accepted53af231 with only the seven
explicit integration paths. Gripv8/default migration is unchanged. No new build,
merge, push or installation accompanies this integration commit.

The existing installed build is0.2.16+56e8ec0:23 payloads match receipt
`8A13916788F519D02D226A5C493B789C28FE32F60E5DA56BE162D801F22FA6ED`;
plugin SHA256 `E0C34978C958157F95D485F222A6168C232C0E77EBEF48E82A7456CD0CD33D78`.
Its private561-member ZIP is `46A6F04BE6A0446C9933E726E301315329BFE6A9179D19C4BFDDC9F639447425`.
The retained full product/package pass includes101 suites/52,337 assertions,
6,919 UI and3,897 input checks; the actual-method fixture below is11/11.
The supported-provider activation prerequisite failed twice before gameplay;
native/rendered first-enable FOV, Photo/replacement/Stop remain open. Saved-view
background replay cannot substitute for the missing paired lens/flag/projection
observations. No force retune or duplicate FOV correction is warranted.

The later04ca696 `STATE.md` update is an eighth path and is deliberately excluded
from this bounded integration. Current closeout remains in the hub handoff
`docs/handoffs/2026-10-24/super-woden-rally-edge.md` and evidence
`artifacts/woden-fov-dume-20261027/{fixture-01,product-01,runtime-01}`.
The source-only/fixture checkpoint below remains historical and is not relabeled.

PM425695. Main/installed baseline is `53af231031a7e93fd78e1eff9d0e31380065b801`.
Held patch `eaea8fb1f0dcb95cc8f2fdb7110b10b901c52a8a` already has an equivalent
rebase `d6488ddeab9a6e70b8e23f8c5c5658cd685a2cc0` on that baseline. This isolated
branch reuses d6488dd unchanged rather than rewriting the production patch.
Existing Egon shutdown worktree08c5325 is untouched. No public push.

The actual path is Runtime's camera postfix -> `AfterCarCamera` -> `Apply`.
Camera replacement first calls `Stop` for the prior camera, then applies the new
one. Apply captures the game's fieldOfView before enabling physical properties,
only enables when currently non-physical, and writes that captured angle back
immediately to synchronize the physical lens. Views and logging use the captured
angle. Repeat calls respect later game/lens writers; an already-physical camera
gets no unnecessary property writes. Stop restores the original projection/rect/
physical flag, without restoring a stale captured game FOV over a later writer.
Force/controls/panel-close/window-landing/default migration code is unchanged.

## Focused deterministic result

`tests/WodenRallyEdge.TripleFovTests` uses only .NET10 and fake cameras. Its
preparer copies the **unchanged actual AfterCarCamera, Apply and Stop bodies**
from an explicit source file, with no body substitutions, into a partial fixture
class. The camera fake models the documented non-physical -> physical FOV change
from a stale45-degree focal lens, then synchronizes that lens on fieldOfView set.
Other rendering/config/canvas collaborators are inert stubs. This is not a Unity
implementation, native setter validation, geometry or physical display test.

Existing UiTests substitutes TripleView entirely, so its FOV/UI checks cannot
exercise this handler. No production abstraction was introduced just for testing.

- Baseline53af231: **11 checks,7 failures**, expected regression witness.
- Successor d6488dd: **11 checks,0 failures**. First activation, unchanged Photo
  reapply, later native FOV writer, camera replacement, Stop, already-physical
  camera and original physical-mode restoration are covered.
- Both fixture compilations: zero warnings/errors, warning-as-error enabled.
  Existing SDK10.0.401, local framework references only; NuGet sources cleared.
- Run09:05:24–09:05:36Z; owned helper safe/quiescent and independent census
  confirms all14 original identities absent. No rig lease used for this small
  device-free compile/check; Revo's live custody was untouched.

Hub `artifacts/woden-fov-dume-20261027/fixture-01/` retains exact commands,
baseline/successor extracted methods, preparation/source hashes, both build/run
logs, process receipt/census and read-only installed identity. Negative-control
exit0 means the expected old-source assertion failures were observed; it does
not mean the baseline behavior passed. Generated/compiled fixtures remain local.

For another fresh fixture directory, run the preparer with `--source` naming
TripleView.cs and `--output` naming a new methods.cs, then build this test project
with `-p:TripleMethods=<absolute methods.cs>`. The retained Run.ps1 supplies the
exact isolated offline restore/build/run arguments for both source versions.

Installed read-only receipt still reports `0.2.16+53af231031a7e93fd78e1eff9d0e31380065b801`;
receipt SHA256 `D824A6E5E010C314F3DB4ED54881AF1C4CE1496355B2E420F27734D1F58694BD`,
plugin SHA256 `DD9E23B18DC1C19E426368FBB4955A78EC5D3EC6D649B8C0A2DC1F6E05564D7B`.
No install or owner-state write occurred.

## Remaining gates

Full `Verify-Source.ps1`, ordinary Core/UiTests and a real plugin build were not
run here: this new isolated checkout lacks ignored local BepInEx/interop pins
(`lib/core/BepInEx.Core.dll`, `lib/local-dependencies.json`). Do not initialize
moving game dependencies or infer product compilation from the BCL-only fixture.
The focused fixture is the newly established evidence level.

After source review and a separately assigned qualified build/package, a named
leased run must observe actual Unity FOV before/after activation, Photo/chase/
mounted camera changes and turning triples off, with the game's later lens
writes preserved and original camera flags/projection restored. Rendered FOV,
three-monitor images, HUD, physical scanout and owner acceptance remain open.
No game, device/FFB, display, config, dependency install or public remote action.
