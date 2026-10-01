# Lingering cornering force — 0.2.7 evidence

Owner feedback on 2026-09-16: force is much better, but centering seems to persist too long. This is an assessment, not a tune change. No configuration, game binary or physical output was changed for this analysis.

## What the latest run proves

The run (PID 50964) reports 9,315 local ticks, 7,160 wheel-input ticks and **6,723 force writes, zero failed calls**, followed by normal shutdown. The owner felt the improvement. Saved FFB is On, strength 49.583332%, cap 25%, smoothing 35 ms, damping 0.05, slip scale 0.35 and reference load 6000. The owned-window initialization/exit guards succeed. No hardware hold-watchdog releases or effect recreation are logged.

The native log records **value changes**, not every force write. Reconstructing each command until the next change or stop gives a 113.365 s driving-output window, with **44.087 s (38.89%) at the ±25% output cap**. The longest constant cap interval is 4.049 s (23:37:31.374–23:37:35.423); several others exceed two seconds. Silence between identical commands is not evidence of missing updates. These are commanded values, not measured wheel torque.

## Ranked explanations

1. **Saturation masks unloading.** The model frequently requests more force than the configured cap permits. While it stays above that threshold, the delivered command cannot fall, even if the input estimate is declining. This is the strongest observed clue, although the log cannot show when the driver started unwinding or whether each plateau occurred in a sustained turn.
2. **Conditioning slows release/reversal.** The model uses a 35 ms low-pass filter and a symmetric 1.5 normalized units/s rate limit, including when reducing force. An offline experiment using the shipped Core and toolkit DLLs at 60 Hz, current strength, 6,000 total synthetic front load and 20 m/s warmed the model then dropped slip instantly to zero. From capped output it took 150–167 ms to shed 90% of force and 200–217 ms to reach output zero. Instant reversal took about 167 ms to cross zero and 317 ms to reach 90% opposite force. These are synthetic response times, not measured driver-to-wheel latency. Ordinary pre-physics sampling can add timing differences as well.
3. **The source is sideways slip, not a center-position spring.** The model follows contact-weighted tanh(front sidewaysSlip / 0.35). It contains no steering-position spring and no separate high-slip unloading curve. If front slip remains nonzero while the driver unwinds, the alignment estimate can continue requesting force. Steering damping also resists wheel movement; its actual contribution is unknown without synchronized steering data.

The half-second ramp is startup/recovery behavior; it is not a half-second release timer. The 150 ms native watchdog releases on stale updates and is not an intentional delay attached to every force command.

## Limits and next measurement

No gameplay recording exists for this run. The game/native logs establish output and cap occupancy, but not synchronized steering, speed, slip, front load, alignment, damping or saturation before shaping. They cannot prove which explanation coincides with the reported sensation or rule out wheelbase-side filtering.

First tuning targets: reduce prolonged clipping and allow quicker force unloading while preserving peak/ramp/lifecycle limits. A short attended recording of steady turns, gradual unwinds and quick direction changes should compare wheelInput.steer, wheel.fl/fr.sidewaysSlip/contactForce, ffb.alignmentEstimate, ffb.dampingEstimate, ffb.preview and ffb.sent. That separates a lingering source estimate from output-filter delay. Keep the current tune until that comparison or an explicitly chosen A/B test.

Private evidence and analysis: ignored artifacts/owner-0.2.7-force-feel contains logs, settings and command-summary.json. artifacts/force-release-analysis contains the managed-only experiment and results.json; no native device calls run. In the isolated shaper comparison, changing smoothing to 20 ms and slew to 3 shortened the synthetic capped-force release to 117 ms; this is a candidate to investigate, not a validated setting or a deployed change.
