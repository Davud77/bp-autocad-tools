# SPDX-License-Identifier: AGPL-3.0-only
# BotPlus command icons. One vector source, two theme variants (AutoCAD COLORTHEME 0 = dark, 1 = light).
#
# Drawing rules (keep every icon in the same family):
#   * 32 x 32 grid, content inside 2..30, integer coordinates, 2-unit round strokes.
#   * Body  - neutral outline: the object the command works on (sheet, table, text, geometry).
#   * Accent - blue: the action or the result (arrow, plus, selection, reset, number).
#   * Fill  - translucent blue: at most one highlighted area per icon.
# Output: Resources/Icons/<Theme>/<name>.png, 64 x 64 px at 192 dpi (32 x 32 DIP), transparent.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase
$root = Split-Path $PSScriptRoot -Parent

function Circle([double]$x, [double]$y, [double]$r) {
    "M$($x - $r),$y A$r,$r 0 1 1 $($x + $r),$y A$r,$r 0 1 1 $($x - $r),$y Z"
}
# Shared motifs, so that related commands read as one family.
$sheet  = 'M3,3 H29 V29 H3 Z'
$table  = 'M3,4 H19 V24 H3 Z M3,11 H19 M3,18 H19 M10,4 V24'
$reset  = 'M29,24 A5,5 0 1 1 24,19 M21,16 L24,19 21,22'
$sparkle = 'M26,2 V10 M22,6 H30'

$icons = [ordered]@{
    axon         = @{ Body = 'M16,3 L28,10 V23 L16,30 L4,23 V10 Z M4,10 L16,17 L28,10 M16,17 V30'; Fill = 'M16,3 L28,10 L16,17 L4,10 Z'; Accent = 'M16,3 L28,10 L16,17 L4,10 Z' }
    autonum      = @{ Body = 'M14,7 H29 M14,16 H29 M14,25 H22'; Accent = 'M3,4 H9 V10 H3 Z M3,13 H9 V19 H3 Z M3,22 H9 V28 H3 Z M26,21 V29 M23,26 L26,29 29,26'; Fill = 'M3,4 H9 V10 H3 Z' }
    selectsim    = @{ Body = 'M4,28 L8,20 L12,28 Z ' + (Circle 24 24 4); Accent = 'M4,4 H12 V12 H4 Z M20,4 H28 V12 H20 Z'; Fill = 'M4,4 H12 V12 H4 Z' }
    sumlen       = @{ Body = 'M21,5 H5 L14,16 L5,27 H21'; Accent = 'M26,5 V27 M23,8 L26,5 L29,8 M23,24 L26,27 L29,24' }
    multicopy    = @{ Body = 'M3,20 H11 V28 H3 Z'; Accent = 'M12,11 H20 V19 H12 Z M21,3 H29 V11 H21 Z'; Fill = 'M21,3 H29 V11 H21 Z' }
    numberedcopy = @{ Body = 'M3,4 H15 V14 H3 Z M6,9 H12 M9,17 V23 H13 M11,21 L13,23 L11,25'; Accent = 'M16,18 H29 V29 H16 Z M20,22 L22,21 V26 M20,26 H24'; Fill = 'M16,18 H29 V29 H16 Z' }
    planprep     = @{ Body = 'M3,7 H22 V29 H3 Z M3,18 H10 M15,18 H22 M12,7 V13'; Accent = $sparkle }
    additional   = @{ Body = 'M4,4 H12 V12 H4 Z M20,4 H28 V12 H20 Z M4,20 H12 V28 H4 Z'; Accent = 'M20,24 H28 M24,20 V28' }
    symbols      = @{ Body = (Circle 9 9 6) + ' M9,15 V29 M18,4 H29 M18,9 H25'; Accent = 'M16,27 H30 M19,19 H27 L23,27 Z'; Fill = 'M19,19 H27 L23,27 Z' }
    section      = @{ Body = 'M12,10 H15 M18,10 H21 M4,24 H28 V29 H4 Z'; Accent = 'M3,10 H9 M23,10 H29 M6,10 V20 M3,17 L6,20 L9,17 M26,10 V20 M23,17 L26,20 L29,17' }
    break        = @{ Body = 'M3,6 H29 M3,26 H29'; Accent = 'M16,2 V10 L12,14 L20,18 L16,22 V30' }
    tableedit    = @{ Body = $table; Accent = 'M17,29 V25 L26,16 L30,20 L21,29 Z M23,19 L27,23'; Fill = 'M17,29 V25 L26,16 L30,20 L21,29 Z' }
    excel_export = @{ Body = 'M3,4 H17 V28 H3 Z M3,12 H17 M3,20 H17 M10,4 V28'; Accent = 'M20,16 H29 M25,12 L29,16 L25,20' }
    excel_import = @{ Body = 'M15,4 H29 V28 H15 Z M15,12 H29 M15,20 H29 M22,4 V28'; Accent = 'M3,16 H12 M8,12 L12,16 L8,20' }
    table_reset  = @{ Body = $table; Accent = $reset }
    clean        = @{ Body = 'M3,22 L14,11 L21,18 L10,29 Z M14,29 H29'; Accent = 'M7,18 L14,25'; Fill = 'M3,22 L7,18 L14,25 L10,29 Z' }
    clean_invert = @{ Body = 'M3,3 L8,8 M8,3 L3,8 M24,3 L29,8 M29,3 L24,8 M3,24 L8,29 M8,24 L3,29 M24,24 L29,29 M29,24 L24,29'; Accent = 'M11,11 H21 V21 H11 Z'; Fill = 'M11,11 H21 V21 H11 Z' }
    srtimtext    = @{ Body = 'M4,5 H20 M12,5 V27 M8,27 H16'; Accent = $reset }
    stamp        = @{ Body = $sheet; Accent = 'M13,20 H29 M13,20 V29 M13,25 H29 M21,20 V29'; Fill = 'M13,20 H29 V29 H13 Z' }
    frame        = @{ Body = 'M3,8 H23 V29 H3 Z M6,11 H20 V26 H6 Z'; Accent = $sparkle }
    wplot        = @{ Body = 'M9,24 H3 V12 H29 V24 H23 M9,12 V4 H23 V12'; Accent = 'M9,19 H23 V29 H9 Z M12,23 H20 M12,26 H17'; Fill = 'M9,19 H23 V29 H9 Z' }
    dwgexport    = @{ Body = 'M5,3 H16 L22,9 V29 H5 Z M16,3 V9 H22'; Accent = 'M14,21 H29 M25,17 L29,21 L25,25' }
    aliases      = @{ Body = 'M3,8 H29 V24 H3 Z M7,13 H9 M12,13 H14 M17,13 H19 M22,13 H25'; Accent = 'M9,19 H23' }
    info         = @{ Body = (Circle 16 16 13); Accent = 'M16,14 V23 M16,9 V9.5' }
}

