# Generates src\OpsisAppearance.App\Assets\app.ico: a circle split into a light and a dark half with an accent ring.
# Run once (the result is committed): powershell -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
    $pad = [Math]::Max(1, [int]($s * 0.06)); $d = $s - 2 * $pad
    $rect = New-Object System.Drawing.Rectangle $pad, $pad, $d, $d
    $light = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 245, 246, 250))
    $dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 27, 31, 42))
    $g.FillPie($light, $rect, 90, 180)      # left half
    $g.FillPie($dark, $rect, 270, 180)      # right half
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 0, 120, 212)), ([Math]::Max(1.2, $s * 0.09))
    $g.DrawEllipse($pen, $rect)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    , @($s, $ms.ToArray())
}
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($p in $pngs) {
    $size = $p[0]; $bytes = $p[1]
    $w.Write([byte]($(if ($size -ge 256) { 0 } else { $size }))); $w.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$bytes.Length); $w.Write([uint32]$offset)
    $offset += $bytes.Length
}
foreach ($p in $pngs) { $w.Write($p[1]) }
$target = Join-Path $PSScriptRoot '..\src\OpsisAppearance.App\Assets\app.ico'
[System.IO.File]::WriteAllBytes((Resolve-Path (Split-Path $target)).Path + '\app.ico', $out.ToArray())
Write-Host "wrote app.ico ($($out.Length) bytes)"
