# Startup input isolation — 0.2.10

## Confirmed 0.2.9 gap

The 0.2.9 diagnostic run opened F6 through IMGUI while the startup message also advanced. Read-only analysis of the supported, hash-guarded game confirms `DailyMessage.Update` reads legacy `Input.anyKey` directly after its readiness flag. Either that read or one of two cached menu controls triggers the next scene. Its wait timer enables the prompt; it does not independently perform that scene change. The direct route bypasses both the existing GamePadSystem masks and the disabled EventSystem. This makes F6 the likely cause of the observed message dismissal, although the capture lacks per-frame handler/branch instrumentation to prove that exact event.

`TitleScreenScript.FixedUpdate` has two separate paths: an autonomous frame counter starts attract mode, while a direct legacy Return check (with Escape exclusion) or its cached Start control enters the menu. Attract mode behind settings therefore does not itself prove leaked input. The direct Return route is a confirmed source gap even though Enter was not exercised in the live check. Private disassembly and engine-call resolution remain under ignored `artifacts/opening-edge-audit`; no proprietary binary or disassembly is packaged.

## Scoped successor

0.2.10 guards only those two startup consumers. Before either executes, a focused, closed panel checks legacy F6, InputSystem F6 and the existing bound Settings action. This happens before the consumer can react to the opening input. The same opening observation precedes the mod's own menu navigation. The existing frame deduplication and IMGUI held latch prevent a delayed event from immediately closing the panel.

An early startup callback and the ordinary runtime update share one device poll per Unity frame. The poll uses existing readers and never enumerates or reopens devices. A failed poll remains failed for other consumers in that frame; a fresh frame can recover. Input read failure holds these consumers rather than treating unknown input as neutral.

While settings owns input, including release-to-close, the two startup callbacks are skipped. Their local animations/timers consequently wait with the panel open. Closed-panel native input, readiness and timed attract behavior continue unchanged. Global input backends, native selection/action maps, vehicle physics and force output infrastructure are untouched. Closing observes legacy keyboard state alongside InputSystem so legacy-only held keys cannot escape the release barrier.

Each actual guard logs its first blocked call. The normal ten-second diagnostic line reports blocked/total calls separately for DailyMessage and TitleScreen. Counters distinguish a patch being installed from its runtime execution.

## Evidence and remaining acceptance

The release build passes with zero warnings/errors. The executable regression harness passes **35 suites / 942 assertions**. The source-linked UI harness passes **532 assertions**, including actual Harmony prefix methods, opening detection, shared polling and release aggregation. Its native-behavior fixture independently models the observed message readiness/anyKey and title Return/Escape/timeout branches. It covers consumer-first and runtime-first ordering, early bound Settings, delayed IMGUI delivery, Enter/Space/cached Confirm suppression, held legacy input with a neutral InputSystem keyboard, fresh-release recovery, normal timed attract, failed reads and same-frame poll failures.

Independent review closed without a concrete blocker on the nine-file manifest SHA-256 `81a13d919817a941bebf9f6a37b47e7057c6e7a98bb1736622d12f8edc016dae`. It checked both native method identities, the legacy GetKeyDown wrapper, failed/disconnected reader behavior, and the absence of device reopening/enumeration or FFB work in early polling. The managed fixtures still do not establish actual Harmony execution or physical acceptance.

Reviewed runtime `25797b3` is installed from the distinct 0.2.10 installer-r3 package; [STATE](STATE.md) records the ZIP, installer source and receipt. The installer-only repair preserves all nine frozen runtime payloads and every owner setting. **Live startup/title acceptance NOT RUN: the user paused desktop testing.** Prior ZIPs remain unchanged.

After the user resumes desktop work and a serialized lease is granted, the next authorized five-minute force-disabled check should begin on the message screen, open F6 without dismissing it, use Enter/Space while open, cancel a provisional edit, and close after release. Then verify normal message dismissal, repeat F6/Enter on the title, and check both guard counters. Capture screenshots as each state is observed. Exit normally, restore the exact pre-run configurations, verify installed payloads and archive the consumed request. No physical force or driving test is part of this check.
