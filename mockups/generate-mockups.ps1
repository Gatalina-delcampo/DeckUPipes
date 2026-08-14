Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$scale = 2
$W = 960 * $scale
$H = 800 * $scale

function Lerp([int]$a, [int]$b, [double]$t) { [int][math]::Round($a + ($b - $a) * $t) }
function Mix([System.Drawing.Color]$a, [System.Drawing.Color]$b, [double]$t) {
    [System.Drawing.Color]::FromArgb(255, (Lerp $a.R $b.R $t), (Lerp $a.G $b.G $t), (Lerp $a.B $b.B $t))
}
function RoundedRect([double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

$accent = [System.Drawing.Color]::FromArgb(255, 78, 201, 176)
$panel = [System.Drawing.Color]::FromArgb(255, 28, 28, 34)
$panelBorder = [System.Drawing.Color]::FromArgb(70, 255, 255, 255)
$boxBg = [System.Drawing.Color]::FromArgb(255, 44, 46, 56)
$boxBorder = [System.Drawing.Color]::FromArgb(40, 255, 255, 255)
$track = [System.Drawing.Color]::FromArgb(255, 47, 51, 61)
$text = [System.Drawing.Color]::FromArgb(255, 232, 232, 236)
$dimText = [System.Drawing.Color]::FromArgb(255, 154, 162, 173)
$white = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$peak = [System.Drawing.Color]::FromArgb(120, 255, 255, 255)

function New-Bitmap { [System.Drawing.Bitmap]::new($W, $H) }

function Draw-Desktop($g) {
    $rect = [System.Drawing.Rectangle]::new(0, 0, $W, $H)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(255, 38, 41, 50), [System.Drawing.Color]::FromArgb(255, 26, 28, 34), 90)
    $g.FillRectangle($grad, $rect)
    $grad.Dispose()

    # fake desktop windows
    $winFill = [System.Drawing.Color]::FromArgb(255, 44, 48, 58)
    $winBorder = [System.Drawing.Color]::FromArgb(255, 58, 63, 74)
    $title = [System.Drawing.Color]::FromArgb(255, 52, 57, 68)
    foreach ($wr in @(@(150, 90, 520, 340), @(700, 160, 420, 300), @(320, 480, 480, 220))) {
        $x = $wr[0] * $scale; $y = $wr[1] * $scale; $w = $wr[2] * $scale; $h = $wr[3] * $scale
        $p = RoundedRect $x $y $w $h 8
        $g.FillPath([System.Drawing.SolidBrush]$winFill, $p)
        $g.DrawPath([System.Drawing.Pen]::new($winBorder, 1 * $scale), $p)
        $p.Dispose()
        $p2 = RoundedRect $x $y $w (24 * $scale) 8
        $g.FillPath([System.Drawing.SolidBrush]$title, $p2)
        $p2.Dispose()
        $bar = RoundedRect ($x + 18 * $scale) ($y + 44 * $scale) ($w - 36 * $scale) (12 * $scale) 4
        $g.FillPath([System.Drawing.SolidBrush]$winBorder, $bar)
        $bar.Dispose()
    }

    # fake taskbar at bottom
    $tb = [System.Drawing.Rectangle]::new(0, ($H - 44 * $scale), $W, 44 * $scale)
    $g.FillRectangle([System.Drawing.SolidBrush][System.Drawing.Color]::FromArgb(255, 30, 31, 36), $tb)
    for ($i = 0; $i -lt 4; $i++) {
        $g.FillRectangle([System.Drawing.SolidBrush][System.Drawing.Color]::FromArgb(255, 58, 60, 68), (120 + $i * 56) * $scale, ($H - 30 * $scale), 36 * $scale, 20 * $scale)
    }
}

function Draw-VolumeBar($g, $x, $y, $w, $pct, $fillBrush, $showPeak) {
    $trackP = RoundedRect $x $y $w (8 * $scale) 4
    $g.FillPath([System.Drawing.SolidBrush]$track, $trackP)
    $trackP.Dispose()
    $fillW = $w * $pct
    if ($fillW -gt 4 * $scale) {
        $fillP = RoundedRect $x $y $fillW (8 * $scale) 4
        $g.FillPath($fillBrush, $fillP)
        $fillP.Dispose()
    }
    if ($showPeak) {
        $peakW = $w * 0.62
        $peakP = RoundedRect $x $y $peakW (8 * $scale) 4
        $g.FillPath([System.Drawing.SolidBrush]$peak, $peakP)
        $peakP.Dispose()
    }
}

function Draw-IconBox($g, $x, $y, $size, $letter, $bgColor) {
    $p = RoundedRect $x $y $size $size 7
    $g.FillPath([System.Drawing.SolidBrush]$bgColor, $p)
    $p.Dispose()
    $font = [System.Drawing.Font]::new('Segoe UI', 10.5 * $scale, [System.Drawing.FontStyle]::Bold)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $g.DrawString($letter, $font, [System.Drawing.SolidBrush]$white, ([System.Drawing.RectangleF]::new($x, $y, $size, $size)), $sf)
    $sf.Dispose(); $font.Dispose()
}

function Draw-TabChip($g, $x, $y) {
    $font = [System.Drawing.Font]::new('Segoe UI', 7.5 * $scale, [System.Drawing.FontStyle]::Bold)
    $w = 30 * $scale; $h = 16 * $scale
    $p = RoundedRect $x $y $w $h 4
    $g.FillPath([System.Drawing.SolidBrush]$accent, $p)
    $p.Dispose()
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $g.DrawString('TAB', $font, [System.Drawing.SolidBrush][System.Drawing.Color]::FromArgb(255, 12, 28, 25), ([System.Drawing.RectangleF]::new($x, $y, $w, $h)), $sf)
    $sf.Dispose(); $font.Dispose()
}

function Draw-SystemRow($g, $x, $y, $pct) {
    $fontLabel = [System.Drawing.Font]::new('Segoe UI', 10 * $scale, [System.Drawing.FontStyle]::Bold)
    $fontPct = [System.Drawing.Font]::new('Segoe UI', 11 * $scale, [System.Drawing.FontStyle]::Regular)
    $g.DrawString('SYSTEM', $fontLabel, [System.Drawing.SolidBrush]$dimText, ($x * 1.0), ($y + 8 * $scale))
    $barX = $x + 64 * $scale
    Draw-VolumeBar $g $barX $y (110 * $scale) $pct $accentBrush $true
    $g.DrawString("$([int][math]::Round($pct * 100))%", $fontPct, [System.Drawing.SolidBrush]$dimText, ($x + 250 * $scale), ($y + 8 * $scale))
    $fontLabel.Dispose(); $fontPct.Dispose()
}

function Draw-AppRow($g, $x, $y, $w, $letter, $iconColor, $name, $pct, $focused, $showTab) {
    $boxP = RoundedRect $x $y $w (46 * $scale) 9
    $borderPen = [System.Drawing.Pen]::new($boxBorder, 1 * $scale)
    $g.FillPath([System.Drawing.SolidBrush]$boxBg, $boxP)
    $g.DrawPath($borderPen, $boxP)
    $boxP.Dispose(); $borderPen.Dispose()

    if ($focused) {
        $focusP = RoundedRect ($x + 1) ($y + 1) ($w - 2 * $scale) (44 * $scale) 8
        $g.DrawPath([System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(150, 78, 201, 176), 1.5 * $scale), $focusP)
        $focusP.Dispose()
    }

    Draw-IconBox $g ($x + 10 * $scale) ($y + 9 * $scale) (28 * $scale) $letter $iconColor

    $nameFont = [System.Drawing.Font]::new('Segoe UI', 13 * $scale, [System.Drawing.FontStyle]::Regular)
    $nameBrush = [System.Drawing.SolidBrush]::new($text)
    $g.DrawString($name, $nameFont, $nameBrush, ($x + 50 * $scale), ($y + 12 * $scale))
    $nameFont.Dispose(); $nameBrush.Dispose()

    $barX = $x + 240 * $scale; $barY = $y + 14 * $scale
    Draw-VolumeBar $g $barX $barY (110 * $scale) $pct $accentBrush $false

    $pctFont = [System.Drawing.Font]::new('Segoe UI', 11 * $scale, [System.Drawing.FontStyle]::Regular)
    $g.DrawString("$([int][math]::Round($pct * 100))%", $pctFont, [System.Drawing.SolidBrush]$dimText, ($x + 358 * $scale), ($y + 14 * $scale))
    $pctFont.Dispose()

    if ($showTab) {
        Draw-TabChip $g ($x + $w - 38 * $scale) ($y + 15 * $scale)
    }
}

function Render-DimRow($g, $x, $y, $w, $letter, $iconColor, $name, $pct, $showTab) {
    $bmp = [System.Drawing.Bitmap]::new([int]$w, [int](46 * $scale))
    $tg = [System.Drawing.Graphics]::FromImage($bmp)
    $tg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $tg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    Draw-AppRow $tg 0 0 $w $letter $iconColor $name $pct $false $showTab
    $cm = New-Object System.Drawing.Imaging.ColorMatrix
    $cm.Matrix33 = 0.55
    $attr = New-Object System.Drawing.Imaging.ImageAttributes
    $attr.SetColorMatrix($cm)
    $g.DrawImage($bmp, ([System.Drawing.Rectangle]::new([int]$x, [int]$y, [int]$w, [int](46 * $scale))), 0, 0, $bmp.Width, $bmp.Height, [System.Drawing.GraphicsUnit]::Pixel, $attr)
    $attr.Dispose(); $tg.Dispose(); $bmp.Dispose()
}

function Draw-Header($g, $x, $y, $w) {
    $font = [System.Drawing.Font]::new('Segoe UI', 11 * $scale, [System.Drawing.FontStyle]::Bold)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $g.DrawString('EarClarinet', $font, [System.Drawing.SolidBrush]$accent, ([System.Drawing.RectangleF]::new($x, $y, $w, 18 * $scale)), $sf)
    $sf.Dispose(); $font.Dispose()
}

function Draw-SystemPanel($g, $x, $y, $w, $pct) {
    $h = 88 * $scale
    $p = RoundedRect $x $y $w $h 14
    $g.FillPath([System.Drawing.SolidBrush]$panel, $p)
    $g.DrawPath([System.Drawing.Pen]::new($panelBorder, 1 * $scale), $p)
    $p.Dispose()
    Draw-Header $g ($x + 12 * $scale) ($y + 8 * $scale) ($w - 24 * $scale)
    Draw-SystemRow $g ($x + 24 * $scale) ($y + 40 * $scale) $pct
}

$accentBrush = [System.Drawing.SolidBrush]::new($accent)

# ============ VARIANT 1: apps BELOW the system panel, floating, centered ============
$bmp = New-Bitmap
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
Draw-Desktop $g
$sx = 48 * $scale; $sy = 64 * $scale; $pw = 320 * $scale
Draw-SystemPanel $g $sx $sy $pw 0.12
$ay = $sy + 88 * $scale + 12 * $scale

Render-DimRow $g $sx $ay $pw 'S' ([System.Drawing.Color]::FromArgb(255, 29, 185, 84)) 'Spotify' 0.6 $false
$ay += 46 * $scale + 8 * $scale
Draw-AppRow $g $sx $ay $pw 'C' ([System.Drawing.Color]::FromArgb(255, 66, 133, 244)) 'Google Chrome' 1.0 $true $false
$ay += 46 * $scale + 8 * $scale
Render-DimRow $g $sx $ay $pw 'G' ([System.Drawing.Color]::FromArgb(255, 86, 119, 252)) 'Steam' 0.35 $true
$g.Dispose()
$bmp.Save('C:\EarClarinet\mockups\v1-apps-abajo.png')
$bmp.Dispose()

# ============ VARIANT 2: apps ABOVE the system panel ============
$bmp = New-Bitmap
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
Draw-Desktop $g
$ay = 64 * $scale
Render-DimRow $g $sx $ay $pw 'S' ([System.Drawing.Color]::FromArgb(255, 29, 185, 84)) 'Spotify' 0.6 $false
$ay += 46 * $scale + 8 * $scale
Draw-AppRow $g $sx $ay $pw 'C' ([System.Drawing.Color]::FromArgb(255, 66, 133, 244)) 'Google Chrome' 1.0 $true $false
$ay += 46 * $scale + 8 * $scale
Render-DimRow $g $sx $ay $pw 'G' ([System.Drawing.Color]::FromArgb(255, 86, 119, 252)) 'Steam' 0.35 $true
$sy2 = $ay + 46 * $scale + 12 * $scale
Draw-SystemPanel $g $sx $sy2 $pw 0.12
$g.Dispose()
$bmp.Save('C:\EarClarinet\mockups\v2-apps-arriba.png')
$bmp.Dispose()

# ============ VARIANT 3: contiguous stack, same width, separate boxes ============
$bmp = New-Bitmap
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
Draw-Desktop $g
$y = 64 * $scale
$p = RoundedRect $sx $y $pw (88 * $scale) 14
$g.FillPath([System.Drawing.SolidBrush]$panel, $p)
$g.DrawPath([System.Drawing.Pen]::new($panelBorder, 1 * $scale), $p)
$p.Dispose()
Draw-Header $g ($sx + 12 * $scale) ($y + 8 * $scale) ($pw - 24 * $scale)
Draw-SystemRow $g ($sx + 24 * $scale) ($y + 40 * $scale) 110 * $scale 0.12 $false
$y += 88 * $scale + 4 * $scale
Render-DimRow $g $sx $y $pw 'S' ([System.Drawing.Color]::FromArgb(255, 29, 185, 84)) 'Spotify' 0.6 $false
$y += 46 * $scale + 4 * $scale
Draw-AppRow $g $sx $y $pw 'C' ([System.Drawing.Color]::FromArgb(255, 66, 133, 244)) 'Google Chrome' 1.0 $true $false
$y += 46 * $scale + 4 * $scale
Render-DimRow $g $sx $y $pw 'G' ([System.Drawing.Color]::FromArgb(255, 86, 119, 252)) 'Steam' 0.35 $true
$g.Dispose()
$bmp.Save('C:\EarClarinet\mockups\v3-contiguo.png')
$bmp.Dispose()

$accentBrush.Dispose()
Write-Output 'mockups generated'
