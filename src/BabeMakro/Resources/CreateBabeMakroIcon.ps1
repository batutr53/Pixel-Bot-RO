# Create BabeMakro icon based on the provided logo design
Add-Type -AssemblyName System.Drawing

# Create multiple sizes for the icon (16x16, 32x32, 48x48, 64x64, 128x128, 256x256)
$sizes = @(16, 32, 48, 64, 128, 256)
$bitmaps = @()

foreach ($size in $sizes) {
    # Create bitmap
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear([System.Drawing.Color]::Transparent)
    
    # Calculate dimensions based on size
    $padding = [math]::Max(1, $size / 16)
    $circleSize = $size - (2 * $padding)
    
    # Draw main circle background (hot pink from the logo)
    $mainBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 20, 147)) # DeepPink
    $graphics.FillEllipse($mainBrush, $padding, $padding, $circleSize, $circleSize)
    
    # Draw gradient effect (lighter center)
    $centerSize = $circleSize * 0.7
    $centerOffset = ($circleSize - $centerSize) / 2 + $padding
    $centerBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(180, 255, 105, 180)) # HotPink with transparency
    $graphics.FillEllipse($centerBrush, $centerOffset, $centerOffset, $centerSize, $centerSize)
    
    # Draw border (darker pink)
    $borderThickness = [math]::Max(1, $size / 32)
    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 139, 0, 139)), $borderThickness # DarkMagenta
    $graphics.DrawEllipse($borderPen, $padding, $padding, $circleSize, $circleSize)
    
    # Draw gear/crown elements (simplified for small sizes)
    if ($size -ge 32) {
        $gearBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 215, 0)) # Gold
        $gearSize = $size * 0.15
        
        # Draw 6 small gear teeth around the circle
        for ($i = 0; $i -lt 6; $i++) {
            $angle = $i * 60 * [Math]::PI / 180
            $x = ($size / 2) + ([Math]::Cos($angle) * ($circleSize / 2 - $gearSize / 2)) - ($gearSize / 2)
            $y = ($size / 2) + ([Math]::Sin($angle) * ($circleSize / 2 - $gearSize / 2)) - ($gearSize / 2)
            $graphics.FillEllipse($gearBrush, $x, $y, $gearSize, $gearSize)
        }
        
        $gearBrush.Dispose()
    }
    
    # Draw character silhouette (simplified for icon)
    if ($size -ge 24) {
        # Head
        $headSize = $size * 0.25
        $headX = ($size / 2) - ($headSize / 2)
        $headY = ($size / 2) - ($headSize / 1.5)
        $skinBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 218, 185)) # PeachPuff
        $graphics.FillEllipse($skinBrush, $headX, $headY, $headSize, $headSize)
        
        # Hair (pink/red)
        $hairBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 220, 20, 60)) # Crimson
        $hairSize = $headSize * 1.2
        $hairX = $headX - ($hairSize - $headSize) / 2
        $hairY = $headY - ($hairSize - $headSize) / 2
        $graphics.FillEllipse($hairBrush, $hairX, $hairY, $hairSize, $hairSize * 0.8)
        
        # Simple body
        if ($size -ge 48) {
            $bodyWidth = $headSize * 0.8
            $bodyHeight = $headSize * 0.6
            $bodyX = ($size / 2) - ($bodyWidth / 2)
            $bodyY = $headY + $headSize * 0.7
            $graphics.FillEllipse($skinBrush, $bodyX, $bodyY, $bodyWidth, $bodyHeight)
        }
        
        $skinBrush.Dispose()
        $hairBrush.Dispose()
    }
    
    # Draw "BabeMakro" text or "BM" for smaller sizes
    $textColor = [System.Drawing.Color]::White
    $textBrush = New-Object System.Drawing.SolidBrush $textColor
    $shadowBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(128, 0, 0, 0))
    
    if ($size -ge 64) {
        # Full "BabeMakro" text
        $fontSize = [math]::Max(6, $size / 16)
        $font = New-Object System.Drawing.Font("Arial", $fontSize, [System.Drawing.FontStyle]::Bold)
        $text = "BabeMakro"
        $textSize = $graphics.MeasureString($text, $font)
        $textX = ($size - $textSize.Width) / 2
        $textY = $size - $textSize.Height - $padding
        
        # Shadow
        $graphics.DrawString($text, $font, $shadowBrush, $textX + 1, $textY + 1)
        # Main text
        $graphics.DrawString($text, $font, $textBrush, $textX, $textY)
        $font.Dispose()
    } elseif ($size -ge 24) {
        # "BM" text
        $fontSize = [math]::Max(8, $size / 4)
        $font = New-Object System.Drawing.Font("Arial", $fontSize, [System.Drawing.FontStyle]::Bold)
        $text = "BM"
        $textSize = $graphics.MeasureString($text, $font)
        $textX = ($size - $textSize.Width) / 2
        $textY = ($size - $textSize.Height) / 2
        
        # Shadow
        $graphics.DrawString($text, $font, $shadowBrush, $textX + 1, $textY + 1)
        # Main text
        $graphics.DrawString($text, $font, $textBrush, $textX, $textY)
        $font.Dispose()
    } else {
        # Very small, just "B"
        $fontSize = [math]::Max(6, $size / 2.5)
        $font = New-Object System.Drawing.Font("Arial", $fontSize, [System.Drawing.FontStyle]::Bold)
        $text = "B"
        $textSize = $graphics.MeasureString($text, $font)
        $textX = ($size - $textSize.Width) / 2
        $textY = ($size - $textSize.Height) / 2
        
        $graphics.DrawString($text, $font, $textBrush, $textX, $textY)
        $font.Dispose()
    }
    
    $graphics.Dispose()
    $mainBrush.Dispose()
    $centerBrush.Dispose()
    $borderPen.Dispose()
    $textBrush.Dispose()
    $shadowBrush.Dispose()
    
    $bitmaps += $bitmap
}

# Save as proper ICO format
$iconPath = "$PSScriptRoot\BabeMakro.ico"

# Create icon from the 32x32 bitmap (most common size)
$icon = [System.Drawing.Icon]::FromHandle($bitmaps[1].GetHicon())
$fileStream = [System.IO.File]::Create($iconPath)
$icon.Save($fileStream)
$fileStream.Close()

# Clean up
$icon.Dispose()
foreach ($bitmap in $bitmaps) {
    $bitmap.Dispose()
}

Write-Host "BabeMakro icon created at: $iconPath"