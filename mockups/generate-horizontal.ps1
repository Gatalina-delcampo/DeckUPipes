Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

# All public functions take LOGICAL units; conversion happens in primitives.
$scale = 1.5
$W = [int](1920 * $scale)
$H = [int](1080 * $scale)

function PX([double]$v) { [int][math]::Round($v * $scale) }

function New-Canvas {
    $bmp = [System.Drawing.Bitmap]::new($W, $H)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    return @($bmp, $g)
}

function Close-Canvas($bmp, $g, $path) {
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function RRect($g, [double]$x, [double]$y, [double]$w, [double]$h, [double]$r, $brush, $pen) {
    $px = PX $x; $py = PX $y; $pw = PX $w; $ph = PX $h; $pr = PX $r
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $pr * 2
    $p.AddArc($px, $py, $d, $d, 180, 90)
    $p.AddArc($px + $pw - $d, $py, $d, $d, 270, 90)
    $p.AddArc($px + $pw - $d, $py + $ph - $d, $d, $d, 0, 90)
    $p.AddArc($px, $py + $ph - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    if ($brush) { $g.FillPath($brush, $p) }
    if ($pen) { $g.DrawPath($pen, $p) }
    $p.Dispose()
}

function Text($g, [double]$x, [double]$y, [double]$w, $font, $brush, $s, $align = 'Left') {
    $sf = New-Object System.Drawing.StringFormat
    if ($align -eq 'Center') { $sf.Alignment = 'Center' }
    $sf.LineAlignment = 'Center'
    $g.DrawString($s, $font, $brush, ([System.Drawing.RectangleF]::new((PX $x), (PX $y), (PX $w), (PX 20))), $sf)
    $sf.Dispose()
}

# ---------- palette (flat, unambiguous) ----------
$accent     = [System.Drawing.Color]::FromArgb(255, 245, 96, 90)
$panel      = [System.Drawing.Color]::FromArgb(255, 28, 28, 34)
$panelBrd   = [System.Drawing.Color]::FromArgb(70, 255, 255, 255)
$boxBg      = [System.Drawing.Color]::FromArgb(255, 44, 46, 56)
$boxBrd     = [System.Drawing.Color]::FromArgb(40, 255, 255, 255)
$track      = [System.Drawing.Color]::FromArgb(255, 51, 56, 68)
$text       = [System.Drawing.Color]::FromArgb(255, 232, 232, 236)
$dimText    = [System.Drawing.Color]::FromArgb(255, 154, 162, 173)
$hintCol    = [System.Drawing.Color]::FromArgb(255, 62, 62, 74)
$chipBrd    = [System.Drawing.Color]::FromArgb(120, 255, 255, 255)
$chipBg     = [System.Drawing.Color]::FromArgb(70, 255, 255, 255)
$darkOnAccent = [System.Drawing.Color]::FromArgb(255, 28, 14, 13)

$brushAccent = [System.Drawing.SolidBrush]::new($accent)
$brushPanel  = [System.Drawing.SolidBrush]::new($panel)
$brushBox    = [System.Drawing.SolidBrush]::new($boxBg)
$brushText   = [System.Drawing.SolidBrush]::new($text)
$brushDim    = [System.Drawing.SolidBrush]::new($dimText)
$brushHint   = [System.Drawing.SolidBrush]::new($hintCol)
$brushChip   = [System.Drawing.SolidBrush]::new($chipBg)
$brushTrack  = [System.Drawing.SolidBrush]::new($track)
$brushDark   = [System.Drawing.SolidBrush]::new($darkOnAccent)
$penPanel    = [System.Drawing.Pen]::new($panelBrd, 1)
$penBox      = [System.Drawing.Pen]::new($boxBrd, 1)
$penChip     = [System.Drawing.Pen]::new($chipBrd, 1)
$penFocus    = [System.Drawing.Pen]::new($accent, 1.5)

$logoImg = [System.Drawing.Image]::FromFile('C:\EarClarinet\logowo.png')

$fTitle  = [System.Drawing.Font]::new('Segoe UI', 12, [System.Drawing.FontStyle]::Bold)
$fHint   = [System.Drawing.Font]::new('Segoe UI', 9, [System.Drawing.FontStyle]::Regular)
$fSmall  = [System.Drawing.Font]::new('Segoe UI', 8, [System.Drawing.FontStyle]::Bold)
$fName   = [System.Drawing.Font]::new('Segoe UI', 12, [System.Drawing.FontStyle]::Regular)
$fPct    = [System.Drawing.Font]::new('Segoe UI', 10, [System.Drawing.FontStyle]::Regular)
$fSystem = [System.Drawing.Font]::new('Segoe UI', 9, [System.Drawing.FontStyle]::Bold)
$fBadge  = [System.Drawing.Font]::new('Segoe UI', 12, [System.Drawing.FontStyle]::Bold)

# ---------- desktop (flat + simple windows + taskbar) ----------
function Draw-Desktop($g) {
    $g.Clear([System.Drawing.Color]::FromArgb(255, 16, 18, 24))
    $winFill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 22, 25, 32))
    $winBrd  = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 30, 34, 44), 1)
    $winBar  = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 26, 30, 38))
    foreach ($wr in @(@(120, 90, 560, 360), @(760, 180, 520, 300), @(300, 560, 700, 260))) {
        RRect $g $wr[0] $wr[1] $wr[2] $wr[3] 8 $winFill $winBrd
        $g.FillRectangle($winBar, (PX $wr[0]), (PX $wr[1]), (PX $wr[2]), (PX 26))
    }
    $winFill.Dispose(); $winBrd.Dispose(); $winBar.Dispose()

    $tbH = 46
    $g.FillRectangle([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 14, 15, 19)), 0, $H - (PX $tbH), $W, (PX $tbH))
    $icon = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 40, 42, 50))
    for ($i = 0; $i -lt 5; $i++) {
        $g.FillRectangle($icon, (PX (80 + $i * 64)), $H - (PX $tbH) + (PX 12), (PX 42), (PX 24))
    }
    $icon.Dispose()
    $tray = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 48, 50, 58))
    for ($i = 0; $i -lt 4; $i++) {
        $g.FillEllipse($tray, $W - (PX (40 + $i * 34)), $H - (PX $tbH) + (PX 13), (PX 18), (PX 18))
    }
    $tray.Dispose()
}

