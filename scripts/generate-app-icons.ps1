$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Drawing

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$assetsDir = Join-Path $repoRoot "src\TypingTrainer.App\Assets"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

function New-RoundedRectanglePath {
    param(
        [Parameter(Mandatory = $true)]
        [float]$X,

        [Parameter(Mandatory = $true)]
        [float]$Y,

        [Parameter(Mandatory = $true)]
        [float]$Width,

        [Parameter(Mandatory = $true)]
        [float]$Height,

        [Parameter(Mandatory = $true)]
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.StartFigure()
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()

    return $path
}

function Fill-RoundedRectangle {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Graphics]$Graphics,

        [Parameter(Mandatory = $true)]
        [System.Drawing.Brush]$Brush,

        [Parameter(Mandatory = $true)]
        [float]$X,

        [Parameter(Mandatory = $true)]
        [float]$Y,

        [Parameter(Mandatory = $true)]
        [float]$Width,

        [Parameter(Mandatory = $true)]
        [float]$Height,

        [Parameter(Mandatory = $true)]
        [float]$Radius
    )

    $path = New-RoundedRectanglePath -X $X -Y $Y -Width $Width -Height $Height -Radius $Radius
    try {
        $Graphics.FillPath($Brush, $path)
    }
    finally {
        $path.Dispose()
    }
}

function Draw-AppMark {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Graphics]$Graphics,

        [Parameter(Mandatory = $true)]
        [float]$X,

        [Parameter(Mandatory = $true)]
        [float]$Y,

        [Parameter(Mandatory = $true)]
        [float]$Size
    )

    $tileInset = $Size * 0.04
    $tileSize = $Size - ($tileInset * 2)
    $tileRadius = $Size * 0.18

    $tileRect = [System.Drawing.RectangleF]::new($X + $tileInset, $Y + $tileInset, $tileSize, $tileSize)
    $tileBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $tileRect,
        [System.Drawing.Color]::FromArgb(255, 13, 27, 54),
        [System.Drawing.Color]::FromArgb(255, 20, 111, 128),
        45.0
    )
    try {
        Fill-RoundedRectangle -Graphics $Graphics -Brush $tileBrush -X $tileRect.X -Y $tileRect.Y -Width $tileRect.Width -Height $tileRect.Height -Radius $tileRadius
    }
    finally {
        $tileBrush.Dispose()
    }

    $keyX = $X + ($Size * 0.22)
    $keyY = $Y + ($Size * 0.30)
    $keyW = $Size * 0.56
    $keyH = $Size * 0.42
    $keyRadius = $Size * 0.08

    $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(75, 0, 0, 0))
    try {
        Fill-RoundedRectangle -Graphics $Graphics -Brush $shadowBrush -X ($keyX + ($Size * 0.015)) -Y ($keyY + ($Size * 0.025)) -Width $keyW -Height $keyH -Radius $keyRadius
    }
    finally {
        $shadowBrush.Dispose()
    }

    $keyBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 245, 248, 252))
    try {
        Fill-RoundedRectangle -Graphics $Graphics -Brush $keyBrush -X $keyX -Y $keyY -Width $keyW -Height $keyH -Radius $keyRadius
    }
    finally {
        $keyBrush.Dispose()
    }

    $blueBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 16, 94, 181))
    $greenBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 40, 203, 140))
    try {
        $barH = [Math]::Max(1.0, $Size * 0.045)
        $stemW = [Math]::Max(1.0, $Size * 0.055)
        $textTop = $keyY + ($keyH * 0.30)
        $textLeft = $keyX + ($keyW * 0.24)
        $textW = $keyW * 0.30

        $Graphics.FillRectangle($blueBrush, $textLeft, $textTop, $textW, $barH)
        $Graphics.FillRectangle($blueBrush, $textLeft + (($textW - $stemW) / 2), $textTop, $stemW, $keyH * 0.38)

        $caretW = [Math]::Max(1.0, $Size * 0.045)
        $caretH = $keyH * 0.48
        $caretX = $keyX + ($keyW * 0.60)
        $caretY = $keyY + ($keyH * 0.26)
        $Graphics.FillRectangle($greenBrush, $caretX, $caretY, $caretW, $caretH)

        $progressH = [Math]::Max(1.0, $Size * 0.035)
        Fill-RoundedRectangle -Graphics $Graphics -Brush $greenBrush -X ($X + ($Size * 0.28)) -Y ($Y + ($Size * 0.79)) -Width ($Size * 0.44) -Height $progressH -Radius ($progressH / 2)
    }
    finally {
        $blueBrush.Dispose()
        $greenBrush.Dispose()
    }
}

