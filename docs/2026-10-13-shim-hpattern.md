# Shared-input H-pattern parity: source candidate

Current install pointer (2026-10-13): private candidate source `d8df97b` is installed under receipt `F645FE98`; saved Backend remains Native. The source-only/no-install wording below describes the earlier H-pattern checkpoint. No new live H-pattern or ordinary Shim FFB coexistence qualification accompanied the HUD-stable install. Exact current evidence is in `components/wheel/docs/STATE.md` and the hub handoff.

October 13, private candidate on top of `cdd8b0a`. No install or game run.

The shared HPattern profile now delivers gates 1-6, reverse and explicit neutral
through the existing `HPatternShifter`. The game's native gearbox must be Manual.
Its stock shift actions still perform each change; no gearbox or physics code is
replaced. Woden has no neutral or reverse gear: neutral cuts wheel throttle, and
reverse selects first gear and swaps wheel pedals, as the existing native input
backend does. Clutch, gear 7/8 and Select remain unsupported.

Pedal emulation happens before native keyboard composition. Keyboard throttle,
brake and shift actions retain their native behavior. Automatic mode neither
swaps nor cuts pedals. Once Manual is selected, a held stick position is used
without requiring release. Gates also remain positions across pause, F6, player
spawn and focus return, while ordinary action buttons retain their context and
release rules. Core device freshness/rearm still determines whether a position
is live; no wheel input is delivered while unfocused. Conflicting gates resolve
to neutral. A terminal shim failure cannot fall back to legacy bindings. An
unavailable shifter is reported separately from a live stick in neutral.

Validation: the actual Unity/IL2CPP plugin builds with zero warnings/errors.
The production-linked UI fixture passes 899 assertions, including 85 shim/lease
checks. New coverage exercises the real WheelInput/action-table lease: neutral,
forward shifting, reverse, conflicting gates, explicit neutral, Automatic/Manual
transition, native keyboard coexistence, exact action restoration, focus loss,
and terminal failure. No game, input device, native force open or force output.

Use the explicitly supplied private shim hash documented in
`vendor/dbce-shim/PROVENANCE.md` for developer builds. This source change grants
no new redistribution right and does not add the DLL to tracking or packaging.
Do not merge the experimental branch history containing the old binary into
public main. A clean source landing and optional dependency/build path are still
separate work.

Remaining parity work: Wheelkit's verified Woden shim writer/capability,
standalone editable shared bindings, live H-pattern consumer and device-loss
checks, force-preview qualification with Grip 6, launch-session recording, shared
recording status, and the owner's HUD jitter observation. The installed native
backend and force tune remain unchanged.
