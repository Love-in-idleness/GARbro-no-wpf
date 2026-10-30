# GARbro-no-wpf

这是 GARbro 的 .NET 10 / Avalonia 跨平台迁移预览，优先保留资源解包能力。
This is a .NET 10 / Avalonia migration preview of GARbro, focused on preserving resource extraction.

下载 [v0.1.0-preview.3](https://github.com/Love-in-idleness/GARbro-no-wpf/releases/tag/v0.1.0-preview.3) 的 Linux x64、Windows x64、macOS arm64 预览包；运行需要 .NET 10 Runtime。
Download the [preview packages](https://github.com/Love-in-idleness/GARbro-no-wpf/releases/tag/v0.1.0-preview.3) for Linux x64, Windows x64 and macOS arm64. They require the .NET 10 runtime.

原版的 1,247 个 Release 格式导出均已编译并注册；具体游戏和格式变体的验证仍在进行，详见[兼容性说明](docs/compatibility.md)。
All 1,247 original Release format exports compile and register; game and format-variant validation is ongoing. See [compatibility and known limits](docs/compatibility.md).

Linux 桌面默认优先使用原生 Wayland，支持 text-input-v3 输入法协议，并在 Wayland 不可用时回退到 X11，详见 [Linux 桌面说明](docs/linux-desktop.md)。
Linux prefers native Wayland with text-input-v3, falling back to X11 when Wayland is unavailable. See [Linux desktop setup](docs/linux-desktop.md).

安装 .NET 10 SDK 后，可从仓库根目录构建与运行：
With the .NET 10 SDK installed, build and run from the repository root:

```sh
dotnet test GARbro.NoWpf.slnx -c Release
dotnet run --project modern/GARbro.Desktop
dotnet run --project modern/GARbro.Cli -- formats
dotnet run --project modern/GARbro.Cli -- extract archive.xp3 --scheme none -o output
dotnet run --project modern/GARbro.Cli -- convert image.lim -o image.png
```

提取保留资源的原始格式；图片转换通过独立命令完成。
Extraction retains the original resource format; image conversion is a separate command.

桌面解码与 CLI 命令使用受监督的工作进程，默认每次操作限时 300 秒、内存检查阈值 1 GiB；桌面高级选项或 CLI 的 `--timeout`、`--max-memory` 可调整这些限制。
Desktop decoding and CLI commands use supervised workers, with a default 300-second timeout and 1 GiB resident-memory threshold. Change these in the desktop advanced options or with CLI `--timeout` and `--max-memory`.

加密归档可使用 `--options options.json` 或 `--scheme` 指定参数，未加密 XP3 使用 `--scheme none`。
Use `--options options.json` or `--scheme` for encrypted archives, and `--scheme none` for unencrypted XP3.

已移植格式使用的公开游戏方案数据库随程序自动加载，也可用 `--schemes` 导入自己的配置。
The public game-scheme database for migrated formats loads automatically; use `--schemes` to import a custom configuration.

下方保留历史 Windows 版本的使用说明；该版本仍使用原有解决方案构建。
The historical Windows documentation follows; that version still builds with the original solution.

---

GARbro
======

Visual Novels resource browser.

Requires .NET Framework v4.7.2 or newer (https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472)

[Supported formats](https://morkt.github.io/GARbro/supported.html)

[Download latest release](https://github.com/crskycode/GARbro/releases)

Operation
---------

Browse through the file system to a file of interest.  If you think it's an
archive, try to 'enter' inside by pressing 'Enter' on it.  If GARbro
recognizes format its contents will be displayed just like regular file
system.  Some archives are encrypted, so you will be asked for credentials or
a supposed game title.  If game is not listed among presented options then
most likely archive could not be opened by current GARbro version.

Files could be extracted from archives by pressing 'F4', with all images and
audio converted to common formats in the process, of course if game format
itself is recognized.

When displaying file system contents GARbro assigns types to files based on
their names extension (so it's not always correct).  If types are misapplied,
it could be changed by selecting files and assigning type manually via context
menu 'Assign file type'.

GUI Hotkeys
-----------

<table>
<tr><td><kbd>Enter</kbd></td><td>                   Try to open selected file as archive -OR- playback audio file</td></tr>
<tr><td><kbd>Ctrl</kbd>+<kbd>PgDn</kbd></td><td>    Try to open selected file as archive</td></tr>
<tr><td><kbd>Ctrl</kbd>+<kbd>E</kbd></td><td>       Open current folder in Windows Explorer</td></tr>
<tr><td><kbd>Backspace</kbd></td><td>               Go back</td></tr>
<tr><td><kbd>Alt</kbd>+<kbd>&rarr;</kbd></td><td>   Go forward</td></tr>
<tr><td><kbd>Ctrl</kbd>+<kbd>PgUp</kbd></td><td>    Go to parent directory</td></tr>
<tr><td><kbd>Ctrl</kbd>+<kbd>O</kbd></td><td>       Choose file to open as archive</td></tr>
<tr><td><kbd>Ctrl</kbd>+<kbd>A</kbd></td><td>       Select all files</td></tr>
<tr><td><kbd>Space</kbd></td><td>                   Select next file</td></tr>
<tr><td><kbd>Numpad +</kbd></td><td>                Select files matching specified mask</td></tr>
<tr><td><kbd>F3</kbd></td><td>                      Create archive</td></tr>
<tr><td><kbd>F4</kbd></td><td>                      Extract selected files</td></tr>
<tr><td><kbd>F5</kbd></td><td>                      Refresh view</td></tr>
<tr><td><kbd>F6</kbd></td><td>                      Convert selected files</td></tr>
<tr><td><kbd>Delete</kbd></td><td>                  Delete selected files</td></tr>
<tr><td><kbd>Ctrl</kbd>+<kbd>H</kbd></td><td>       Fit window to a displayed image</td></tr>
<tr><td><kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>M</kbd></td><td>   Hide menu bar</td></tr>
<tr><td><kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>T</kbd></td><td>   Hide tool bar</td></tr>
<tr><td><kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>S</kbd></td><td>   Hide status bar</td></tr>
<tr><td><kbd>Ctrl</kbd>+<kbd>S</kbd></td><td>       Toggle scaling of large images</td></tr>
<tr><td><kbd>Ctrl</kbd>+<kbd>Q</kbd></td><td>       Exit</td></tr>
</table>

Author
------

Written by [morkt](https://github.com/morkt/GARbro) under [MIT License](https://github.com/morkt/GARbro/blob/master/LICENSE).

Korean translation by [mireado](https://github.com/mireado), [overworks](https://github.com/overworks)

Simplified Chinese translation by [elasticblitz](https://github.com/elasticblitz), [PeratX](https://github.com/PeratX) and [taroxd](https://github.com/taroxd)

Japanese translation by [haniwa55](https://github.com/haniwa55)

Contributors
------

<a href="https://github.com/crskycode/GARbro/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=crskycode/GARbro" />
</a>
