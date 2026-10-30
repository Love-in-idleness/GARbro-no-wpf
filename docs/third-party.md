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
| Concentus | BSD-3-Clause | Managed Opus decoding |
| Concentus.Oggfile | MS-PL | Ogg packet reading |
| BCnEncoder.Net | MIT | GPU texture decoding |
| CommunityToolkit.HighPerformance | MIT | Pixel and texture buffer operations |
| NVorbis | MIT | Managed Vorbis decoding |
| SharpZipLib | MIT | ZIP and BZip2 |
| K4os.Compression.LZ4 | MIT | LZ4 |
| Snappier | MIT | Snappy |
| ZstdSharp.Port | MIT | Zstandard |
| Avalonia and Inter fonts | MIT / SIL Open Font License respectively | Desktop UI and Latin font |
| Microsoft .NET packages | MIT | MEF, configuration and bounded NRBF record parsing |
| GPPG / GPLEX runtime sources | BSD-3-Clause | Existing Artemis IPT parser and scanner |
| xUnit and test tooling | See respective package licenses | Tests only |

Magick.NET includes native platform assets. Framework-dependent distributions
must retain their runtime directories. No package relies on System.Drawing.Common.
The core does not open playback devices; some legacy compressed WAV conversions
still require an alternative portable decoder and report that limitation.

The GPPG and GPLEX notices in `docs/licenses` are copied from the original
maintainer's repositories:
[GPPG](https://github.com/k-john-gough/gppg/blob/master/LICENSE.md)
(blob `61bd8adb2d7088b7d8bd69e0ba35cc9bd9ad7bb4`) and
[GPLEX](https://github.com/k-john-gough/gplex/blob/master/LICENSE.md)
(blob `09bff8aa94fb915ceb4168651bf639edabb464de`). The release packager includes
these source notices alongside dependency license files.
The Concentus.Oggfile source notice is from its package's repository revision
`dee65d9de0ef009658c2d60be6fd9b29d362a482` and is included separately because the
NuGet package declares MS-PL without embedding the license text.
