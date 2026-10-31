# Install GARbro 0.2 preview

All packages require the external **.NET 10 Runtime** (not the Windows Desktop
Runtime or SDK). Linux packages support Debian 13 and Ubuntu 24.04, x64.

## Debian / Ubuntu

Enable a .NET 10 package source for your distribution using Microsoft's official
instructions (Ubuntu's distribution archive may also supply it). Then install:

```sh
sudo apt install ./garbro-0.2.0-preview.1-linux-x64.deb
```

APT installs the declared runtime and native dependencies. Launch GARbro from
the application menu, `garbro-desktop`, or Open With in a file manager. Native
Wayland is preferred; X11 is the fallback. The launcher does not open a terminal.
Game-resource MIME types are registered without changing existing defaults.
Choose GARbro in your file manager's Open With dialog to make it the default.
Uninstall with `sudo apt remove garbro`. User settings stay in your user profile.

The Linux tarball can also be unpacked and run without installation. See
[linux-desktop.md](linux-desktop.md) for native libraries and input-method diagnostics.

## Windows MSIX

Install the x64 .NET 10 Runtime first. Download the MSIX and **matching public
`.cer` certificate** from the same release and verify `SHA256SUMS`.
The MSIX uses a self-signed preview certificate. Review its subject
`CN=GARbro-no-wpf-Test` and fingerprint before trusting it. In an administrator
PowerShell, explicitly trust the downloaded public certificate:

```powershell
Import-Certificate -FilePath .\garbro-0.2.0-preview.1-win-x64.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
Add-AppxPackage .\garbro-0.2.0-preview.1-win-x64.msix
```

Trust the new matching certificate again for every release, then install the new
MSIX over the old version. The fixed package identity preserves upgrade order.
The signing private key is discarded by CI and is never distributed. Remove the
app in Windows Settings; remove the test certificate from Trusted People when
you no longer need it. Windows 10 2004 or later is required by the manifest;
CI tests installation and activation on its Windows runner, not every Windows edition.

Open With offers GARbro for game-specific extensions. Installation does not
replace the current default application. Common media, general archive formats,
DAT/BIN and executable types are deliberately excluded from registration.

The ZIP is an alternative that needs no certificate trust. Unpack all files and
run `desktop\GARbro.Desktop.exe`; the CLI is `cli\garbro.exe`.

## macOS

The unsigned ARM64 tarball contains GARbro.app and a separate CLI. Install .NET
10 ARM64 and retain the whole unpacked directory. This preview is not notarized.

## Settings and diagnostics

Settings use an atomic, versioned JSON file in the user's application-data
directory. Passwords and keys stay in the session. Help → Export logs creates a
local ZIP with redacted paths and environment information; it is never uploaded
automatically. See the bundled English/Chinese manuals and compatibility ledger.
