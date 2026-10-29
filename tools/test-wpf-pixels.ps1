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
if($failed.Count){throw "WPF pixel differences: $($failed -join ', ')"}
