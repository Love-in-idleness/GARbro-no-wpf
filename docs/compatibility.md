# Compatibility

The migration baseline is commit `698ccc57` (local tag `migration-baseline-2026-10-30`).
`compatibility/baseline-source.json` accounts for all 1,254 resource exports in
GameRes, ArcFormats, Legacy and Experimental. Static declarations do not establish
runtime support. The historical Windows workflow exports its actual MEF catalog.

The portable runtime currently discovers 34 exports. Generated fixtures verify
raw extraction for XFL, LWG, SAR, NSA, PFS (pf6/pf8), XP3 (raw/zlib), RPA-3, NPA,
YPF 0x100 and PACK/QLIE v2. LIM BGR565 pixels and basic PNG/BMP/TIFF round trips
are tested. These tests cover only the listed variants; they do not establish
compatibility with all games or all encryption schemes.

Other exports in the source inventory are preserved but remain unported.
Other variants of a compiled format remain unverified. `Legacy` and
`Experimental` have not been migrated. Encrypted game schemes, auxiliary game
files, remaining native backends and actual-game baseline comparisons are still
release blockers. The application is a migration preview, not the 1.0 parity release.

XP3 requests encryption options even for archives with clear encryption flags,
matching the old decoder's behavior. Use `--scheme none` explicitly for unencrypted
XP3 fixtures. A missing key is reported; it is never silently guessed as no encryption.

Linux verification is local. Windows/macOS claims require completed portable CI;
the original Windows build passes independently.
