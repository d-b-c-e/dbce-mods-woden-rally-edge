# dbce_shim.dll (vendored, provisional)

The shared input shim behind its experimental C ABI 2, built from the reviewed outrun2-decomp sources (Astra 17050:
provisional vendoring until the shim moves to the toolkit, before the baseline is called reusable).

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
