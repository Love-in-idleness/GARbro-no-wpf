# Third-party software

The portable projects preserve GARbro's MIT license and original source notices.
The following packages are redistributed through NuGet; their own license files
remain authoritative and must accompany binary distribution.

| Component | License | Purpose |
| --- | --- | --- |
| Magick.NET | Apache-2.0 | Portable PNG, BMP, JPEG, TIFF, WebP codecs |
| ImageMagick (bundled by Magick.NET) | ImageMagick License | Native codec implementation |
| Newtonsoft.Json | MIT | Resource options and scheme serialization |
| NAudio.Core | MIT | RIFF/PCM reading, without audio devices |
| NVorbis | MIT | Managed Vorbis decoding |
| SharpZipLib | MIT | NScripter BZip2 |
| K4os.Compression.LZ4 | MIT | LZ4 |
| Snappier | MIT | Snappy |
| ZstdSharp.Port | MIT | Zstandard |
| Microsoft .NET packages | MIT | MEF and configuration |
| xUnit and test tooling | See respective package licenses | Tests only |

Magick.NET includes native platform assets. Framework-dependent distributions
must retain their runtime directories. No package relies on System.Drawing.Common.
The core does not open playback devices; some legacy compressed WAV conversions
still require an alternative portable decoder and report that limitation.
