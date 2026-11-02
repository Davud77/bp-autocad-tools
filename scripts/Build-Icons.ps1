# SPDX-License-Identifier: AGPL-3.0-only
# Original BotPlus line icons. Edit these vector paths, then regenerate PNG resources.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$root = Split-Path $PSScriptRoot -Parent
$icons = [ordered]@{
    additional = @('M4,4 H12 V12 H4 Z M20,4 H28 V12 H20 Z M4,20 H12 V28 H4 Z','M20,24 H28 M24,20 V28')
    excel_export = @('M4,4 H23 V11 M4,4 V28 H23 V23 M4,12 H14 M4,20 H14 M11,4 V28','M17,17 H29 M24,12 L29,17 24,22')
    excel_import = @('M4,4 H23 V11 M4,4 V28 H23 V23 M4,12 H14 M4,20 H14 M11,4 V28','M29,17 H17 M22,12 L17,17 22,22')
    table_reset = @('M4,4 H28 V28 H4 Z M4,12 H28 M4,20 H12 M12,4 V28 M20,4 V12','M19,18 A6,6 0 1 1 17,24 M16,17 L20,17 20,21')
    frame = @('M3,5 H29 V27 H3 Z M6,8 H26 V24 H6 Z M17,19 H26 M17,19 V24 M21,19 V24','M3,2 V8 M0,5 H6 M26,27 H32 M29,24 V30')
    numberedcopy = @('M3,3 H16 V18 H3 Z M8,8 H21 V23 H8 M13,13 H28 V29 H13 Z','M18,19 L21,17 V25 M18,25 H24')
    symbols = @('M3,16 H13 M8,11 V21 M20,4 H29 V13 H20 Z M24,20 A5,5 0 1 1 23.99,20','M4,5 L7,2 10,5 M7,2 V8 M3,28 L9,22 15,28')
    section = @('M3,9 H10 M22,9 H29 M8,9 V25 M24,9 V25 M12,9 H20','M4,20 L8,25 12,20 M20,20 L24,25 28,20 M12,4 L16,1 20,4')
    break = @('M2,9 H12 L15,4 18,14 21,9 H30 M2,24 H11 M23,24 H30','M11,24 C13,14 17,14 19,24 S25,34 27,24')
    aliases = @('M3,5 H29 V27 H3 Z M7,10 H10 M14,10 H17 M21,10 H24 M7,16 H10 M14,16 H17 M21,16 H24','M7,22 H17 M22,20 L25,22 22,24')
}
$white = [Windows.Media.BrushConverter]::new().ConvertFromString('#DCE8F2')
$blue = [Windows.Media.BrushConverter]::new().ConvertFromString('#22A7DD')
foreach ($entry in $icons.GetEnumerator()) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    for ($i = 0; $i -lt $entry.Value.Count; $i++) {
        $brush = if ($i -eq 0) { $white } else { $blue }
        $pen = [Windows.Media.Pen]::new($brush, 1.8)
        $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
        $context.DrawGeometry($null, $pen, [Windows.Media.Geometry]::Parse($entry.Value[$i]))
    }
    $context.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(64,64,192,192,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $root ('Resources/' + $entry.Key + '.png')))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
Write-Output "Generated $($icons.Count) original icons (64 px, transparent)."
