
## Shared binding Save status (source-only candidate, October15)

BindingSaveStatus.cs is byte-exact from toolkit d342370f0397c041091d2fd8cc77488a9812d48c,
shared/ui/BindingSaveStatus.cs, MIT (root LICENSE), SHA256
D22B954166E904D6FFC11F2309B83ADFC5D5802D1D1692BF1D7EB585543FCAE5.
Optional ISharedBindingSaveStatus does not change ISharedBindingHost. Failed Save
text persists independently of capture/general status; only successful Save,
explicit Discard, or a new host clears it. No writer/capture/ActiveSaved/force
policy changes. Source-linked regressions are prepared; build/run pending.
