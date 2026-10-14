# Super Woden: ready to test (2026-10-14)

**Installed:** private `0.2.14+f252debc54159d101b344abf0f5631b503b64c16`, receipt `3041965C…` (23/23 payloads). The
saved backend is **Native**, with the owner's force tune unchanged. The optional shared-input shim (`dbce_shim.dll`
`9B8A036F…`) is installed but not opted in. The owner runs Wheelkit 0.6.34.

The automated, software-side checks are done (see [STATE](../components/wheel/docs/STATE.md) and
[TOOLKIT-ADOPTION](../TOOLKIT-ADOPTION.md)). The checks below need a person at the rig. Record each as PASS, FAIL or
NOT TESTED, with what was seen.

## A. As installed (Native)

1. Launch Woden normally. **F6** opens and closes the panel from the keyboard (STD-030). **F8** stops force feedback.
2. Drive with the Native bindings. Steering, pedals, handbrake and shifting respond.
3. **Force feel** compared with art of rally, on the same feel test in both games (STD-031). The tune is not changed
   for this test.
4. **Separate monitors with the dashboard connected:**
   - the game uses only the three racing screens;
   - the HUD sits on the center screen;
   - the HUD no longer shakes. The position fix is in production; only the owner's acceptance is open.
   - The speed digits can flicker by 1 km/h. That is stock game behavior, and the mod leaves it unchanged.

## B. Optional: shared Wheelkit profile (Shim)

5. **Before launch:** turn off every controller Unity would count as a gamepad. If one is connected, the shim refuses
   at startup and the Native bindings stay active. A gamepad plugged in later stops shared input until a restart.
6. **Opt in:** with the game closed, Apply your profile to Super Woden in Wheelkit 0.6.34.
   - It writes `BepInEx/config/dbce-controls.ini` and sets `[Input] Backend = Shim` for the next start.
   - Your Native bindings are kept. Shared bindings are wheel and shifter only; the keyboard keeps working.
   - For sequential or H-pattern gears, choose **Manual** in Woden's own settings; Wheelkit does not change it.
7. **Check that it took over:** launch, then open F6 **Controls**. It shows a status line and live bars for steer,
   throttle, brake and handbrake:
   - "Shim pilot active · wheel-only · native keyboard retained": the shared profile drives the car;
   - "Shim pilot: waiting for …" or "Shim refused; Native retained: <reason>": Native stays in charge;
   - "Shim stopped; wheel actions neutral until restart: <reason>": restart the game.
8. **Drive:** steering, pedals, handbrake, buttons, H-pattern gates 1–6, reverse and neutral. Clutch, Select and gates
   7/8 are not supported. Shared input needs the game window in the foreground.
9. **Force feedback while the shim drives (the open question):** force should start on the steering wheel the profile
   names, and steering and pedals must **keep responding after force starts**. The F6 Controls bars must keep moving. That ordering has never been run with
   real devices (see the [reader boundary](SHIM-PILOT.md#ffb-and-direct-provider-reader-boundary-source-review-2026-10-13)).
   If input freezes once force starts, stop and report it; do not drive on.
10. **Revert:** with the game closed, set `[Input] Backend = Native` in
    `BepInEx/config/dbce.wodenrallyedgewheel.cfg`. Wheelkit's Woden page only selects Shim. Then start the game and
    confirm the Native bindings are back.

## Not in this build

- Calibrated axis endpoints (controls schema 2, STD-037): the profile uses schema 1.
- A public release or Wheelkit's public installer for this receipt revision.
- Any change to the force tuning.
