<#
.SYNOPSIS
    Generates assets/icon.ico for Inventory Tracker.
    Requires ImageMagick (magick) on PATH.
    On GitHub Actions: pre-installed on windows-latest.
    Locally: choco install imagemagick.app
#>

Add-Type -AssemblyName System.Drawing

function New-ITBitmap([int]$Size) {
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # Blue-600 background
    $g.Clear([System.Drawing.Color]::FromArgb(255, 37, 99, 235))

    # Rounded corner clipping path
    $radius = [int]($Size * 0.18)
    $path   = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $radius * 2, $radius * 2, 180, 90)
    $path.AddArc($Size - $radius * 2, 0, $radius * 2, $radius * 2, 270, 90)
    $path.AddArc($Size - $radius * 2, $Size - $radius * 2, $radius * 2, $radius * 2, 0, 90)
    $path.AddArc(0, $Size - $radius * 2, $radius * 2, $radius * 2, 90, 90)
    $path.CloseFigure()
    $g.SetClip($path)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 37, 99, 235))
    $g.ResetClip()
    $g.FillPath([System.Drawing.Brushes]::Transparent, $path)
    # Redraw on transparent canvas clipped to rounded rect
    $g.SetClip($path)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 37, 99, 235))

    # "IT" text — white, bold, centered
    $fontSize = [float]([Math]::Max(5, $Size * 0.41))
    $font  = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $sf    = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect  = New-Object System.Drawing.RectangleF(0, 0, $Size, $Size)
    $g.DrawString('IT', $font, [System.Drawing.Brushes]::White, $rect, $sf)

    $g.ResetClip()
    $g.Dispose()
    $font.Dispose()
    return $bmp
}

$null = New-Item -ItemType Directory -Path 'assets' -Force
$tmpDir = Join-Path $env:TEMP 'it-icon-build'
$null = New-Item -ItemType Directory -Path $tmpDir -Force

$sizes = @(256, 128, 64, 48, 32, 16)
$pngPaths = @()

foreach ($s in $sizes) {
    $bmp  = New-ITBitmap $s
    $path = Join-Path $tmpDir "icon-$s.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngPaths += $path
    Write-Host "  Generated ${s}x${s} PNG"
}

# Combine into ICO using ImageMagick
$outIco = 'assets\icon.ico'
& magick @pngPaths $outIco
if ($LASTEXITCODE -ne 0) {
    Write-Error "ImageMagick failed. Is 'magick' on PATH? (choco install imagemagick.app)"
    exit 1
}

Remove-Item $tmpDir -Recurse -Force
Write-Host "Icon written to $outIco" -ForegroundColor Green
