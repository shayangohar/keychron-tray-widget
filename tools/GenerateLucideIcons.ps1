param(
    [string]$SourceDirectory = "$PSScriptRoot\..\assets\lucide",
    [string]$OutputDirectory = "$PSScriptRoot\..\src\KeychronK8BatteryTray\icons"
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$iconNames = @(
    'battery-full',
    'battery-medium',
    'battery-low',
    'battery-warning',
    'battery-charging',
    'unplug'
)

$viewBoxSize = 24
$iconSizes = @(16, 24, 32, 48, 64)

[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

function ConvertTo-IcoImage([xml]$svg, [int]$size) {
    $scale = $size / $viewBoxSize
    $pen = [System.Windows.Media.Pen]::new([System.Windows.Media.Brushes]::White, 2 * $scale)
    $pen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $pen.LineJoin = [System.Windows.Media.PenLineJoin]::Round

    $visual = [System.Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    $drawing.PushTransform([System.Windows.Media.ScaleTransform]::new($scale, $scale))

    foreach ($element in $svg.svg.ChildNodes) {
        switch ($element.LocalName) {
            'path' {
                $geometry = [System.Windows.Media.Geometry]::Parse($element.d)
                $drawing.DrawGeometry($null, $pen, $geometry)
            }
            'rect' {
                $rect = [System.Windows.Rect]::new(
                    [double]$element.x,
                    [double]$element.y,
                    [double]$element.width,
                    [double]$element.height)
                $radius = [double]$element.rx
                $geometry = [System.Windows.Media.RectangleGeometry]::new($rect, $radius, $radius)
                $drawing.DrawGeometry($null, $pen, $geometry)
            }
        }
    }

    $drawing.Pop()
    $drawing.Close()

    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
        $size,
        $size,
        96,
        96,
        [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)

    $stride = $size * 4
    $pixels = [byte[]]::new($stride * $size)
    $bitmap.CopyPixels($pixels, $stride, 0)

    $maskStride = [int]([math]::Ceiling($size / 32) * 4)
    $mask = [byte[]]::new($maskStride * $size)
    for ($y = 0; $y -lt $size; $y++) {
        for ($x = 0; $x -lt $size; $x++) {
            $alpha = $pixels[($y * $stride) + ($x * 4) + 3]
            if ($alpha -eq 0) {
                $maskIndex = (($size - 1 - $y) * $maskStride) + [int][math]::Floor($x / 8)
                $mask[$maskIndex] = $mask[$maskIndex] -bor (1 -shl (7 - ($x % 8)))
            }
        }
    }

    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([System.UInt32]40)
        $writer.Write([System.Int32]$size)
        $writer.Write([System.Int32]($size * 2))
        $writer.Write([System.UInt16]1)
        $writer.Write([System.UInt16]32)
        $writer.Write([System.UInt32]0)
        $writer.Write([System.UInt32]$pixels.Length)
        $writer.Write([System.Int32]0)
        $writer.Write([System.Int32]0)
        $writer.Write([System.UInt32]0)
        $writer.Write([System.UInt32]0)

        for ($y = $size - 1; $y -ge 0; $y--) {
            $rowStart = $y * $stride
            $writer.Write($pixels, $rowStart, $stride)
        }
        $writer.Write($mask)
        return ,$stream.ToArray()
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function Write-Ico([string]$path, [object[]]$images) {
    $headerSize = 6
    $directoryEntrySize = 16
    $offset = $headerSize + ($directoryEntrySize * $images.Count)
    $stream = [System.IO.File]::Create($path)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([System.UInt16]0)
        $writer.Write([System.UInt16]1)
        $writer.Write([System.UInt16]$images.Count)

        foreach ($image in $images) {
            $dimension = if ($image.Size -ge 256) { [byte]0 } else { [byte]$image.Size }
            $writer.Write($dimension)
            $writer.Write($dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([System.UInt16]1)
            $writer.Write([System.UInt16]32)
            $writer.Write([System.UInt32]$image.Bytes.Length)
            $writer.Write([System.UInt32]$offset)
            $offset += $image.Bytes.Length
        }

        foreach ($image in $images) {
            $writer.Write($image.Bytes)
        }
    }
    finally {
        $writer.Dispose()
    }
}

foreach ($iconName in $iconNames) {
    $sourcePath = Join-Path $SourceDirectory "$iconName.svg"
    $outputPath = Join-Path $OutputDirectory "$iconName.ico"
    [xml]$svg = Get-Content -Raw $sourcePath

    $images = @(
        foreach ($size in $iconSizes) {
            [pscustomobject]@{
                Size = $size
                Bytes = ConvertTo-IcoImage $svg $size
            }
        }
    )
    Write-Ico $outputPath $images
}
