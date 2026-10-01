# DBCE mods for Super Woden Rally Edge

Delivery **0.2.13-delivery.1** is a reviewed-runtime repack with package metadata
and local documentation. It retains runtime **0.2.13+14740fd** and its installer.
Candidate rig acceptance is pending. Read the [player guide](GUIDE.md).

Extract the whole ZIP into its own folder. On Windows, verify it with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Verify-Package.ps1 -PackageRoot .
```

This checks the package only; it does not start Woden, open devices, install
anything or provide a no-write target-install preflight. Run `Install.bat` only
with the supported game closed and installation separately authorized.
`Uninstall.bat` removes unchanged receipt-owned files. Existing settings,
recordings and other mods are retained. See [recovery and limits](GUIDE.md).

Wheel input, provisional FFB, telemetry and request-driven diagnostic capture
are retained. True triples and player playback commands are unavailable in this
ZIP. No collision effect, shared cadence adoption or game-input replay is added.

Package identity is `dbce-mods-super-woden-rally-edge`; the approved intended
repository name is `dbce-mods-woden-rally-edge`. That rename has not occurred.
The runtime loader/config/receipt identities remain unchanged. [License](LICENSE).
