Add-Type -AssemblyName System.Drawing

$size = 32
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# --- 背景円（青緑）---
$bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 26, 140, 160))
$g.FillEllipse($bgBrush, 0, 0, $size-1, $size-1)

# --- 時計外枠 ---
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 2)
$g.DrawEllipse($pen, 5, 5, 21, 21)

# --- 時計の針（12時・3時方向）---
$pen2 = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 2)
$pen2.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen2.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($pen2, 16, 16, 16, 9)   # 時針（上）
$g.DrawLine($pen2, 16, 16, 22, 16)  # 分針（右）

# --- 中心点 ---
$dotBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$g.FillEllipse($dotBrush, 14, 14, 4, 4)

# --- 循環矢印の右下アーク（定期アクセスのイメージ）---
$pen3 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200,255,255,255), 1.5)
$g.DrawArc($pen3, 20, 20, 9, 9, 180, 270)

$g.Dispose()

# --- ICO 形式で保存 ---
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $ms.ToArray()
$ms.Dispose()
$bmp.Dispose()

$icoStream = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($icoStream)
$w.Write([uint16]0)
$w.Write([uint16]1)
$w.Write([uint16]1)
$w.Write([byte]32)
$w.Write([byte]32)
$w.Write([byte]0)
$w.Write([byte]0)
$w.Write([uint16]1)
$w.Write([uint16]32)
$w.Write([uint32]$pngBytes.Length)
$w.Write([uint32]22)
$w.Write($pngBytes)
$w.Flush()

$outPath = Join-Path $PSScriptRoot "app.ico"
[System.IO.File]::WriteAllBytes($outPath, $icoStream.ToArray())
Write-Host "アイコンを作成しました: $outPath"
