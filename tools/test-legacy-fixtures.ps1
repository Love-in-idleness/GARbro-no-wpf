param([string]$Directory="bin/Release",[string]$Fixtures="tests/fixtures/generated")
$ErrorActionPreference="Stop"
$base=(Resolve-Path $Directory).Path
[System.Reflection.Assembly]::LoadFrom((Join-Path $base "GameRes.dll")) | Out-Null
$catalog=[GameRes.FormatCatalog]::Instance
$handler=[GameRes.ParametersRequestEventHandler]{ param($source,$eventArgs)
    $eventArgs.Options=$source.GetDefaultOptions();$eventArgs.InputResult=$true
}
$catalog.add_ParametersRequest($handler)
$manifest=Get-Content (Join-Path $Fixtures "manifest.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$sha=[System.Security.Cryptography.SHA256]::Create()
try {
    foreach($fixture in $manifest.archives) {
        if($fixture.file -in @("unsafe.xfl","collision.xfl")){continue}
        $path=(Resolve-Path (Join-Path $Fixtures $fixture.file)).Path
        $arc=[GameRes.ArcFile]::TryOpen($path)
        if($null -eq $arc){throw "Legacy cannot open $($fixture.file): $($catalog.LastError)"}
        try {
            if($arc.Tag -ne $fixture.format){throw "Wrong format for $($fixture.file): $($arc.Tag)"}
            if($arc.Dir.Count -ne $fixture.entries.Count){throw "Wrong entry count for $($fixture.file)"}
            foreach($expected in $fixture.entries) {
                $entry=@($arc.Dir | Where-Object { $_.Name.Replace('\','/') -ceq $expected.path })
                if($entry.Count -ne 1){throw "Missing or duplicate $($expected.path) in $($fixture.file)"}
                $input=$arc.OpenEntry($entry[0]);$copy=New-Object System.IO.MemoryStream
                try {
                    $input.CopyTo($copy)
                    $actual=([BitConverter]::ToString($sha.ComputeHash($copy.ToArray()))).Replace('-','').ToLowerInvariant()
                    if($copy.Length -ne $expected.size -or $actual -ne $expected.sha256){throw "Digest mismatch for $($fixture.file)/$($expected.path)"}
                } finally {$input.Dispose();$copy.Dispose()}
            }
            Write-Output "PASS $($fixture.file)"
        } finally {$arc.Dispose()}
    }
} finally {$sha.Dispose();$catalog.remove_ParametersRequest($handler)}
