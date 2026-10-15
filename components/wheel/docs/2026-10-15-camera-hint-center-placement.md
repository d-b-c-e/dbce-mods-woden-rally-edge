# Camera hints on the gameplay center display

Nonblocking source-only follow-up for Sana162265 during owner UAT. This branch is isolated from the installed0.2.16+d45 candidate; it changes only `Panel.DrawCameraMessage` presentation. No build, test execution, game, input/display operation or installed/config/tune change was performed.

While `TripleView.Active`, camera hint/toast boxes reuse `SessionStatusPresentation.Layout` (already used by the recording HUD) as their center-display anchor. Their existing maximum width is capped by one display's width and existing gutters. Single-view placement stays bottom-left. Text, six-second FOV message lifetime, font scale, vertical position/height, focus/ownership gates, saved FOV mode/defaults/poses, projection geometry and force behavior are untouched. No shared helper or vendored source was modified.

Manual source review of box bounds, not rendered evidence:

| Viewport | Before | Proposed | Result |
|---|---|---|---|
|7680x1440, triples active|x36..1536|x3090..4590, y1305..1401|Inside gameplay center display2560..5120; existing1500px width/height retained|
|3840x1080, triples active|x36..1536|x1316..2524|Inside center1280..2560 with36px gutters; width constrained to1208|
|1280x720, triples inactive|x24..1024|x24..1024|Existing single-view placement retained|
|3840px wide, triples inactive|x36..1536|x36..1536|Wide single-view behavior is not treated as three displays|

Panel.cs passed syntax parsing with the host's existing Roslyn5.0 parser, zero syntax errors; no compilation, semantic binding or fixture was run. The first SDK-parser load was refused by the host's assembly version resolution, before parsing; the compatible existing host parser was used without installing/changing dependencies. Diff/whitespace review passed. Build/runtime/readability acceptance is deferred until after UAT and a separate handoff. Keep this separate from owner FOV/force acceptance and from the release-blocker queue.
