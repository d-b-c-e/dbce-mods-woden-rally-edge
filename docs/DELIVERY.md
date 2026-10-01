# Woden package delivery contract v1

The authoritative player ZIP generator is `tools/game/Package-Delivery.ps1`.
It accepts only the independently reviewed immutable stage for runtime/installer
14740fd, manifest SHA256 20e9c9beed5458f81c183e582a34067535a946c6a948a96d91817db013741a23.
It never builds new runtime bytes, changes user settings or installs anything.
The product release is the explicitly versioned repack `0.2.13-delivery.1`;
retained plugin/receipt version `0.2.13+14740fd` remains independently identified.

The parser, schema, specification and acceptance fixture definitions are pinned
unchanged to published Art pilot 41d0378, contract 1.0.0. Their canonical Git-blob
byte SHA256 and Git blob pins are in tools/delivery/v1/PIN.json. The publication
record's canonicalSha256 values differ from actual raw Git blobs; actual bytes
match its checkoutSha256 values and listed blob IDs, as recorded in PIN.json.
The generic full integrity branch
supports Art only, so Woden invokes unchanged schema validation in MetadataOnly
mode then its own full integrity/semantic adapter. Neither alone certifies delivery.
No generic behavior or Art capability is forked into Woden.

Every package byte, including root delivery-manifest.json, is allowlisted and
SHA256 covered by manifest.json, except manifest.json itself. The delivery
descriptor hashes neither itself nor the inventory; the archive hash is external.
package-provenance.json independently records actual Git commit/tree snapshots,
separate runtime/installer and packaging roles, original baseline manifest hash
and retained artifact origins. Cross-record agreement is consistency, not compiler
attestation. The Recording dependency retains its explicitly dirty original
eight-source hash catalog and exact DLL pin; an exact clean commit is unknown,
so sourceCommit remains null with a reason. It is not attributed to released
toolkit v0.12.0. Input-only override 9ad1f640 remains distinct. Reviewed cadence
4ef65c16 is unadopted.

| Feature | Implementation in source | Shipped player implementation | Candidate acceptance/default |
|---|---|---|---|
| Wheel | Implemented | Retained calibrated input/button/HAT path | Pending; preserve-existing, fresh Off |
| FFB | Provisional implementation | Retained bounded force requests | Pending; preserve-existing, legacy fresh On; F8 saves Off |
| Telemetry | Implemented | Retained Forza/detailed UDP | Pending; preserve-existing, fresh On |
| Triple | Unavailable renderer; excluded research probe | No | Not applicable, unavailable |
| Recording | Request-driven diagnostic consumer | Retained; owner request helper is source-only | Pending; preserve-existing, fresh Off |
| Playback | Actual offline ForceSignal analysis | No player command; tooling remains source-only | Not applicable, unavailable |

Diagnostic raw input/force channels are not gameplay replay or physical torque.
Captures remain local-private, bounded and completion/identity checked. No
game-input replay or device playback is declared. No capture is started by metadata.

Package ID dbce-mods-super-woden-rally-edge maps explicitly to intended private
repository dbce-mods-woden-rally-edge; mappingStatus is proposed because remote
rename has not occurred. Legacy loader/config/receipt identities remain unchanged.
The installed optimizer contract is not adopted.

Setup retains the reviewed receipt/hash installer protections and backups.
Package verification is no-target-write but is not a target installation preflight.
There is no invented DryRun or rollback CLI. Caught failure rollback is verified
while closed; interrupted/conflicted operations require manual backup/report
review. Run the 99-check installer fixture against the extracted real ZIP plus
Test-Delivery.ps1 negative ZIP fixtures before independent review.
