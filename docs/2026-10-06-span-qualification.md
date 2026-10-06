# Separate-monitor span candidate — 2026-10-06

Installed `b9e9929c40d79b2884189e7bff7c410e1d55982a`, candidate
`0.2.14-session-span.1`: countdown leaderboard guard `884a106`, span `b043bbc`,
offline exporter `fbcf686`, restored replay span override and passive game-frame
capture. Span remains **default off pending runtime qualification**.

## Package and installation

- Source: 42 suites / 37,760 core assertions, 630 UI assertions, 22 geometry checks,
  10,836 force-envelope checks, zero build warnings/errors. Seven temporary
  span-config cases pass without game/device access.
- Exact player stage: all 111 Windows PowerShell 5.1 installer checks passed.
- ZIP: `components/wheel/dist/WodenRallyEdge-0.2.14-session-span.1.zip`.
- SHA-256: `971692DCEF10798F9D81532163428B0186FFBFEECCE4FEE35E6419D645242D11`.
- Installed `05:45:56Z`, game closed, under the rig lease. All 13 payloads verified;
  18 protected files retained exact hashes. The previous `05837ee` runtime matched
  its receipt before replacement and was backed up by the installer.
- Evidence: `results/install-session-span.1-20261006-004552`.

The owner's current saved FFB setting is **On**, strength 49.583332%, peak 25%.
No tune was changed. Replay must mute physical wheel/network output independently
of these settings. Neither physical force nor leaderboard submission was tested.

## First attempt: Steam contention, no game process

Request `229a2bed771e4b789a1e1f55b7d98e44` backed up owner state and temporarily
enabled only `Triple.SpanSeparateMonitors`. Steam had closed at 00:40:42 CT; the
launch URL restarted it at 00:46:03. Steam then waited at `KickingOtherSession`
for permission to interrupt another session. One URL retry did not advance it.
No confirmation was accepted, no Woden process started and its BepInEx log did
not change. This is **not a Woden span/replay failure**.

The runner restored owner state at `05:52:05Z`, removed its request and released
its lease. A further check verified all 13 payloads and the original config hash.
The local Steam client started by this attempt was gracefully closed under a
short cleanup lease at `05:52:56Z`, clearing the pending launch. The pre-existing
Wallpaper Engine process remained running; no remote-session interruption was
authorized or performed.

Evidence: `results/woden-kenya-span1-20261006-0046`. Its copied game log is the
**old October 5 log**, not a current result; require matching request identity.
An independent hash-checked copy of 137 install/attempt files plus validation
logs is under `%LOCALAPPDATA%/Dbce/StagePlayback/SessionEvidence/woden-span-preparation-20261006`.

## Deferred retry and remaining acceptance

One retry is queued for **02:05 CT**, honoring the owner's after-02:00 instruction.
It waits until 04:00 for five owner-idle minutes, a free lease and no local game.
It refuses changed runner scripts, installed candidate/payloads, game hash or an
unrelated request. It never accepts Steam interstitials. Private queue script:
`results/Queue-SpanReplay.ps1`; status/logs: `results/woden-span-queue-20261006/`.
Create `stop.txt` there to cancel before launch. A pass records
`awaiting-visual-review` and does not automatically promote a default.

```powershell
./tools/game/Run-StageSession.ps1 -Recording "$env:LOCALAPPDATA/Dbce/StagePlayback/references/woden-kenya-20261005" -SpanSeparateMonitors -Result <new-directory>
```

Qualification requires all 3,601 poses, the expected 7680×1440 span at (-2560,0),
triples on, inspected frames, zero physical/network delivery, normal exit and
exact restoration. Do not change display profiles or force focus. A focused
pass leaves the separate background-acquisition check open. After qualification,
enable the new-config span default, preserve explicit saved opt-outs, and package
and install the final candidate with its own receipt. Build/installer checks alone
do not establish plain-launch span or public-release readiness.
