# SPDX-License-Identifier: EUPL-1.2
# Regenerates the application icon (exe, window, taskbar) and the MSIX logo assets.
[CmdletBinding()]
param(
    [string]$IconPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'src\Xfir.App\Assets\xFirW.ico'),
    [string]$StoreAssetDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'packaging\windows\Assets')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Drawing

# Colours from the main window: title bar navy, accent blue, secondary text.
$navy = [System.Drawing.Color]::FromArgb(0x13, 0x2A, 0x47)
$blue = [System.Drawing.Color]::FromArgb(0x32, 0x69, 0xBD)
$fold = [System.Drawing.Color]::FromArgb(0xAF, 0xC3, 0xDF)

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $path
}

function New-IconBitmap([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = [single]$size
    $brushes = @()
    try {
        # Rounded navy tile.
        $bg = New-Object System.Drawing.SolidBrush $navy; $brushes += $bg
        $tile = New-RoundedPath 0 0 ($s - 0.01) ($s - 0.01) ($s * 0.2)
        $g.FillPath($bg, $tile)

        # White sheet with a folded top-right corner (the form).
        $left = $s * 0.22; $top = $s * 0.13; $right = $s * 0.78; $bottom = $s * 0.87; $corner = $s * 0.17
        $sheet = New-Object System.Drawing.Drawing2D.GraphicsPath
        $sheet.AddLines([System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($left, $top),
            [System.Drawing.PointF]::new($right - $corner, $top),
            [System.Drawing.PointF]::new($right, $top + $corner),
            [System.Drawing.PointF]::new($right, $bottom),
            [System.Drawing.PointF]::new($left, $bottom)))
        $sheet.CloseFigure()
        $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White); $brushes += $white
        $g.FillPath($white, $sheet)
        $foldBrush = New-Object System.Drawing.SolidBrush $fold; $brushes += $foldBrush
        $g.FillPolygon($foldBrush, [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($right - $corner, $top),
            [System.Drawing.PointF]::new($right - $corner, $top + $corner),
            [System.Drawing.PointF]::new($right, $top + $corner)))

        $accent = New-Object System.Drawing.SolidBrush $blue; $brushes += $accent
        if ($size -lt 32) {
            # Letters are unreadable at small sizes: show the form's lines instead.
            foreach ($y in @(0.40, 0.55, 0.70)) {
                $g.FillRectangle($accent, $s * 0.32, $s * $y, $s * 0.36, [Math]::Max(1, $s * 0.08))
            }
        } else {
            $font = New-Object System.Drawing.Font 'Segoe UI', ($s * 0.36), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
            $format = New-Object System.Drawing.StringFormat
            $format.Alignment = [System.Drawing.StringAlignment]::Center
            $format.LineAlignment = [System.Drawing.StringAlignment]::Center
            try {
                $g.DrawString('xF', $font, $accent, [System.Drawing.RectangleF]::new($left, $top + $s * 0.14, $right - $left, $bottom - $top - $s * 0.26), $format)
            } finally { $format.Dispose(); $font.Dispose() }
            # Accent band at the foot of the sheet.
            $g.FillRectangle($accent, $s * 0.32, $s * 0.73, $s * 0.36, [Math]::Max(1, $s * 0.05))
        }
    } finally {
        foreach ($b in $brushes) { $b.Dispose() }
        $g.Dispose()
    }
    $bitmap
}

function Get-PngBytes([int]$size) {
    $bitmap = New-IconBitmap $size
    $stream = New-Object System.IO.MemoryStream
    try { $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png); , $stream.ToArray() }
    finally { $stream.Dispose(); $bitmap.Dispose() }
}

# Multi-size ICO with PNG-compressed entries (supported since Windows Vista).
$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($size in $sizes) { , (Get-PngBytes $size) }
New-Item -ItemType Directory -Path (Split-Path $IconPath -Parent) -Force | Out-Null
$file = [System.IO.File]::Create($IconPath)
$writer = New-Object System.IO.BinaryWriter $file
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) { $writer.Write($image) }
} finally { $writer.Dispose() }
Write-Host "Icon: $IconPath"

New-Item -ItemType Directory -Path $StoreAssetDir -Force | Out-Null
foreach ($asset in @(@('StoreLogo', 50), @('Square44x44Logo', 44), @('Square150x150Logo', 150))) {
    $path = Join-Path $StoreAssetDir ($asset[0] + '.png')
    [System.IO.File]::WriteAllBytes($path, (Get-PngBytes ([int]$asset[1])))
    Write-Host "Store asset: $path"
}
