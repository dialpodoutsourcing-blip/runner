$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'assets\GhostUserRunner.ico'
$bitmap = [System.Drawing.Bitmap]::new(256, 256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(9, 18, 37))
$cyan = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(116, 233, 255))
$dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(9, 18, 37))
$graphics.FillEllipse($cyan, 52, 34, 152, 152)
$graphics.FillRectangle($cyan, 52, 108, 152, 78)
$points = [System.Drawing.Point[]]@([System.Drawing.Point]::new(52,186),[System.Drawing.Point]::new(78,168),[System.Drawing.Point]::new(103,190),[System.Drawing.Point]::new(128,168),[System.Drawing.Point]::new(153,190),[System.Drawing.Point]::new(178,168),[System.Drawing.Point]::new(204,186))
$graphics.FillPolygon($cyan, $points)
$graphics.FillEllipse($dark, 91, 94, 20, 24)
$graphics.FillEllipse($dark, 146, 94, 20, 24)
$pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(9,18,37), 9)
$pen.StartCap = $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawArc($pen, 105, 124, 50, 35, 20, 140)
$handle = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($handle)
$stream = [System.IO.File]::Create($output)
try { $icon.Save($stream) } finally { $stream.Dispose(); $icon.Dispose(); $pen.Dispose(); $cyan.Dispose(); $dark.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
Write-Host "Generated $output"
