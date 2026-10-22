# Shared support report source

Exact `shared/support/SupportReport.cs` and MIT license from toolkit commit
`ab638898f52ee482718e371594457ae493549551`.

Source SHA-256: `CA005A794086D66E3F7660EED692FB5583DE87B112BCA748FF60385A5944033A`.

The common source alone owns redaction, selected bounded log reads, immutable
snapshots and browser drafts. `Panel.Support.cs` supplies Woden's packaged
repository/version, known log path, UI state and deliberate shell actions.
Do not create a second sanitizer/collector here. Update the common toolkit
source and its pin together. No Wheelkit installation or auth token is needed.
