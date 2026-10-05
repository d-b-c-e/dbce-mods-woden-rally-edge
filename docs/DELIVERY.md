# Woden package delivery contract v1

Status as of 2026-10-02: private published main is
`0bb5784b55fcfa0e578fa28120682b6bf645a793`, tree
`2cc4ecef4998439c51077aa18e58c7617a5d850b`. This is source publication,
not a binary release. Installed build remains **0.2.12 / bf58042**; no
shutdown candidate installation or device acceptance has occurred.

The latest frozen local package candidate is **0.2.14-shutdown.1**, archive
`dbce-mods-super-woden-rally-edge-0.2.14-shutdown.1.zip`, SHA256
`b31bc2ccde9aa6530b200b5a608d9554b2828dd81ca5c368127f2ad30fa80d64`.
It is retained in private external review evidence, not an available public binary
download. `tools/game/Build-ShutdownDelivery.ps1` prepared it using the separate
exact successor adapter `Woden-ShutdownDelivery.ps1`. Preserve its bytes rather
than rerunning the generator at current HEAD. Its recorded provenance is:

| Role | Exact source revision |
|---|---|
| Consumer runtime | `e3d118e6d72e1e9847b73620e02e9f101fb22de3` |
| Shared native 502 / HAT39 shutdown override | `a51bed99ee1f9e02cc92a397c47f9461e225bf4b` |
| Packaging | `4286797be490c5e94c115827a0444b1beda6a62b` |
| Retained installer | `14740fdcba4a7e9c490a7e26d94f54614c75e5f7` |

Runtime/plugin and receipt version is `0.2.14+e3d118e6d72e1e9847b73620e02e9f101fb22de3`.
Later source-only capture, CLI and CI changes on main are not retroactively
included in this frozen package. Independent review evidence records 39 ZIP
semantic/mutation checks and 99 disposable installer/rollback/preservation checks;
these do not establish actual Unity/driver, physical force or visual acceptance.
Frozen metadata retains its preparation-time pending-review labels; external
review evidence supplements them without rewriting the archive. There is no
public binary release for this candidate, and deployment still requires coordinator
authorization, a closed game, the planned force-Off transaction and an attended rig slot.

Historical `tools/game/Package-Delivery.ps1` remains scoped to the immutable
14740fd runtime/installer baseline, manifest SHA256
`20e9c9beed5458f81c183e582a34067535a946c6a948a96d91817db013741a23`.
It produces retained **0.2.13-delivery.1**, plugin/receipt `0.2.13+14740fd`,
without rebuilding runtime bytes or installing anything. It is not the generator
for the shutdown candidate. Preserve this baseline and its exact verifier.

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
toolkit v0.12.0. Retained managed input-only override 9ad1f640 remains distinct
from the fresh unpublished native 502 override a51bed9, recorded in
native-build-provenance.json. The historical input-override catalog's native 501
entry is superseded only for this shutdown package by that explicit dependency.
Cadence candidates and collision activation remain excluded.

| Feature | Implementation in source | Frozen candidate payload | Candidate acceptance/default |
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

Package ID dbce-mods-super-woden-rally-edge maps explicitly to the verified renamed
private repository d-b-c-e/dbce-mods-woden-rally-edge (repository ID 1367936639).
The shutdown descriptor records mappingStatus verified and
verified-existing-repository-rename; historical provenance URLs remain retained
under the prior name. Legacy loader/config/receipt identities remain unchanged.
The installed optimizer contract is not adopted.

Setup retains the reviewed receipt/hash installer protections and backups.
Package verification is no-target-write but is not a target installation preflight.
There is no invented DryRun or rollback CLI. Caught failure rollback is verified
while closed; interrupted/conflicted operations require manual backup/report
review. The exact shutdown ZIP's 99-check installer and 39-check delivery results
are disposable offline review evidence, not a successful live installation. The
asset-free hosted CI subset does not recertify this archive or run full installer
rollback tests. Future package changes require fresh exact-byte review; attended
wheel/button/HAT, force lifecycle and visual acceptance remain separate gates.
