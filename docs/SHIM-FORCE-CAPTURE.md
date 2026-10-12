# Bounded original forward-force capture — prepared, not run

Production analysis source `1bff212bbc72c1058d776100790f221016ee20a4` passed
parent Core review and independent wiring/export review. The unchanged
production plugin and Core were built clean at that exact revision and frozen
under `artifacts/force-analysis-forward-01/production`; the separate addon
recipe below does not change production sources, owner gains, HUD or physics.

The schema 5 six-action workload and both original trial recordings remain
unchanged. Its 700 ms pulses had no qualified Driving reference among the
Grip6-admissible rows, so another identical run is not a useful reference test.

## Explicit diagnostic scope

Schema **6**, `shim-force-capture` / `-ShimForceCapture`, uses the same nonce,
exact profile/device admission, restored native Kenya SS1/car8/player0/Manual
startup, output/progression/cloud mute and original DiagnosticLaunch producer.
It requires native gear **1**, completed countdown, racing/unlocked state and
the actual foreground/Unity focus checks. No focus or input API moves the OS
cursor/keyboard. No body/pose writes, replay or saved-tune changes occur.

Only this synthetic recipe is permitted, with at most **four batches**:

1. 700 ms native-neutral baseline.
2. **8000 ms 50% throttle** using the independently retained raw sample
   `axis 2 / value 32768 / range 0..65535`. All other five raw controls remain
   neutral, including the accepted brake rest **1743**. Native gear stays 1.
3. 700 ms native-neutral final plateau, then Core `end` / Completed.

There is no brake pulse (which can invoke native reverse), no shift and no
additional steering. The accepted workload has only ±0.5 steering samples;
those are not presented as a gentle variation. Long throttle must be observed
through native Controls and MainCar for at least **7.6 seconds**, with no
observation gap above 100 ms. The long batch has an 11-second timeout; original
700 ms batches retain their eight-second bound. Startup retains the existing
95-second timeout and the supervisor uses 180 seconds.

A passive callback after production `ObserveForceAnalysis` reads the same source
sample before publication. Success additionally requires **at least two cumulative
seconds** of measured forward motion at **15 km/h or higher**, both front contacts
grounded with positive finite loads, and **positive valid-analysis row count with
the production Driving reference**. A provisional reference, requested throttle,
accepted batch or synthetic preview cannot satisfy it. The callback requires
Grip6/analysis contract1, diagnostic gate1 and zero physical attempts/connections/
sent force. Final source/hash/footer and offline export verification still follow.

Zero analysis magnitude on a straight route remains honest. This first run
qualifies capture plumbing/reference coverage, not a representative normalization
workload, measured torque or physical FFB coexistence. Loss of natural focus,
owner input, respawn, missing source observations or recorder errors refuse;
there is no background-driving exception.

## Candidate and intended command

Frozen production product version: `0.2.14+1bff212bbc72c1058d776100790f221016ee20a4`.

| Artifact | SHA-256 |
|---|---|
| WodenRallyEdgeWheel.dll | `75fb404b3a58f7f1a8ce126b2870801652fc7922a3f1b3516ae6acb6c9a9c738` |
| WodenRallyEdge.Core.dll | `89fcf5eb08c212bc82f048f928e749b74623d6d0960e3635412451e64f84ce07` |
| dbce_shim.dll, existing provisional ABI2 | `9b8a036f664cf8f45cccf82b98e5e4376f93215b60eac50b6db965bf498015d8` |
| Existing verified no-force WheelFfb.dll | `24a9b624e3f3c474383c64a0177019bfb0837de70b311854c59ee692d7db03f5` |
| Accepted Controls bytes | `5206edfbd1b400e5d6a41d91ae836485bbe27d63992ad6f7483b3babb0e72b79` |

The private bundle manifest records the separate addon hash and source revision.
Before a coordinated GO, compare every bundle pin and the current runner/addon
against that manifest. The existing runner performs the same hash, idle, exact
lease, owner-tree/raw-registry backup, temporary scope and restoration checks.
Do not launch this while another game/lease owns the rig.

```powershell
pwsh -NoProfile -File tools/controls/Run-Controls.ps1 `
  -Result results/shim-force-capture-01 -ShimForceCapture -Seconds 180 `
  -CandidateDirectory artifacts/force-analysis-forward-01/production `
  -ControlsSource artifacts/force-analysis-forward-01/inputs/dbce-controls.ini `
  -ControlsSha256 5206EDFBD1B400E5D6A41D91AE836485BBE27D63992AD6F7483B3BABB0E72B79 `
  -NativeCandidate artifacts/force-analysis-forward-01/native/WheelFfb.dll `
  -NativeSha256 24A9B624E3F3C474383C64A0177019BFB0837DE70B311854C59EE692D7DB03F5
```

After normal exit, retain the original recorder and trace, run `reprocess` then
`ffb-analysis-export` on the archived new case with its recorded strength/ratio,
and inspect valid/eligible/nonzero counts separately. Verify zero output/network
and exact owner file/preferences/receipt readback before releasing the lease.
Never overwrite trial01, trial02 or the sealed Kenya reference.

## Passive HUD evidence

The existing native `shot` command is intentionally refused by latched trial
modes, and StageVisualCapture is replay-only. Use the already reviewed WGC helper
at `E:/Source/toolkits/.worktrees/window-capture-burst/tools/window-capture/bin/Release/net8.0-windows10.0.22621.0/dbce-window-capture.exe`.
Immediately before capture, require its `--list` process/start time to equal the
runner's owned `context.json` and select the visible Unity HWND. On the forward
batch, capture only that HWND into a fresh folder:

```powershell
# Values come from verified context/list readback; never substitute a foreground window.
& $capture $ownedPid $freshHudFolder --hwnd $ownedUnityHwnd --duration 5 --max-frames 75
```

Keep native resolution for HUD pixel review. The helper checks PID, start time,
window class/liveness before saves, never captures the desktop and never moves
focus. Its frame timestamps and skipped counts bound the evidence. This is
sampled HUD motion only, not continuous flicker/performance qualification. Opening
F6 would change the driving gate and is outside this run. Owner-reported HUD jitter
remains unproven; no HUD fix is included.

## Offline review checks

New actual pinned Core recipe: **1,260 frames / 5,088 raw Live/Injected checks**;
actual production consumer fixture: **8,747 assertions** including **7,933 shim/
lease checks**. Native engine behavior remains simulated in that fixture.
Addon lifecycle: **362 assertions**, including long native plateau, missing
reference refusal, final neutral/Core completion and honest zero preview.
Protocol/workload/coverage: **112**, configuration: **13**, production recovery:
**95**. Compiled production/addon hook seams: **48 + 40**. Addon build has zero
warnings/errors. Original six-action regression also passes unchanged: **2,640
Core frames / 5,166 raw checks**, **15,857** production UI assertions (including
15,043 shim/lease checks), and the same **362** addon lifecycle assertions.

Logs: `artifacts/force-forward-{core-consumer,legacy-regression,recovery,hooks,addon-build}.log`.
The shared Core pin remains provisional `f49cfc559f065b778967a7c94068a75df538ec69`;
this slice does not promote a toolkit pin. No live run or install is claimed.
