# dbce_shim.dll (private build input, provisional)

The shared input shim behind its experimental C ABI 2, built from the reviewed outrun2-decomp sources (Astra 17050:
provisional vendoring until the shim moves to the toolkit, before the baseline is called reusable).

## Supply the dependency explicitly

The DLL is not part of the current source tree. A private developer build must
set the MSBuild property `PrivateShimPath` to an existing absolute path with the
exact hash below. There is no download or fallback. For example, after privately
obtaining the reviewed dependency:

```powershell
$env:PrivateShimPath = 'E:/private-dependencies/woden/dbce_shim.dll'
dotnet build components/wheel/WodenRallyEdgeWheel.sln -c Release -warnaserror
dotnet run --project components/wheel/tests/WodenRallyEdge.UiTests -c Release
```

The path shown is illustrative. The plugin and UI fixture copy that explicit
input to their ignored build outputs. `Run-Controls.ps1` takes it from the chosen
`CandidateDirectory` and verifies the unchanged hash; the runtime loader also
pins it. Public package allowlists do not include this DLL. Do not add it to a
public release until its source/license disposition is settled.

**October 12 correction:** commit `2a5a112` inadvertently tracked this private
DLL on the public experimental branch despite its restrictive notice. It has
been removed from current tracking, with the local test copy preserved and
ignored. Earlier commits still contain the file; removing it at the branch tip
does not erase that history or grant redistribution rights.

Validation of this correction: the full solution builds with zero warnings or
errors using the preserved private pin; the production UI fixture passes 881
assertions (including 67 shim/lease checks). Empty and nonexistent dependency
paths both fail before build. The controls runner parses, and its candidate
hash and runtime loader pin are unchanged. No game, device, install or physical
force was used in these checks.

| File | SHA-256 | Source |
|---|---|---|
| dbce_shim.dll | 9b8a036f664cf8f45cccf82b98e5e4376f93215b60eac50b6db965bf498015d8 | outrun2-decomp `codex/shim-device-path` f49cfc559f065b778967a7c94068a75df538ec69, target `dbce_shim` (Release, Visual Studio 2022 ClangCL x64, `-DOR2_XBOX=ON`) |
| dbce_shim.h | 3c63b472fc78770e8b9fc90ae92debbf979eedd7f8c68800f56b8690f4f65b74 | the same commit, `windows/shim_dll/dbce_shim.h` |

Based on input-shim `eea0660`, including the bounded sharing-violation retry in the
control reader. The additive device report names the exact owned handle's HID
interface path, published with its epoch and cleared on loss. No ABI structs or
exports were removed. Core 163, provider 106 and ABI 132 checks pass on this
build; no real hardware was used. Those counts are upstream evidence, not new
Woden checks. Woden's plugin loader additionally pins the DLL SHA-256 and refuses
another ABI, frame size or action list. This is the exact DRIVE pin, copied
without rebuilding. See `docs/SHIM-PILOT.md` in this repository for Woden's
consumer restriction and pending runtime qualification. This does not promote
the dependency to the shared toolkit or grant public redistribution rights.
