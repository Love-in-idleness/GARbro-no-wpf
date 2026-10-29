param([string]$Directory = "bin/Release", [string]$Output = "artifacts/legacy-catalog.json")
$ErrorActionPreference = "Stop"
$base = (Resolve-Path $Directory).Path
[System.Reflection.Assembly]::LoadFrom((Join-Path $base "GameRes.dll")) | Out-Null
$catalog = [GameRes.FormatCatalog]::Instance
$rows = @($catalog.Formats | ForEach-Object {
    [ordered]@{ tag = $_.Tag; type = $_.Type; class = $_.GetType().FullName;
        assembly = $_.GetType().Assembly.GetName().Name; description = $_.Description;
        signatures = @($_.Signatures); extensions = @($_.Extensions); can_write = $_.CanWrite }
} | Sort-Object type,tag,class)
New-Item -ItemType Directory -Force (Split-Path -Parent $Output) | Out-Null
ConvertTo-Json -Depth 8 -InputObject $rows | Set-Content -Encoding UTF8 $Output
Write-Output "$($rows.Count) runtime formats exported"
