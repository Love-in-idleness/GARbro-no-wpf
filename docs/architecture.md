# Architecture

The historical solution remains intact. SDK projects link original sources;
original decoder algorithms are not duplicated in the application or UI.

- `GARbro.Imaging`: owned immutable pixel buffers, packed formats, palettes and codecs.
- `GARbro.Core`: GameRes I/O, archive/image/audio abstractions and MEF discovery.
- `GARbro.Formats`: selected original providers, emitted as `ArcFormats.Portable.dll`.
- `GARbro.Application`: archive sessions, UI-independent options, extraction and previews.
- `GARbro.Cli`: command parsing and presentation of service results.
- `GARbro.Worker`: bounded-protocol host used by the desktop application.

Core references Imaging; providers reference Core; Application references both
Core and providers. MEF preserves exports, metadata and original priorities.
Only the historical build includes WPF widget adapters. The modern source uses
explicit portable image types, without defining counterfeit System.Windows types.

Original decoders share a global catalog and sometimes use the process working
directory. The application serializes decoder operations and restores that
directory after each operation. CLI commands execute in supervised child processes.
The desktop keeps archive metadata and starts a worker for opening, previews and
extraction. Its UI process does not load a live archive session. File size/time
fingerprints reject stale metadata if an archive changes between operations.

The parent monitors elapsed time and resident memory, terminates the worker tree
on cancellation or a limit violation, and removes this operation's registered,
nonce-named temporary outputs. Completed outputs and pre-existing files remain.
The defaults are 300 seconds and 1 GiB per operation; CLI flags and desktop
advanced options can change them. Memory is sampled, so this is a resource guard,
not an OS security sandbox or an exact allocator cap. Direct library callers must
choose their own process boundary. Protocol messages are bounded and use fixed
JSON data models without CLR type activation.

Linux chooses native Wayland first, with X11 fallback. The official Avalonia
Wayland backend supplies text-input-v3; a public client-reset adapter re-enables
cached surrounding-text state after focus so wlroots negotiates the capability.
No reflection or patched framework binaries are used. Windows/macOS retain their
native backends.
