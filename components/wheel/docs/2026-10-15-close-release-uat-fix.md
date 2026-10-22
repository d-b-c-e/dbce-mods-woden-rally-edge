# Woden pending-close recovery, October 15

Source base: `4d439612d49b7d49624fbdf52f2bc70833577647`.
Private worktree: `livo-woden-close-release-20261015`. Owner installation and
current UAT settings are preserved. This change is separate from the earlier
center-camera-hint source candidate and Dine's native-input diagnosis.

## Confirmed source cause and limits

The screenshot's release message and **Keep settings open** action identify a
pending close. `Editing` includes `MenuOwnership.Closing`, disabling ordinary
tabs, view buttons and Close. F6 was explicitly suppressed while closing, and
Escape called Toggle, which rejected Editing. Normal bound menu dispatch also
suppressed Back. The only explicit recovery was the body mouse button; the
pending-close render branch returned without maintaining keyboard focus/count.
Its header incorrectly described pending close as an unfinished edit.

The stock release barrier combines all configured pad actions, stick values,
keys, mouse buttons and bound menu controls. A held/nonneutral source can keep
it waiting, but the retained UAT log did not name the source or record raw
pedal values. No held brake or false close caused by Advanced-to-Simple is
proven. Header button activation consumes the event in source. Global GUI state
is restored, and header/sidebar controls do not use body scrolling.

The no-drive complaint before F6 remains a separate investigation. Dine owns
that path and the preserved final UAT config/log/binding evidence. Do not restore
the old A9D5 config over the owner's newer settings or change pedal calibration.

## Change

- Pending close retains stock masking, owned pause, focus/freshness checks and
  the existing 100 ms neutral interval. Cancellation keeps settings open and
  stock dispatch suppressed; it never bypasses the release barrier or enables
  force output.
- Escape and F6 cancel through InputSystem or the existing IMGUI fallback.
  Bound Settings and Back can cancel despite normal menu dispatch suppression.
  The sidebar offers enabled **Cancel close**, with **Keep settings open** as
  the default focused body action. Focus/count stay current. The header names
  pending close and explains cancellation.
- The waiting message and periodic stock status identify the first observed
  blocker: stock pad/read, button/stick or `PadActions[index]` value, mouse,
  keyboard aggregate or bound menu action. An index is not guessed to be Brake.
  Unknown/stale/unfocused/error reads remain blocked. Nonfinite stick values
  also block rather than count as neutral.

## Validation

New focused synthetic fixture source: `CloseReleaseChecks.cs`, exposed through
`WodenRallyEdge.UiTests --close-release-only`. It exercises named blockers,
default Enter focus, InputSystem/IMGUI cancellation, direct/bound Back, sidebar
mouse recovery, Advanced-to-Simple without closing, and retained neutral timing
and EventSystem ownership. No new device reader or game hook was added.

Source checkpoint `f61da7b38afd95ea0bee5bdc7daacfbbb89ce2e8` passed root review
163681. After owner rig handback and grant 163544 / predecessor closure 163675,
one leased batch passed UI compilation, **33 focused close/release assertions**
and production Plugin/Core compilation, both builds with zero warnings/errors.
No older UI/physics/force baseline was repeated. Four C# files also parsed with
zero syntax errors before execution.

Retained outputs are version `0.2.16+f61da7b38afd95ea0bee5bdc7daacfbbb89ce2e8`:

- Plugin: `F51301F01B52155B0D12DFD1C6D674BD88835817379EEB37BDEFC2D70BE8F7D6`.
- Core: `A9BD59926163F37A62242E208A8822801DF725B5FC4E16D867068087B08D0EAF`.

Hub `artifacts/woden-close-release-uat-20261015/checks-01` holds original logs
and result `F5288D00DE8356B2C4898E134A0151B8BFFA9E07AF980E64A8EFC3C07C786EFE`.
Independent exit `74C494FF5BCA1B8CB4B92CADEACC0BC12EB64D7C901094D84A2C404A5E860FBC`
confirmed all 11 original process identities absent, scope zero, actual shared
lease absent and output hashes exact before analysis. Successful outputs remain
available for packaging without rebuilding.

No game, device/native/force run, owner config write, install or rendered UI
acceptance occurred. Owner recovery via the old mouse action was untested
because Woden had already closed. The separate camera-center hint and shared
Help/report component are not part of these compiled bytes. No-drive diagnosis
and an attended combined runtime observation remain separate gates.
