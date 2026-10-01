# Super Woden Rally Edge setup and readiness

The available component is [wheel 0.2.12](../components/wheel/README.md). It targets Steam app 3218630, build 21802346, Unity 6000.3.6f1 and the exact game assembly hash enforced by the component. Unknown builds require review before patching.

Use the existing packaged `Manage-Install.ps1` only with the game closed and installation separately authorized. It preserves verified backups, rollback and owned-file uninstall. Source reorganization changes no personal configuration, deployment, monitor profile or launch profile.

| Capability | Evidence and remaining acceptance |
|---|---|
| Wheel/pedals | Adapter implemented; preserve exact-device calibration and saved bindings. Actual acceptance remains scoped to recorded owner runs. |
| FFB | Provisional adapter; owner felt force on earlier builds. Current 0.2.12 live recording and formal sign/load/lifecycle acceptance are pending. F8 saves Off; never silently resume force. |
| Telemetry | Adapter and offline signal reprocessing implemented; actual driving capture coverage is pending. |
| Camera controls | Adapter implemented; framing, transitions and split-screen acceptance pending. |
| True triples | Blocked for everyday play: [default-off private-target probe 0.0.1](../components/triple/README.md) exists, but has no runtime/visual acceptance or separate-display/Surround presentation. Monitor span or FOV changes do not prove three projections. |

See [current state](../components/wheel/docs/STATE.md) for exact installed identities and evidence. Runtime work needs resumed owner authorization and a serialized device lease; source checks provide no rig-readiness claim.
