# Compatibility

The migration baseline is commit `698ccc57` (local tag `migration-baseline-2026-10-30`).
`compatibility/baseline-source.json` records 1,251 actual export declarations,
including four DEBUG-only providers. The observed Release catalog contains 1,247
exports and is frozen in `compatibility/baseline-runtime.json`. Every difference
is accounted for; commented-out declarations are excluded. Assemblies are part
of provider identity because the original catalog reuses one full class name.
`compatibility/portable-status.json` applies an explicit unported/not-tested
default to all baseline providers and overrides only compiled/tested variants.

The portable runtime currently discovers 34 exports. Generated fixtures verify
raw extraction for XFL, LWG, SAR, NSA, PFS (pf6/pf8), XP3 (raw/zlib), RPA-3, NPA,
YPF 0x100 and PACK/QLIE v2. LIM BGR565 pixels and basic PNG/BMP/TIFF round trips
are tested. These tests cover only the listed variants; they do not establish
compatibility with all games or all encryption schemes.

Other exports in the source inventory are preserved but remain unported.
Other variants of a compiled format remain unverified. `Legacy` and
`Experimental` have not been migrated. Portable scheme import/export is implemented and synthetic polymorphic schemes
round-trip. The complete legacy database still needs its exported JSON validated.
Bundled file-name lists are retained. User-supplied auxiliary game files, remaining
native backends and actual-game baseline comparisons remain release blockers. The application is a migration preview, not the 1.0 parity release.

XP3 requests encryption options even for archives with clear encryption flags,
matching the old decoder's behavior. Use `--scheme none` explicitly for unencrypted
XP3 fixtures. A missing key is reported; it is never silently guessed as no encryption.

The portable build and 38 tests (37 core/integration, one headless GUI smoke test)
pass on Linux, Windows and macOS at commit `de9ccdf1`. The current working branch
adds further audio and pixel-vector checks. All 12 positive archive fixtures also
match the historical Windows decoders. Headless GUI tests do not replace native
file-picker, compositor and audio-playback testing on actual desktops.
