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

Source syntax/diff checks only for this checkpoint. No compilation, semantic
binding, fixture execution, rendered UI, install or runtime result is claimed.
The owner later returned the rig; execution still needs the coordinator's
assigned shared lease/build slot. Owner recovery via the old mouse action was
not tested because Woden had already closed.