# ---------- bar ----------
function Draw-VolumeBar($g, [double]$x, [double]$y, [double]$w, [double]$pct) {
    RRect $g $x $y $w 8 4 $brushTrack $null
    $fw = $w * $pct
    if ($fw -gt 4) { RRect $g $x $y $fw 8 4 $brushAccent $null }
}

function Draw-Chip($g, [double]$x, [double]$y, [double]$w, $bgBrush, $pen, $label, $labelBrush) {
    RRect $g $x $y $w 16 4 $bgBrush $pen
    Text $g $x $y $w $fSmall $labelBrush $label 'Center'
}

# App box with FIXED columns so nothing overlaps:
#   [icon 32][name *][bar 100][pct 32][chips 56 right-aligned]
function Draw-AppBox($g, [double]$x, [double]$y, [double]$w, [double]$h, $letter, $iconColor, $name, [double]$pct, $focused, $showTab, $showM) {
    $tw = PX $w; $th = PX $h
    $tmp = [System.Drawing.Bitmap]::new($tw, $th)
    $tg = [System.Drawing.Graphics]::FromImage($tmp)
    $tg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $tg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $pen = if ($focused) { $penFocus } else { $penBox }
    RRect $tg 0 0 $w $h 8 $brushBox $pen

    # icon
    $iconBrush = [System.Drawing.SolidBrush]::new($iconColor)
    RRect $tg 8 (($h - 28) / 2) 28 28 6 $iconBrush $null
    $iconBrush.Dispose()
    Text $tg 8 (($h - 28) / 2) 28 $fSmall $brushText $letter 'Center'

    # name
    Text $tg 46 (($h - 20) / 2) 96 $fName $brushText $name

    # bar + pct
    $barX = 46 + 96 + 8
    Draw-VolumeBar $tg $barX (($h - 8) / 2) 100 $pct
    Text $tg ($barX + 100 + 8) (($h - 20) / 2) 32 $fPct $brushDim ("{0}%" -f [int][math]::Round($pct * 100))

    # chips (fixed 56-wide column, right-aligned)
    $cx = $w - 56
    if ($showTab) {
        Draw-Chip $tg $cx (($h - 16) / 2) 26 $brushAccent $null 'TAB' $brushDark
        $cx += 30
    }
    if ($showM) {
        Draw-Chip $tg $cx (($h - 16) / 2) 22 $brushChip $penChip 'M' $brushText
    }

    if ($focused) {
        $g.DrawImage($tmp, (PX $x), (PX $y), $tw, $th)
    }
    else {
        $cm = New-Object System.Drawing.Imaging.ColorMatrix
        $cm.Matrix33 = 0.55
        $attr = New-Object System.Drawing.Imaging.ImageAttributes
        $attr.SetColorMatrix($cm)
        $g.DrawImage($tmp, [System.Drawing.Rectangle]::new((PX $x), (PX $y), $tw, $th), 0, 0, $tw, $th, [System.Drawing.GraphicsUnit]::Pixel, $attr)
        $attr.Dispose()
    }
    $tg.Dispose(); $tmp.Dispose()
}

