# Automatic H-pattern pedal eligibility

Request 175605. Isolated product successor based on frozen source eed77ffc. No build, test execution, game, install, device, video, lease or owner-state action. Frozen Woden06 input D3EF2185/source eed77/request 36ce1df2 and all Woden05 records remain unchanged.

## Concrete trigger and result

Native WheelInput.Apply previously applied HPatternShifter.Pedals whenever any gate was bound, then called Step with the car's Automatic flag. Step correctly suppressed shifts and displayed its existing manual-mode hint. Pedals had already cut throttle for neutral or swapped throttle/brake for reverse, even for an Automatic car.

The caller now reads that same Automatic flag once and supplies it to both pedal handling and Step. A small Core overload passes supplied normalized throttle/brake unchanged for Automatic and otherwise delegates to the existing three-argument manual Pedals method. The original method and signature remain unchanged for existing source/binary consumers; the Shim path retains its current admission behavior. Step and its manual-mode hint are unchanged. No persisted key, binding, calibration, native/shared API, sequential controls or physical output tuning changes.

| Gate | Input throttle/brake | Automatic | Manual retained |
| --- | --- | --- | --- |
| Neutral |0.7 /0.2 |0.7 /0.2 |0 /0.2 |
| Reverse |0.7 /0.2 |0.7 /0.2 |0.2 /0.7 |
| Forward 3 |0.7 /0.2 |0.7 /0.2 |0.7 /0.2 |

## Regression coverage and remaining validation

The existing WodenRallyEdge.Tests H-pattern coverage is retained. One added test covers Automatic neutral/reverse/forward pedal preservation, explicit Manual neutral/reverse/forward results, and suppression/reset of a pending manual shift with the existing hint on transition to Automatic: 9 authored checks. It exercises the exact new Core overload used by Native WheelInput; no copied predicate, new harness or input implementation.

Source diff/whitespace and control-character review passed. These are authored source assertions, not executed passes. Under hold 175205 there was no compiler or test execution. Remaining offline validation is the actual Core/Plugin build with existing warnings-as-errors and execution of the existing regression harness containing this H-pattern block in an authorized offline batch. That still would not prove native/game input delivery, a physical gearbox/pedal condition or owner acceptance. Merge/integration into another source line and any binary packaging/installation require their own exact reviewed pins.

Current owner EC81/97BBB readbacks are Native/Automatic with only Gear up/down, no H-pattern gates. Therefore this trigger is absent in that owner's current setup; this correction is not an established explanation or fix for their no-drive report. No owner configuration or frozen candidate is altered.

Toolkit comparison found all 37 IDs present;16/17/20 unchecked and31/37 pending remain open. This is a game-specific correction to the existing manual-only emulation contract, not a new family-wide standard or runtime adoption claim.
