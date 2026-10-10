`NativeTestInjectionClient.cs` is an unchanged MIT-licensed developer client from
dbce-wheel-mod-toolkit, source caeb14c083c413d704ac5b62a94621a8320d4155
(merged in 09dc483). SHA-256:
`0DA63B3D2E7C1255E60454803BCE61B378492A4F6CF8331D856FC380E633A03E`.

It resolves only exports of the already resident hash-pinned module; it never
loads a DLL or opens a device. The toolkit's 79 fake-client checks and x64/x86
native fence checks are separate from Woden hook/runtime qualification. This
addon is excluded from all player packages and does not change their toolkit
pin. Do not replace the installed native outside a separately snapshotted test.
