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
At this source checkpoint, exact-package installer checks, installation and a
post-Apply muted game/input check are pending. No settings or forces were changed.
