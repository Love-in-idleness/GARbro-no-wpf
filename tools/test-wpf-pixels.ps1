param([string]$Vectors="tests/fixtures/generated/pixel-vectors.json")
$ErrorActionPreference="Stop"
Add-Type -AssemblyName PresentationCore
$cases=Get-Content $Vectors -Raw | ConvertFrom-Json
$failed=@()
foreach($case in $cases){
    $format=[System.Windows.Media.PixelFormats].GetProperty($case.format).GetValue($null,$null)
    $palette=$null
    if($null -ne $case.palette){
        $colors=New-Object 'System.Collections.Generic.List[System.Windows.Media.Color]'
        foreach($color in $case.palette){$colors.Add([System.Windows.Media.Color]::FromArgb($color.a,$color.r,$color.g,$color.b))}
        $palette=[System.Windows.Media.Imaging.BitmapPalette]::new($colors)
    }
    $data=[Convert]::FromBase64String($case.pixels)
    $bitmap=[System.Windows.Media.Imaging.BitmapSource]::Create($case.width,$case.height,96,96,$format,$palette,[System.Array]$data,$case.stride)
    $converted=[System.Windows.Media.Imaging.FormatConvertedBitmap]::new()
    $converted.BeginInit();$converted.Source=$bitmap;$converted.DestinationFormat=[System.Windows.Media.PixelFormats]::Bgra32;$converted.EndInit()
    $output=New-Object byte[] ($case.width*$case.height*4)
    $converted.CopyPixels($output,$case.width*4,0)
    if([Convert]::ToBase64String($output) -cne $case.bgra32){
        $failed+=$case.name
        Write-Output "MISMATCH $($case.name): $([BitConverter]::ToString($output))"
    }else{Write-Output "PASS $($case.name)"}
}
# Compare the independent Windows reference for every possible Gray16 input.
$bounds=(Get-Content "tests/fixtures/generated/gray16-quantization.json" -Raw | ConvertFrom-Json).lowerBounds
$ramp=New-Object byte[] 131072
for($value=0;$value -lt 65536;$value++){$ramp[2*$value]=$value -band 255;$ramp[2*$value+1]=$value -shr 8}
$bitmap=[System.Windows.Media.Imaging.BitmapSource]::Create(256,256,96,96,[System.Windows.Media.PixelFormats]::Gray16,$null,[System.Array]$ramp,512)
$converted=[System.Windows.Media.Imaging.FormatConvertedBitmap]::new($bitmap,[System.Windows.Media.PixelFormats]::Bgra32,$null,0)
$output=New-Object byte[] 262144
$converted.CopyPixels($output,1024,0)
$level=0;$differences=0
for($value=0;$value -lt 65536;$value++){
    while($level -lt $bounds.Count -and $value -ge $bounds[$level]){$level++}
    $offset=4*$value
    if($output[$offset] -ne $level -or $output[$offset+1] -ne $level -or $output[$offset+2] -ne $level -or $output[$offset+3] -ne 255){
        if($differences -lt 12){Write-Output "Gray16 difference at $value : expected $level, actual $($output[$offset])"}
        $differences++
    }
}
if($differences){$failed+='Gray16 exhaustive';Write-Output "Gray16 exhaustive differences: $differences"}
else{Write-Output "PASS all 65536 Gray16 values"}
if($failed.Count){throw "WPF pixel differences: $($failed -join ', ')"}
