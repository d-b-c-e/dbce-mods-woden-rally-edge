# Separate-monitor span candidate — 2026-10-06

**Current:** the 02:05 replay passed, including naturally unfocused acquisition;
Claude inspected the frames and installed `8678f06/session-span.2` with the new
span default on. Explicit opt-outs remain intact. Plain launch and Surround
return remain open. Exact promotion and independent verification follow below.

Earlier installed `b9e9929c40d79b2884189e7bff7c410e1d55982a`, candidate
`0.2.14-session-span.1`: countdown leaderboard guard `884a106`, span `b043bbc`,
offline exporter `fbcf686`, restored replay span override and passive game-frame
capture. That candidate kept span default off pending the qualification below.

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

## Default promotion and install — 2026-10-06 02:10 CT (Claude)

The 02:05 queued replay (`results/woden-kenya-span-after2-20261006-020506`) passed: 3,601 poses,
max 6.1e-05 m / 1.7e-05 deg, `Span window: spanning 7680x1440 at (-2560,0)`, five passive frames at
7680x1440 windowed with triples on, owner state restored. Claude inspected frames 01 and 03: bonnet view
continuous across both seams, HUD centred, side views angle-correct. The game re-runs
`FirstResolutionSet` on scene loads; each run is substituted, so no apply is added.

`8678f06` makes `[Triple] SpanSeparateMonitors` default true; an explicit saved false is kept (the owner's
installed config had no such key). Candidate `0.2.14-session-span.2`: 42 suites / 37,760 assertions,
630 UI assertions, all 111 Windows PowerShell 5.1 exact-package installer checks; installed 02:10 CT under
the rig lease (backup `WodenWheelBackups/before-install-20261006-021053-31870fbf`; log in
`results/install-session-span.2-*`). Not yet seen: a plain launch with the new default (expect the same
span log line) and the Surround round trip (Target() returns null under Surround, so nothing changes there).

## Independent readback and background qualification

Codex checked request `27fb67943faa446caccd583242a78823` against the actual run
log. It acquired with `focused=False; backgroundAuthorized=True`, then released
with **unfocusedReplaySteps=3601**. Force writes and Forza sends stayed zero.
Normal exit and restoration completed at `07:07:03.3575464Z`. This qualifies
naturally unfocused acquisition and the full Kenya reference on session-span.1;
focus transitions and the broader interruption/device matrix remain untested.

Frames 01 and 03 were independently inspected and show the drive progressing,
bonnet view across the three rendered views and the HUD confined to the centre.
Both frame sidecars report 7680x1440, Windowed, triples On and focused false.
Original-signal review remains exact: 3,601 samples, 3,553 valid force samples,
3,477 nonzero and zero force-reprocess error or physical-delivery attempts.

At `07:14:53Z`, the promoted session-span.2 installation matched all **13** owned
payload hashes. All **60** config/save/controller files in the replay's original
owner manifest and all **25** raw registry values (including exact names/types/
bytes) still matched. These are read-only checks; no new game run or display
change was made. Installed receipt source:
`8678f06fa967e0985d963315b72ef879c79e3235`; installed plugin SHA-256:
`224B51ECC0B7E589D2EB51DA9882C6A5637231FD22A3CA21293097EB04A75EDC`.
Player ZIP SHA-256:
`53D3115372CEFF76F0969B12250A31B91AB864DF36EEE6D637ECF617ED8D007A`.
The package-preparation JSON's `installed:false` is its build-time state; the
subsequent game receipt and installer log identify the installed candidate.

An independent private archive retains 155 files: the entire replay/queue result,
five images, original/after-run owner snapshots, promotion install log/receipt,
player package and source archive. Location:
`%LOCALAPPDATA%/Dbce/StagePlayback/SessionEvidence/woden-span-pass-promotion-20261006`.
Evidence manifest SHA-256:
`22CAEFAF0AE1100F94C692544F9CB19659A2F763F738917845AF72049C30F7C0`.
Do not publish its owner backups. The former deferred worker has completed;
`awaiting-visual-review` is its terminal handoff state, now reviewed above.

**Plain launch with the new default, 2026-10-06 03:24 CT (Claude):** Steam launch (no arguments) of installed session-span.2 on "Sim Racing" logged `Span window: spanning 7680x1440 at (-2560,0)`; the frame is 7680x1440; the config now carries `SpanSeparateMonitors = true` (written as the default); normal close. Surround return not yet checked.
