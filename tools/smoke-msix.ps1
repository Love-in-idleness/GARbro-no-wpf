param([string]$Output = 'artifacts', [string]$Version = '0.2.0-preview.1')
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path (Join-Path $Output "garbro-$Version-win-x64.msix")).Path
$certificate = (Resolve-Path (Join-Path $Output "garbro-$Version-win-x64.cer")).Path
$older = (Resolve-Path (Join-Path $Output 'msix-test/upgrade-from.msix')).Path
$olderCertificate = (Resolve-Path (Join-Path $Output 'msix-test/upgrade-from.cer')).Path
$trusted=$olderTrust=$root=$olderRoot=$null
$identity = 'LoveInIdleness.GARbro'
try {
    $trusted = Import-Certificate -FilePath $certificate -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
    $olderTrust = Import-Certificate -FilePath $olderCertificate -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $sdk = Get-ChildItem $sdkRoot -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'x64\signtool.exe') } | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $sdk) { throw 'Windows SDK SignTool is missing.' }
    $signtool = Join-Path $sdk.FullName 'x64\signtool.exe'
    try {
        # LocalMachine root import is noninteractive in this elevated disposable test.
        # Remove it before installing: installation must work with TrustedPeople only.
        $root = Import-Certificate -FilePath $certificate -CertStoreLocation 'Cert:\LocalMachine\Root'
        $olderRoot = Import-Certificate -FilePath $olderCertificate -CertStoreLocation 'Cert:\LocalMachine\Root'
        foreach ($candidate in @($package,$older)) {
            & $signtool verify /pa /v $candidate
            if ($LASTEXITCODE -ne 0) { throw 'MSIX signature verification failed.' }
        }
    } finally {
        if ($root) { Remove-Item -LiteralPath $root.PSPath; $root=$null }
        if ($olderRoot) { Remove-Item -LiteralPath $olderRoot.PSPath; $olderRoot=$null }
    }
    Write-Output 'Installing with TrustedPeople test certificates.'
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
[ComImport, Guid("F04061AC-1659-4a3f-A954-775AA57FC083"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAssocHandler {
    void GetName([MarshalAs(UnmanagedType.LPWStr)] out string name);
    void GetUIName([MarshalAs(UnmanagedType.LPWStr)] out string name);
    void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] out string path, out int index);
    [PreserveSig] int IsRecommended();
    void MakeDefault([MarshalAs(UnmanagedType.LPWStr)] string description);
    void Invoke(IntPtr data);
}
[ComImport, Guid("973810ae-9599-4b88-9e4d-6ee98c9552da"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IEnumAssocHandlers {
    [PreserveSig] int Next(uint count, [MarshalAs(UnmanagedType.Interface)] out IAssocHandler handler, out uint fetched);
}
[ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IShellItemArray {
    void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr result);
}
public static class PackageActivation {
    [DllImport("shell32.dll", CharSet=CharSet.Unicode, PreserveSig=false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, out IntPtr item);
    [DllImport("shell32.dll", PreserveSig=false)]
    static extern void SHCreateShellItemArrayFromShellItem(IntPtr item, ref Guid iid, out IntPtr array);
    [DllImport("shell32.dll", CharSet=CharSet.Unicode, PreserveSig=false)]
    static extern void SHAssocEnumHandlers(string extension, uint filter, out IEnumAssocHandlers handlers);
    public static void Open(string path) {
        IntPtr item=IntPtr.Zero, array=IntPtr.Zero, data=IntPtr.Zero;
        IShellItemArray shellArray=null; IEnumAssocHandlers handlers=null;
        try {
            var itemId=new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
            var arrayId=typeof(IShellItemArray).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref itemId, out item);
            SHCreateShellItemArrayFromShellItem(item, ref arrayId, out array);
            shellArray=(IShellItemArray)Marshal.GetObjectForIUnknown(array);
            var dataHandler=new Guid("b8c0bd9f-ed24-455c-83e6-d5390c4fe8c4");
            var dataId=new Guid("0000010e-0000-0000-C000-000000000046");
            shellArray.BindToHandler(IntPtr.Zero, ref dataHandler, ref dataId, out data);
            SHAssocEnumHandlers(System.IO.Path.GetExtension(path), 0, out handlers);
            IAssocHandler handler; uint fetched;
            while(handlers.Next(1,out handler,out fetched)==0 && fetched==1) {
                try {
                    string name; handler.GetUIName(out name);
                    if(name.IndexOf("GARbro",StringComparison.OrdinalIgnoreCase)>=0) {
                        handler.Invoke(data); return;
                    }
                } finally { Marshal.ReleaseComObject(handler); }
            }
            throw new InvalidOperationException("GARbro is missing from Windows Open With handlers.");
        } finally {
            if(handlers!=null)Marshal.ReleaseComObject(handlers);
            if(data!=IntPtr.Zero)Marshal.Release(data);
            if(shellArray!=null)Marshal.ReleaseComObject(shellArray);
            if(array!=IntPtr.Zero)Marshal.Release(array);
            if(item!=IntPtr.Zero)Marshal.Release(item);
        }
    }
}
'@
    $fixtureDirectory = Join-Path ([IO.Path]::GetTempPath()) ('GARbro ' + [char]0x6d4b + [char]0x8bd5 + ' test ' + [guid]::NewGuid())
    New-Item -ItemType Directory $fixtureDirectory | Out-Null
    $fixture = Join-Path $fixtureDirectory 'standard.xp3'
    # Recognized XFL bytes under an associated game-specific extension.
    Copy-Item 'tests/fixtures/generated/standard.xfl' $fixture
    [PackageActivation]::Open($fixture)
    $deadline = (Get-Date).AddSeconds(25)
    do {
        Start-Sleep -Milliseconds 250
        $process = Get-Process -Name GARbro.Desktop -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like '*standard.xp3*' } | Select-Object -First 1
        if ($process -and $process.MainWindowTitle -like '*standard.xp3*') { break }
    } while ((Get-Date) -lt $deadline)
    if (-not $process -or $process.MainWindowTitle -notlike '*standard.xp3*') { throw 'Packaged file activation failed.' }
    Stop-Process -Id $process.Id
    Remove-AppxPackage -Package $installed.PackageFullName
    if (Get-AppxPackage -Name $identity) { throw 'MSIX uninstall failed.' }
    Write-Output 'MSIX install, upgrade, associated-file activation and uninstall passed.'
} finally {
    Get-AppxPackage -Name $identity | Remove-AppxPackage
    if ($trusted) { Remove-Item -LiteralPath $trusted.PSPath }
    if ($root) { Remove-Item -LiteralPath $root.PSPath }
    if ($olderRoot) { Remove-Item -LiteralPath $olderRoot.PSPath }
    if ($olderTrust) { Remove-Item -LiteralPath $olderTrust.PSPath }
    if ($fixtureDirectory) { Remove-Item -LiteralPath $fixtureDirectory -Recurse -Force }
}
