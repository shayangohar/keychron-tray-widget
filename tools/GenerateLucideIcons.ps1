param(
    [string]$SourceDirectory = "$PSScriptRoot\..\assets\lucide",
    [string]$OutputDirectory = "$PSScriptRoot\..\src\KeychronK8BatteryTray\icons"
)

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class NativeIconMethods
{
    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr handle);
}
'@

$iconNames = @(
    'battery-full',
    'battery-medium',
    'battery-low',
    'battery-warning',
    'battery-charging',
    'unplug'
)

$iconSize = 16
$viewBoxSize = 24
$scale = $iconSize / $viewBoxSize
$pen = [System.Windows.Media.Pen]::new([System.Windows.Media.Brushes]::White, 2)
$pen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
$pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
$pen.LineJoin = [System.Windows.Media.PenLineJoin]::Round

[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

foreach ($iconName in $iconNames) {
    $sourcePath = Join-Path $SourceDirectory "$iconName.svg"
    $outputPath = Join-Path $OutputDirectory "$iconName.ico"
    [xml]$svg = Get-Content -Raw $sourcePath

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
        $iconSize,
        $iconSize,
        96,
        96,
        [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)

    $pngEncoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $pngEncoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $pngStream = [System.IO.MemoryStream]::new()
    $pngEncoder.Save($pngStream)
    $pngStream.Position = 0

    $drawingBitmap = [System.Drawing.Bitmap]::new($pngStream)
    $handle = $drawingBitmap.GetHicon()
    try {
        $icon = [System.Drawing.Icon]::FromHandle($handle)
        try {
            $outputStream = [System.IO.File]::Create($outputPath)
            try {
                $icon.Save($outputStream)
            }
            finally {
                $outputStream.Dispose()
            }
        }
        finally {
            $icon.Dispose()
        }
    }
    finally {
        [NativeIconMethods]::DestroyIcon($handle) | Out-Null
        $drawingBitmap.Dispose()
        $pngStream.Dispose()
    }
}
