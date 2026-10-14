# Super Woden: ready to test (2026-10-14)

**Installed (2026-10-14, 15:21 CT):** private `0.2.14+24d7203e684e45b1a69ba6a6db3ba2b1d0b28179`, receipt `D50B71A2…`
(23/23 payloads), the save-guard and gearbox-at-start candidate. The saved backend is **Native** and **Gearbox at start**
is **Keep**, with the owner's force tune unchanged. The optional shared-input shim (`dbce_shim.dll` `9B8A036F…`) is
installed but not opted in. The owner runs Wheelkit 0.6.42. The previous `f252deb` install is in
`WodenWheelBackups\before-install-20261014-152117-cd7626c5`.

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
6. **Opt in:** with the game closed, Apply your profile to Super Woden in Wheelkit 0.6.42.
   - It writes `BepInEx/config/dbce-controls.ini` and sets `[Input] Backend = Shim` for the next start.
   - Your Native bindings are kept. Shared bindings are wheel and shifter only; the keyboard keeps working.
   - The same Apply sets **Gearbox at start** from your profile: Sequential or H-pattern selects Manual, Automatic
     selects Automatic (section C).
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

## C. Gearbox at start

The installed `24d7203` package (ZIP `4ffb64f2…`, see the
[qualification note](2026-10-14-native-gearbox-qualification.md#run-03-save-guard-candidate-0810-ct)) reads this setting.
Before the install, Wheelkit 0.6.42 did not offer it; after it, Wheelkit's own rule offers it (H-pattern selects
Manual, Automatic selects Automatic). If Wheelkit says "this Woden build does not read the gearbox-at-start setting",
the install has been replaced; report it.

11. **Choose, with the game closed.** Use any one of these:
    - F6 **Controls** (or **Shim**): **Gearbox at start**, Keep / Manual / Automatic;
    - Wheelkit 0.6.42: **Gearbox at start** on Super Woden's Controls settings page;
    - Wheelkit Apply of your profile: Sequential or H-pattern selects Manual, Automatic selects Automatic.

    Keep, the default, leaves the game's own choice alone.
12. **Start the game and reach the first main menu.** F6 **Controls** shows "This start:" followed by one of these:
    - "Manual: set Auto_Transmission[0], Arcade_Auto_Transmission[0] for this start (…)": the mod changed the game's
      choice and read it back;
    - "Manual: the game's choice already matched": there was nothing to change;
    - "not applied: …" or "incomplete: …": the game's choice may differ from your pick. Set it in Woden's own settings
      and report the text.
13. **Drive one stage.** The car uses the chosen gearbox. The car-select menu can still change it, and the game saves it
    as usual.
14. **Saving is normal.** The save guard only acts for an automated test's request or a muted stage replay. Ordinary play
    saves as before.

## Not in this build

- Calibrated axis endpoints (controls schema 2, STD-037): the profile uses schema 1.
- A public release or Wheelkit's public installer for this receipt revision.
- Any change to the force tuning.
