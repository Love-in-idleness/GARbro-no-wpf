# Migration status

This is an executable migration preview. It is not the completed 1.0 compatibility release.

| Milestone | Implemented and verified | Remaining gate |
| --- | --- | --- |
| M0 | Frozen source and Windows runtime inventories; 1,247 Release exports plus four DEBUG-only declarations; generated fixtures | Extend fixtures with legally usable real-game cases |
| M1 | Portable mapped I/O, boundaries, partial reads, code pages and MEF; three-platform tests | Continue regression coverage as providers migrate |
| M2 | Independent pixel buffers and basic raster codecs; packed/indexed/alpha vectors and WPF comparisons | Broader actual-image corpus and metadata parity; WPF vectors cover all observed source pixel kinds |
| M3 | 1,233 registered exports, including all 300 Legacy exports; 18 archive formats verified on 25 synthetic variants | Migrate the other 14 Release exports, remaining composited image paths and Experimental |
| M4 | CLI commands, JSON, options, batches, explicit overwrites, progress/cancellation in services | Validate game-specific decryption, conversion variants and production corpus |
| M5 | Path/link/collision checks and bounded copied output | Remaining backends, worker memory/time isolation, nested-archive recursion; broader game corpus |
| M6 | Avalonia open/tree/list/search/preview/extract/cancel flows; headless smoke test on three platforms | Native desktop/portal tests, audio playback and full UI parity |
| M7 | Three-platform build/test CI, unpacked package checks and a gated release workflow with checksums | Validate all preview packages, finish full compatibility gates, sign and publish a stable release |

The catalog ledger gives every original provider an explicit status; an export
that is unported or untested is never counted as extraction support. Generated
fixtures prove only their documented variants. See [compatibility](compatibility.md)
and [testing](testing.md) for reproducible evidence.