$themes = [ordered]@{
    Dark  = @{ Body = '#E1E6EB'; Accent = '#3BB4F2'; Fill = '#553BB4F2' }
    Light = @{ Body = '#3D4752'; Accent = '#0A84C6'; Fill = '#400A84C6' }
}

$converter = [Windows.Media.BrushConverter]::new()
foreach ($theme in $themes.GetEnumerator()) {
    $directory = Join-Path $root "Resources/Icons/$($theme.Key)"
    New-Item -ItemType Directory -Force $directory | Out-Null
    $pens = @{}
    foreach ($layer in 'Body', 'Accent') {
        $pen = [Windows.Media.Pen]::new($converter.ConvertFromString($theme.Value[$layer]), 2)
        $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
        $pen.Freeze(); $pens[$layer] = $pen
    }
    $fill = $converter.ConvertFromString($theme.Value.Fill)
    foreach ($icon in $icons.GetEnumerator()) {
        $visual = [Windows.Media.DrawingVisual]::new()
        $context = $visual.RenderOpen()
        if ($icon.Value.Fill) { $context.DrawGeometry($fill, $null, [Windows.Media.Geometry]::Parse($icon.Value.Fill)) }
        $context.DrawGeometry($null, $pens.Body, [Windows.Media.Geometry]::Parse($icon.Value.Body))
        if ($icon.Value.Accent) { $context.DrawGeometry($null, $pens.Accent, [Windows.Media.Geometry]::Parse($icon.Value.Accent)) }
        $context.Close()
        $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(64, 64, 192, 192, [Windows.Media.PixelFormats]::Pbgra32)
        $bitmap.Render($visual)
        $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream = [IO.File]::Create((Join-Path $directory ($icon.Key + '.png')))
        try { $encoder.Save($stream) } finally { $stream.Dispose() }
    }
}
Write-Output "Generated $($icons.Count) icons x $($themes.Count) themes."
