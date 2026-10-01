# Super Woden Rally Edge coordination

Read [component instructions](components/wheel/AGENTS.md), [STATE](components/wheel/docs/STATE.md) and [HANDOFF](components/wheel/docs/HANDOFF.md) before component work. Their safety, ownership, fixture and release requirements remain in force.

Keep one game repository with independent components under `components/wheel` and, only when implemented, `components/triple`. Never create an empty triple component as evidence of support. Shared setup and release policy live in root `docs`; component versions and package layouts remain independent. No game/runtime/device actions are authorized by source verification.

Run `tools/game/Verify-Source.ps1` for the build and both executable regression harnesses; `dotnet test` does not execute them. Use component packaging and installer fixtures as documented. Preserve private visibility, all history and frozen archives. Do not include proprietary dependencies, owner recordings or settings in Git/packages.
