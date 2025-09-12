# Create a proper ICO file using .NET
Add-Type -AssemblyName System.Drawing

# Create multiple sizes for the icon (16x16, 32x32, 48x48, 256x256)
$sizes = @(16, 32, 48, 256)
$bitmaps = @()

foreach ($size in $sizes) {
    # Create bitmap
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    
    # Draw circle background (pink/magenta)
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 20, 147))
    $padding = [math]::Max(2, $size / 16)
    $graphics.FillEllipse($brush, $padding, $padding, $size - 2*$padding, $size - 2*$padding)
    
    # Draw border
    $penThickness = [math]::Max(1, $size / 32)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 139, 0, 139)), $penThickness
    $graphics.DrawEllipse($pen, $padding, $padding, $size - 2*$padding, $size - 2*$padding)
    
    # Draw "BM" text
    $fontSize = [math]::Max(6, $size / 4)
    $font = New-Object System.Drawing.Font("Arial", $fontSize, [System.Drawing.FontStyle]::Bold)
    $textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $stringFormat = New-Object System.Drawing.StringFormat
    $stringFormat.Alignment = [System.Drawing.StringAlignment]::Center
    $stringFormat.LineAlignment = [System.Drawing.StringAlignment]::Center
    $graphics.DrawString("BM", $font, $textBrush, $size/2, $size/2, $stringFormat)
    
    $graphics.Dispose()
    $brush.Dispose()
    $pen.Dispose()
    $font.Dispose()
    $textBrush.Dispose()
    
    $bitmaps += $bitmap
}

# Save as ICO format manually
$iconPath = "$PSScriptRoot\BabeMakro.ico"

# Use .NET Icon class to create proper ICO
$icon = [System.Drawing.Icon]::FromHandle($bitmaps[1].GetHicon()) # Use 32x32 for icon
$fileStream = [System.IO.File]::Create($iconPath)
$icon.Save($fileStream)
$fileStream.Close()

# Clean up
$icon.Dispose()
foreach ($bitmap in $bitmaps) {
    $bitmap.Dispose()
}

Write-Host "Proper ICO created at: $iconPath"