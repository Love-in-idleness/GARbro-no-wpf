# Third-party software

The portable projects preserve GARbro's MIT license and original source notices.
The following packages are redistributed through NuGet; their own license files
remain authoritative and must accompany binary distribution.

| Component | License | Purpose |
| --- | --- | --- |
| Magick.NET | Apache-2.0 | Portable PNG, BMP, JPEG, TIFF, WebP codecs |
| ImageMagick (bundled by Magick.NET) | ImageMagick License | Native codec implementation |
| Newtonsoft.Json | MIT | Resource options and scheme serialization |
| NAudio.Core / NAudio.Wasapi | MIT | RIFF/AIFF reading and Windows-only WMA decoding |
| NLayer | MIT | Managed MP3 decoding |
| BCnEncoder.Net | MIT | GPU texture decoding |
| CommunityToolkit.HighPerformance | MIT | Pixel and texture buffer operations |
| NVorbis | MIT | Managed Vorbis decoding |
| SharpZipLib | MIT | ZIP and BZip2 |
| K4os.Compression.LZ4 | MIT | LZ4 |
| Snappier | MIT | Snappy |
| ZstdSharp.Port | MIT | Zstandard |
| Avalonia and Inter fonts | MIT / SIL Open Font License respectively | Desktop UI and Latin font |
| Microsoft .NET packages | MIT | MEF and configuration |
| xUnit and test tooling | See respective package licenses | Tests only |

Magick.NET includes native platform assets. Framework-dependent distributions
must retain their runtime directories. No package relies on System.Drawing.Common.
The core does not open playback devices; some legacy compressed WAV conversions
still require an alternative portable decoder and report that limitation.