function Draw-SmallAppMark {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Graphics]$Graphics,

        [Parameter(Mandatory = $true)]
        [float]$X,

        [Parameter(Mandatory = $true)]
        [float]$Y,

        [Parameter(Mandatory = $true)]
        [float]$Size
    )

    $tileInset = [Math]::Max(1.0, $Size * 0.08)
    $tileSize = $Size - ($tileInset * 2)
    $tileRadius = [Math]::Max(2.0, $Size * 0.16)
    $tileRect = [System.Drawing.RectangleF]::new($X + $tileInset, $Y + $tileInset, $tileSize, $tileSize)
    $tileBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 13, 67, 84))
    try {
        Fill-RoundedRectangle -Graphics $Graphics -Brush $tileBrush -X $tileRect.X -Y $tileRect.Y -Width $tileRect.Width -Height $tileRect.Height -Radius $tileRadius
    }
    finally {
        $tileBrush.Dispose()
    }

    $keyX = $X + ($Size * 0.23)
    $keyY = $Y + ($Size * 0.31)
    $keyW = $Size * 0.54
    $keyH = $Size * 0.38
    $keyRadius = [Math]::Max(1.0, $Size * 0.08)
    $keyBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 245, 248, 252))
    try {
        Fill-RoundedRectangle -Graphics $Graphics -Brush $keyBrush -X $keyX -Y $keyY -Width $keyW -Height $keyH -Radius $keyRadius
    }
    finally {
        $keyBrush.Dispose()
    }

    $blueBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 16, 94, 181))
    $greenBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 40, 203, 140))
    try {
        $barH = [Math]::Max(1.0, [Math]::Round($Size * 0.08))
        $stemW = [Math]::Max(1.0, [Math]::Round($Size * 0.09))
        $textTop = [Math]::Round($keyY + ($keyH * 0.28))
        $textLeft = [Math]::Round($keyX + ($keyW * 0.22))
        $textW = [Math]::Max(2.0, [Math]::Round($keyW * 0.34))

        $Graphics.FillRectangle($blueBrush, $textLeft, $textTop, $textW, $barH)
        $Graphics.FillRectangle($blueBrush, $textLeft + (($textW - $stemW) / 2), $textTop, $stemW, $keyH * 0.42)

        $caretW = [Math]::Max(1.0, [Math]::Round($Size * 0.09))
        $caretH = $keyH * 0.56
        $caretX = [Math]::Round($keyX + ($keyW * 0.62))
        $caretY = [Math]::Round($keyY + ($keyH * 0.22))
        $Graphics.FillRectangle($greenBrush, $caretX, $caretY, $caretW, $caretH)
    }
    finally {
        $blueBrush.Dispose()
        $greenBrush.Dispose()
    }
}

function Draw-AppMarkForIconSize {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Graphics]$Graphics,

        [Parameter(Mandatory = $true)]
        [float]$X,

        [Parameter(Mandatory = $true)]
        [float]$Y,

        [Parameter(Mandatory = $true)]
        [float]$Size
    )

    if ($Size -le 32) {
        Draw-SmallAppMark -Graphics $Graphics -X $X -Y $Y -Size $Size
        return
    }

    Draw-AppMark -Graphics $Graphics -X $X -Y $Y -Size $Size
}