function Draw-Bar($g, [double]$x, [double]$y) {
    $barW = 1144.0
    $barH = 128.0
    RRect $g $x $y $barW $barH 14 $brushPanel $penPanel

    # header row: logo + title | hint (centered) | close chip
    $hy = $y + 10
    $g.DrawImage($logoImg, (PX ($x + 14)), (PX $hy), (PX 16), (PX 16))
    Text $g ($x + 14 + 22) $hy 18 $fTitle $brushAccent 'EarClarinet'
    $hint = 'close with Ctrl+Alt+M'
    $hintW = $g.MeasureString($hint, $fHint).Width / $scale
    Text $g ($x + ($barW - $hintW) / 2) $hy 18 $fHint $brushHint $hint
    Draw-Chip $g ($x + $barW - 14 - 22) $hy 22 $brushAccent $null 'X' $brushDark

    # main row: SYSTEM block + app boxes
    $my = $y + 10 + 34 + 10
    $mh = 54.0

    RRect $g ($x + 14) $my 190 $mh 8 $brushBox $penBox
    Text $g ($x + 14 + 12) $my 20 $fSystem $brushDim 'SYSTEM'
    Draw-VolumeBar $g ($x + 14 + 64) ($my + (($mh - 8) / 2)) 108 0.85
    Text $g ($x + 14 + 64 + 108 + 8) $my 30 $fPct $brushDim '85%'

    $bx = $x + 14 + 190 + 10
    $bw = 300.0
    Draw-AppBox $g $bx $my $bw $mh 'S' ([System.Drawing.Color]::FromArgb(255, 29, 185, 84)) 'Spotify' 0.62 $false $false $false
    $bx += $bw + 8
    Draw-AppBox $g $bx $my $bw $mh 'C' ([System.Drawing.Color]::FromArgb(255, 66, 133, 244)) 'Chrome' 1.0 $true $false $true
    $bx += $bw + 8
    Draw-AppBox $g $bx $my $bw $mh 'S' ([System.Drawing.Color]::FromArgb(255, 86, 119, 252)) 'Steam' 0.35 $false $true $false
}

function Draw-PositionBadge($g, $label) {
    Text $g 24 24 100 $fBadge ([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 200, 204, 214))) $label
}

$outDir = 'C:\EarClarinet\mockups\h6'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$variants = @(
    @{ Name = '01-top-left';      X = 16;  Y = 16 },
    @{ Name = '02-top-center';    X = 388; Y = 16 },
    @{ Name = '03-top-right';     X = 760; Y = 16 },
    @{ Name = '04-bottom-left';   X = 16;  Y = 900 },
    @{ Name = '05-bottom-center'; X = 388; Y = 900 },
    @{ Name = '06-bottom-right';  X = 760; Y = 900 }
)

foreach ($v in $variants) {
    $c = New-Canvas
    $g = $c[1]
    Draw-Desktop $g
    Draw-Bar $g ($v.X) ($v.Y)
    Draw-PositionBadge $g ($v.Name -replace '-', ' ')
    Close-Canvas $c[0] $g (Join-Path $outDir ($v.Name + '.png'))
}

$logoImg.Dispose()
Write-Output 'mockups h6 regenerados'
