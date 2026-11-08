# Woden single-key save preservation, November 2

Kole assignment 497268; compiler run. Worktree
`E:/Source/_work/napo-woden-configline-20261102`, based on main c7e34f9.
Owner Steam Cloud choice remains unresolved: no launch, install, clean-player
preparation or owner-state changes were performed.

`WodenRallyEdge.Core.ConfigLine.Write`, used by F7 CompareView, previously read all
lines and rebuilt them with Environment.NewLine and UTF-8 without BOM. An intended
single-value save changed unrelated line separators/BOM/final-newline state.

The writer now keeps each separator, UTF-8 BOM state, the owned line's surrounding
spaces and the absence of a final newline. Duplicate owned keys/sections,
malformed headers, multiline values and unsupported encodings refuse without
writing. A missing entry uses an existing separator; the existing missing-file
section layout is retained. Writes use unique CreateNew siblings, Flush(true),
atomic replace and cleanup only after successful ownership. Cleanup errors do not
replace the primary save error. No stale temporary files are reclaimed.

This changes only the single-key writer. Full settings/bindings writers,
concurrent external-edit admission, camera policy, accepted FFB tune and input
handling are unchanged. STD-044 remains partial; STD-038..049 audit is in the ledger.

## Offline evidence

- New actual-file preservation fixture fails original ConfigLine as expected.
  The first launch-command attempt passed a build-only flag to the console app;
  its usage/exit2 log is retained separately. Explicit build then direct DLL
  execution establishes the negative-control regression (exit1).
- Fixed clean build/run: **104 suites, 52,642 assertions, zero failed**. Includes
  byte comparisons for mixed CRLF/LF, BOM/no BOM, no final newline, duplicate
  owned keys/sections, unsupported encoding, locked replacement and unrelated temp
  preservation. Existing missing-file/F7 and force/input fixtures remain passing.
- Core and Plugin Release builds: zero warnings/errors.
- Core SHA-256: `B5BE7B134BB29D5F1FBD26DD2646128E862F0E4E291D70444DE5571AC98603EC`.
- Plugin SHA-256: `17A745867922AD1D39E7FB042C9EAAF81CB0086E9EC75635027495E98F1A0731`.

Ignored evidence: this worktree's `artifacts/napo-configline-20261102/`, with
baseline/fixed/build logs, result.json and named lease logs. A redundant private
dependency copy is retained at `retained-redundant-lib/`: automatic approval review
rejected its deletion with "blocked by policy"; it was safely moved into this
ignored evidence directory instead, with no deletion. Only explicit source/doc
paths are committed; no private dependency is included.

No game/DLL/device/desktop/force use or owner settings/cloud/save writes. TEMP/TMP
restored, napo lease released and remi notified for the next CPU slot. Not packaged,
installed or runtime-tested. Live F7 save/restart and UI remain a later coordinated
qualification of a reviewed candidate, with exact restore and muted outputs.

## Public push decision

Woden is verified PUBLIC. The hub master handoff
`docs/handoffs/2026-10-25-MASTER-HANDOFF.md:18` says: "Never push branches to
**public** repos without asking" and explicitly lists woden-rally-edge. Kole
497462 confirms local main only and will route the public-push decision to the
owner. The tested source is committed/landed locally; no Woden push or release
publication is performed by this task without that authorization.