function New-TransparentBitmap {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Width,

        [Parameter(Mandatory = $true)]
        [int]$Height
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $graphics.Clear([System.Drawing.Color]::Transparent)

    return [pscustomobject]@{
        Bitmap = $bitmap
        Graphics = $graphics
    }
}

function Save-Png {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Bitmap]$Bitmap,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function New-SquareLogo {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Size,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $surface = New-TransparentBitmap -Width $Size -Height $Size
    try {
        Draw-AppMark -Graphics $surface.Graphics -X 0 -Y 0 -Size $Size
        Save-Png -Bitmap $surface.Bitmap -Path $Path
    }
    finally {
        $surface.Graphics.Dispose()
        $surface.Bitmap.Dispose()
    }
}

function New-WideLogo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $surface = New-TransparentBitmap -Width 310 -Height 150
    try {
        Draw-AppMark -Graphics $surface.Graphics -X 104 -Y 24 -Size 102
        Save-Png -Bitmap $surface.Bitmap -Path $Path
    }
    finally {
        $surface.Graphics.Dispose()
        $surface.Bitmap.Dispose()
    }
}

function New-SplashScreen {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $surface = New-TransparentBitmap -Width 620 -Height 300
    try {
        Draw-AppMark -Graphics $surface.Graphics -X 236 -Y 76 -Size 148
        Save-Png -Bitmap $surface.Bitmap -Path $Path
    }
    finally {
        $surface.Graphics.Dispose()
        $surface.Bitmap.Dispose()
    }
}

function Convert-PngsToIco {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$PngBytesBySize,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $sizes = $PngBytesBySize.Keys | Sort-Object {[int]$_}
    $count = $sizes.Count
    $headerSize = 6
    $entrySize = 16
    $imageOffset = $headerSize + ($entrySize * $count)

    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)

    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$count)

        foreach ($size in $sizes) {
            $bytes = $PngBytesBySize[$size]
            $dimension = if ([int]$size -ge 256) { 0 } else { [byte][int]$size }

            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$bytes.Length)
            $writer.Write([UInt32]$imageOffset)

            $imageOffset += $bytes.Length
        }

        foreach ($size in $sizes) {
            $writer.Write([byte[]]$PngBytesBySize[$size])
        }

        [System.IO.File]::WriteAllBytes($Path, $stream.ToArray())
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

New-SquareLogo -Size 44 -Path (Join-Path $assetsDir "Square44x44Logo.png")
New-SquareLogo -Size 50 -Path (Join-Path $assetsDir "StoreLogo.png")
New-SquareLogo -Size 150 -Path (Join-Path $assetsDir "Square150x150Logo.png")
New-SquareLogo -Size 256 -Path (Join-Path $assetsDir "AppIcon.png")
New-WideLogo -Path (Join-Path $assetsDir "Wide310x150Logo.png")
New-SplashScreen -Path (Join-Path $assetsDir "SplashScreen.png")

$iconSizes = @(16, 24, 32, 48, 64, 128, 256)
$pngBytesBySize = @{}

foreach ($size in $iconSizes) {
    $surface = New-TransparentBitmap -Width $size -Height $size
    $memoryStream = [System.IO.MemoryStream]::new()

    try {
        Draw-AppMarkForIconSize -Graphics $surface.Graphics -X 0 -Y 0 -Size $size
        $surface.Bitmap.Save($memoryStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytesBySize[$size] = $memoryStream.ToArray()
    }
    finally {
        $memoryStream.Dispose()
        $surface.Graphics.Dispose()
        $surface.Bitmap.Dispose()
    }
}

Convert-PngsToIco -PngBytesBySize $pngBytesBySize -Path (Join-Path $assetsDir "AppIcon.ico")

Write-Host "Generated app icon assets in:"
Write-Host $assetsDir
