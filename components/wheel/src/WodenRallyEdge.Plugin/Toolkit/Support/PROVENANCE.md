# Shared support report source

`shared/support/SupportReport.cs` and MIT license from toolkit commit
`ab638898f52ee482718e371594457ae493549551` (source SHA-256
`CA005A794086D66E3F7660EED692FB5583DE87B112BCA748FF60385A5944033A`), plus one line from toolkit
`320020208b19643d65067da904a9dcb17a85b80c`: any `token` key label is redacted, not only
`access_token`. The legacy-CLR changes `20b269f` and `969a109` are not applied.

Vendored SHA-256 (LF): `10A14DD10536F8039A47F33FB7704393381B730AC0D01E137D92BEEBB16D001A`.

The common source alone owns redaction, selected bounded log reads, immutable
snapshots and browser drafts. `Panel.Support.cs` supplies Woden's packaged
repository/version, known log path, UI state and deliberate shell actions.
Do not create a second sanitizer/collector here. Update the common toolkit
source and its pin together. No Wheelkit installation or auth token is needed.
