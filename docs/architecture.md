# Architecture

The historical solution remains intact. SDK projects link original sources;
original decoder algorithms are not duplicated in the application or UI.

- `GARbro.Imaging`: owned immutable pixel buffers, packed formats, palettes and codecs.
- `GARbro.Core`: GameRes I/O, archive/image/audio abstractions and MEF discovery.
- `GARbro.Formats`: selected original providers, emitted as `ArcFormats.Portable.dll`.
- `GARbro.Application`: archive sessions, UI-independent options, extraction and previews.
- `GARbro.Cli`: command parsing and presentation of service results.

Core references Imaging; providers reference Core; Application references both
Core and providers. MEF preserves exports, metadata and original priorities.
Only the historical build includes WPF widget adapters. The modern source uses
explicit portable image types, without defining counterfeit System.Windows types.

Original decoders share a global catalog and sometimes use the process working
directory. The application serializes decoder operations and restores that
directory after each operation. Cancellation is cooperative during extraction;
it cannot interrupt a decoder blocked inside synchronous native/legacy code.
Extraction limits constrain copied output; they do not form a hard process memory
sandbox around legacy decoders. Those limits require further worker isolation.
