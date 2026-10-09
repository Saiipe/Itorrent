# Gera as imagens do assistente do instalador a partir do ícone em icon/png.
# Rode de novo sempre que o ícone mudar:
#   powershell -ExecutionPolicy Bypass -File installer\make-wizard-images.ps1

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$icon = [System.Drawing.Image]::FromFile((Join-Path $root 'icon\png\itorrent-512x512.png'))
$out = Join-Path $PSScriptRoot 'images'
New-Item -ItemType Directory -Force $out | Out-Null

function New-Canvas([int]$w, [int]$h, [System.Drawing.Color]$bg) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear($bg)
    return $bmp, $g
}

# Imagem pequena (canto superior direito). Tamanhos para 100% a 250% de escala.
$smallSizes = @(@(55, 55), @(64, 68), @(69, 69), @(83, 80), @(92, 97), @(110, 106), @(119, 123), @(138, 140))
foreach ($s in $smallSizes) {
    $w = $s[0]; $h = $s[1]
    $bmp, $g = New-Canvas $w $h ([System.Drawing.Color]::White)
    $side = [Math]::Min($w, $h)
    $g.DrawImage($icon, [int](($w - $side) / 2), [int](($h - $side) / 2), $side, $side)
    $bmp.Save((Join-Path $out "small-$w.bmp"), [System.Drawing.Imaging.ImageFormat]::Bmp)
    $g.Dispose(); $bmp.Dispose()
}

# Imagem grande (lateral das telas de boas-vindas e conclusão): azul-marinho do tema.
$navy = [System.Drawing.Color]::FromArgb(0, 0, 128)
$largeSizes = @(@(164, 314), @(192, 386), @(246, 459), @(273, 556), @(328, 604), @(355, 700), @(410, 797))
foreach ($s in $largeSizes) {
    $w = $s[0]; $h = $s[1]
    $bmp, $g = New-Canvas $w $h $navy
    $scale = $w / 164.0

    $side = [int](116 * $scale)
    $top = [int](64 * $scale)
    $g.DrawImage($icon, [int](($w - $side) / 2), $top, $side, $side)

    $center = New-Object System.Drawing.StringFormat
    $center.Alignment = 'Center'
    $title = New-Object System.Drawing.Font 'Microsoft Sans Serif', ([single](17 * $scale)), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $sub = New-Object System.Drawing.Font 'Microsoft Sans Serif', ([single](11 * $scale)), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $textTop = $top + $side + [int](14 * $scale)
    $g.DrawString('Itorrent', $title, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, $textTop, $w, (30 * $scale)), $center)
    $g.DrawString("Cliente BitTorrent`nsem anúncios", $sub, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(192, 192, 192))),
        (New-Object System.Drawing.RectangleF 0, ($textTop + 26 * $scale), $w, (40 * $scale)), $center)

    $bmp.Save((Join-Path $out "large-$w.bmp"), [System.Drawing.Imaging.ImageFormat]::Bmp)
    $g.Dispose(); $bmp.Dispose()
}

$icon.Dispose()
Get-ChildItem $out | Select-Object Name, Length
