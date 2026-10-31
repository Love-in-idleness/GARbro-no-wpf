param([string]$Output = 'artifacts', [string]$Version = '0.2.0-preview.1')
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path (Join-Path $Output "garbro-$Version-win-x64.msix")).Path
$certificate = (Resolve-Path (Join-Path $Output "garbro-$Version-win-x64.cer")).Path
$older = (Resolve-Path (Join-Path $Output 'msix-test/upgrade-from.msix')).Path
$trusted = Import-Certificate -FilePath $certificate -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
$root = Import-Certificate -FilePath $certificate -CertStoreLocation 'Cert:\CurrentUser\Root'
$identity = 'LoveInIdleness.GARbro'
try {
    Add-AppxPackage -Path $older
    $before = Get-AppxPackage -Name $identity
    if (-not $before) { throw 'MSIX did not install.' }
    Add-AppxPackage -Path $package
    $installed = Get-AppxPackage -Name $identity
    if ([version]$installed.Version -le [version]$before.Version) { throw 'MSIX upgrade did not advance the version.' }
    if ($installed.PackageFamilyName -ne $before.PackageFamilyName) { throw 'MSIX identity changed during upgrade.' }
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
[ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IApplicationActivationManager {
    [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string args, uint options, out uint pid);
    [PreserveSig] int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint pid);
    [PreserveSig] int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr items, out uint pid);
}
[ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")] class ApplicationActivationManager {}
public static class PackageActivation {
    [DllImport("shell32.dll", CharSet=CharSet.Unicode, PreserveSig=false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, out IntPtr item);
    [DllImport("shell32.dll", PreserveSig=false)]
    static extern void SHCreateShellItemArrayFromShellItem(IntPtr item, ref Guid iid, out IntPtr array);
    public static uint Open(string id, string path) {
        var manager=(IApplicationActivationManager)new ApplicationActivationManager();
        IntPtr item=IntPtr.Zero, array=IntPtr.Zero;
        try {
            var itemId=new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
            var arrayId=new Guid("b63ea76d-1f85-456f-a19c-48159efa858b");
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref itemId, out item);
            SHCreateShellItemArrayFromShellItem(item, ref arrayId, out array);
            uint pid; Marshal.ThrowExceptionForHR(manager.ActivateForFile(id, array, "open", out pid)); return pid;
        } finally { if(array!=IntPtr.Zero)Marshal.Release(array); if(item!=IntPtr.Zero)Marshal.Release(item); Marshal.ReleaseComObject(manager); }
    }
}
'@
    $fixtureDirectory = Join-Path ([IO.Path]::GetTempPath()) ('GARbro test ' + [guid]::NewGuid())
    New-Item -ItemType Directory $fixtureDirectory | Out-Null
    $fixture = Join-Path $fixtureDirectory 'standard.xp3'
    # Recognized XFL bytes under an associated game-specific extension.
    Copy-Item 'tests/fixtures/generated/standard.xfl' $fixture
    $activatedPid = [PackageActivation]::Open($installed.PackageFamilyName + '!GARbro', $fixture)
    $deadline = (Get-Date).AddSeconds(25)
    do {
        Start-Sleep -Milliseconds 250
        $process = Get-Process -Id $activatedPid -ErrorAction SilentlyContinue
        if ($process -and $process.MainWindowTitle -like '*standard.xp3*') { break }
    } while ((Get-Date) -lt $deadline)
    if (-not $process -or $process.MainWindowTitle -notlike '*standard.xp3*') { throw 'Packaged file activation failed.' }
    Stop-Process -Id $activatedPid
    Remove-AppxPackage -Package $installed.PackageFullName
    if (Get-AppxPackage -Name $identity) { throw 'MSIX uninstall failed.' }
    Write-Output 'MSIX install, upgrade, associated-file activation and uninstall passed.'
} finally {
    Get-AppxPackage -Name $identity | Remove-AppxPackage
    if ($trusted) { Remove-Item -LiteralPath $trusted.PSPath }
    if ($root) { Remove-Item -LiteralPath $root.PSPath }
    if ($fixtureDirectory) { Remove-Item -LiteralPath $fixtureDirectory -Recurse -Force }
}
