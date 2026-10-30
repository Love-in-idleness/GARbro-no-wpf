# Compatibility

The migration baseline is commit `698ccc57` (local tag `migration-baseline-2026-10-30`).
`compatibility/baseline-source.json` records 1,251 actual export declarations,
including four DEBUG-only providers. The observed Release catalog contains 1,247
exports and is frozen in `compatibility/baseline-runtime.json`. Every difference
is accounted for; commented-out declarations are excluded. Assemblies are part
of provider identity because the original catalog reuses one full class name.
`compatibility/portable-status.json` applies an explicit unported/not-tested
default to all baseline providers and overrides only compiled/tested variants.

The portable runtime currently discovers 1,218 exports. Generated fixtures verify
raw extraction for XFL, LWG, SAR, NSA, PFS (pf6/pf8), XP3 (raw/zlib), RPA-3, NPA,
YPF 0x100, PACK/QLIE v2, ZIP (stored/deflate) and Majiro v1/v2/v3, DL1 (raw/LZSS), PAK/ALPHA and PAK/ASURA (raw/LZSS). LIM BGR565 pixels, WAV/AIFF PCM retention, managed MP3/Vorbis conversion, and basic PNG/BMP/TIFF round trips
are tested. These tests cover only the listed variants; they do not establish
compatibility with all games or all encryption schemes.

Other exports in the source inventory are preserved but remain unported.
Other variants of a compiled format remain unverified. `Legacy` now builds and registers all 300 Release exports. Most of its providers
remain untested; the named synthetic archive fixtures and MBP/CBF BGR555 images cover
only their listed variants. `Experimental` remains unported. Portable scheme import/export is implemented and synthetic polymorphic schemes
round-trip. The source-version-148 database imports for all 77 consumed scheme groups,
including more than 500 XP3 definitions; that data is bundled in a deterministic
compressed JSON file. Import success does not verify all game-specific decryption.
Bundled file-name lists are retained. User-supplied auxiliary game files, remaining
native backends and actual-game baseline comparisons remain release blockers. The application is a migration preview, not the 1.0 parity release.

XP3 requests encryption options even for archives with clear encryption flags,
matching the old decoder's behavior. Use `--scheme none` explicitly for unencrypted
XP3 fixtures. A missing key is reported; it is never silently guessed as no encryption.

The CI matrix builds and tests Linux x64, Windows x64 and macOS arm64, including
archive, image, audio, safety and headless GUI checks. Preview archives are unpacked
and their published CLI, codecs and bundled scheme data are exercised separately. All 22 positive archive fixtures also
are checked against the historical Windows decoders in CI, alongside MBP/CBF, AGF and BBM pixels and metadata. Headless GUI tests do not replace native
file-picker, compositor and audio-playback testing on actual desktops.

The registered catalog count measures compilation and discovery, not successful
extraction. WMA decoding requires Windows Media Foundation. DAT/SIDEB uses
256-bit-block Rijndael, which still needs a replacement backend on modern .NET.
Other game-specific uses of Windows resource helpers remain unverified.

Portable scheme schema 2 retains inherited private fields and avoids computed
password setters. Bundled group identities and data digests are checked after
packaging; see `modern/GameData/README.md` for export provenance.
