# Generates wwwroot/Images/og-image.png (1200x630) - the social share card.
# Composites a real app screenshot onto a dark brand canvas with a wordmark header strip.
# Re-run after changing the screenshot / wordmark / tagline. Requires Windows PowerShell (System.Drawing).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$imgDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\PegboardWebSite\wwwroot\Images'))
$shot   = Join-Path $imgDir 'site-dashboard.png'   # mid-session dashboard - the representative shot
$out    = Join-Path $imgDir 'og-image.png'

$W = 1200; $H = 630
$bg    = [System.Drawing.ColorTranslator]::FromHtml('#0f1419')
$white = [System.Drawing.ColorTranslator]::FromHtml('#e6edf3')
$cyan  = [System.Drawing.ColorTranslator]::FromHtml('#4cc9f0')
$blue  = [System.Drawing.ColorTranslator]::FromHtml('#3a86ff')
$muted = [System.Drawing.ColorTranslator]::FromHtml('#9aa7b4')

$bmp = New-Object System.Drawing.Bitmap($W, $H)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear($bg)

# Top accent band
$barRect = New-Object System.Drawing.Rectangle(0, 0, $W, 8)
$barBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($barRect, $cyan, $blue, [System.Drawing.Drawing2D.LinearGradientMode]::Horizontal)
$g.FillRectangle($barBrush, $barRect)

# --- Header strip: wordmark + tagline ---
$fam = New-Object System.Drawing.FontFamily('Segoe UI')
$wordFont = New-Object System.Drawing.Font($fam, 54, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$tagFont  = New-Object System.Drawing.Font($fam, 26, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fmt = [System.Drawing.StringFormat]::GenericTypographic
$fmt.FormatFlags = $fmt.FormatFlags -bor [System.Drawing.StringFormatFlags]::MeasureTrailingSpaces

$marginX = 50
$wordY = 34
# wordmark: e (white) P (cyan) egboard (white)
$segs = @(
  @{ t = 'e';       c = $white },
  @{ t = 'P';       c = $cyan  },
  @{ t = 'egboard'; c = $white }
)
$x = $marginX
foreach ($s in $segs) {
  $brush = New-Object System.Drawing.SolidBrush($s.c)
  $g.DrawString($s.t, $wordFont, $brush, $x, $wordY, $fmt)
  $x += ($g.MeasureString($s.t, $wordFont, 10000, $fmt)).Width
  $brush.Dispose()
}
# tagline right-aligned on the same band
$tagline = 'Run your club night without the clipboard'
$tagSize = $g.MeasureString($tagline, $tagFont, 10000, $fmt)
$tagBrush = New-Object System.Drawing.SolidBrush($muted)
$g.DrawString($tagline, $tagFont, $tagBrush, ($W - $marginX - $tagSize.Width), 56, $fmt)

# --- Screenshot, scaled to fit the area below the header ---
$areaTop = 130
$areaPad = 50
$areaW = $W - (2 * $areaPad)
$areaH = $H - $areaTop - $areaPad

$src = [System.Drawing.Image]::FromFile($shot)
$scale = [Math]::Min($areaW / $src.Width, $areaH / $src.Height)
$dw = [int]($src.Width * $scale)
$dh = [int]($src.Height * $scale)
$dx = [int](($W - $dw) / 2)
$dy = [int]($areaTop + (($areaH - $dh) / 2))

# subtle frame behind the screenshot
$frame = New-Object System.Drawing.Rectangle(($dx-2), ($dy-2), ($dw+4), ($dh+4))
$framePen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml('#2a3441'), 2)
$g.DrawRectangle($framePen, $frame)
$g.DrawImage($src, (New-Object System.Drawing.Rectangle($dx, $dy, $dw, $dh)))
$src.Dispose()

$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "Wrote $out ($W x $H) from $(Split-Path $shot -Leaf)" -ForegroundColor Green
