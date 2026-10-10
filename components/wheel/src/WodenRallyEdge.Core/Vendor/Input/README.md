# Optional input source

MIT source, unchanged from dbce-wheel-mod-toolkit:

- `ControlBinding.cs`: `83b3f93`, shared STD-033 circular hat matching.
- `PovSnapshotReader.cs`: `58651c0`, optional export on the exact resident module.

No native binary or managed-wrapper ABI change. The input override's `c8ec2ee`
native already supplies `ReadDeviceStateWithPov`. Existing read slots, lifecycle
and exact eight-way F6 bindings remain owned by DeviceHub; only bindings with
`HatNeighbours=true` use the profile's circular 4500-unit rule. A failed coherent
read does not fall back to a second legacy read.
