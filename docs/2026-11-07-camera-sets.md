# Camera sets (Game/Realistic) checked on the rig

2026-11-07, lane-a. Triples on the three 2560x1440 screens, force feedback off, Kenya SS1 from Arcade.

| Check | Result |
|---|---|
| F7 on the bonnet | Switched Game to Realistic and wrote only `BonnetRealistic = true` |
| Numpad + three times | Narrower view, label gained "(Custom)", saved `BonnetRealisticOffsetDegrees = -6` |
| Shift+F7 | Stepped original / angle / angle + curve for the session; the config file was unchanged |
| F7 on the bumper | Wrote only `BumperRealistic = false`; the bonnet's choice stayed |
| Restart | The stage opened on the saved Bumper camera in its Game set; the bonnet came back Realistic (Custom), 27.6° center lens (33.6° seat-true minus 6°) |
| F6 > Cameras | "Bonnet Camera - Realistic FOV (Custom). F7 switches this camera's saved set." and the "Use Game set for this camera (F7)" button |
| Picture | Every capture passed `Test-GamePicture`, including the final one with F6 closed |

Fixed in this run: the camera name was drawn twice at the bottom of the center screen (the panel's camera box under
the name notice), so the text was garbled. Only the name notice draws now, and the panel's status line still reports it while F6 is open.

Seen, not changed: a plain launch rewrites `dbce.wodenrallyedgewheel.cfg` through BepInEx (new descriptions and order;
values unchanged). Pass `WodenUserState.ps1` an absolute backup path; a relative one makes `-Restore` refuse.

Captures and logs: `E:\Source\temp\woden-camsets\s\run-s1`, `run-s2` (scripts beside them).
