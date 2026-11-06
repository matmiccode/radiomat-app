Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$root = Split-Path -Parent $PSScriptRoot
$dictionary = [System.Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $root 'Assets\Brand.xaml')))
$drawing = $dictionary['RadioMark']
$frames = @()
foreach ($size in @(16, 20, 24, 32, 48, 64, 128, 256)) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $context.DrawImage($drawing, (New-Object System.Windows.Rect(0, 0, $size, $size)))
    $context.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32))
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object IO.MemoryStream
    $encoder.Save($stream)
    $frames += [PSCustomObject]@{ Size = $size; Data = $stream.ToArray() }
    $stream.Dispose()
}
$file = [IO.File]::Create((Join-Path $root 'Assets\radioMAT.ico'))
$writer = New-Object IO.BinaryWriter($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Data.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Data.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Data) }
} finally { $writer.Dispose(); $file.Dispose() }
