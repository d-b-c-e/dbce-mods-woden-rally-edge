# Independent component releases

Wheel source, tests, private dependency caches and packaging tools live under `components/wheel`. Run its classic solution and packaging script from that folder. Version remains 0.2.12; assembly names, mod ID, installer receipt layout and ZIP payload layout remain unchanged. Independent triple probe 0.0.1 lives under `components/triple`, imported from `d4ce04a80310ac54bb80740b3e27c99605fc26ce`. It is unreleased, default off and not added to the wheel solution/installer. No component or bundle is released by this change.

Preserve all existing history, tags, release links and immutable ZIP bytes. New component tags may use `wheel/vX.Y.Z`, future triple tags `triple/vX.Y.Z`, and bundles `bundle/vX.Y.Z`; do not retag historical releases. Packaging remains allowlisted and refuses overwriting the frozen 0.2.12 filename.

The intended repository name is private `d-b-c-e/dbce-mods-super-woden-rally-edge`, reusing the existing repository identity through a rename after owner handoff. Never reuse the old repository name, which would break GitHub redirects. No binary release is published as part of source organization. Future bundles must identify exact independent component versions, manifests and acceptance limits.

Proprietary game/interop/loader assemblies, owner settings, recordings and caches remain excluded from Git and release payloads. Offline build, UI and installer fixtures do not establish rendered triples or physical FFB acceptance.
