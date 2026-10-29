# Compatibility

The migration baseline is commit `698ccc57` (tag `migration-baseline-2026-10-30`).
`compatibility/baseline-source.json` accounts for all 1,254 resource exports in
GameRes, ArcFormats, Legacy and Experimental. It records static declarations,
not verified runtime support. The original Windows workflow exports a runtime
catalog as a separate artifact.

The portable solution currently exercises archive I/O only. No game format has
been declared portable based on compilation alone. Windows/macOS results must
come from CI; local Linux results do not establish their status.
