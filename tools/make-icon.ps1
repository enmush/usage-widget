# Draws the widget icon (three rising bars on a dark rounded square) and writes a multi-size .ico
# with PNG entries. Run: powershell -File tools/make-icon.ps1
param([string]$Out = "$PSScriptRoot\..\src\UsageWidget\app.ico")
Add-Type -AssemblyName System.Drawing

function Draw([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $r = [Math]::Max(2, [int]($s * 0.22)); $d = 2 * $r; $m = [Math]::Max(1, [int]($s / 32))
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($m, $m, $d, $d, 180, 90); $p.AddArc($s - $m - $d, $m, $d, $d, 270, 90)
    $p.AddArc($s - $m - $d, $s - $m - $d, $d, $d, 0, 90); $p.AddArc($m, $s - $m - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 32, 33, 36))), $p)
    $bar = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 92, 200, 255))
    $w = $s * 0.16; $base = $s * 0.80
    foreach ($i in 0..2) {
        $h = $s * (0.25 + 0.17 * $i)
        $g.FillRectangle($bar, [single]($s * (0.20 + 0.22 * $i)), [single]($base - $h), [single]$w, [single]$h)
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 256
$images = $sizes | ForEach-Object { ,(Draw $_) }
$fs = [System.IO.File]::Create($Out); $bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $bw.Write([byte]($s % 256)); $bw.Write([byte]($s % 256)); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Close()
"wrote $Out"
