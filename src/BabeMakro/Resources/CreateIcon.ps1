# Create a simple BabeMakro icon using .NET
Add-Type -AssemblyName System.Drawing

# Create a 256x256 bitmap
$bitmap = New-Object System.Drawing.Bitmap 256, 256

# Create graphics object
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# Fill background with transparent
$graphics.Clear([System.Drawing.Color]::Transparent)

# Draw main circle (pink/magenta background)
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 20, 147))
$graphics.FillEllipse($brush, 20, 20, 216, 216)

# Draw border
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 139, 0, 139)), 8
$graphics.DrawEllipse($pen, 20, 20, 216, 216)

# Draw "BM" text
$font = New-Object System.Drawing.Font("Arial", 72, [System.Drawing.FontStyle]::Bold)
$textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$stringFormat = New-Object System.Drawing.StringFormat
$stringFormat.Alignment = [System.Drawing.StringAlignment]::Center
$stringFormat.LineAlignment = [System.Drawing.StringAlignment]::Center
$graphics.DrawString("BM", $font, $textBrush, 128, 128, $stringFormat)

# Save as ICO
$iconPath = "$PSScriptRoot\BabeMakro.ico"
$bitmap.Save($iconPath, [System.Drawing.Imaging.ImageFormat]::Icon)

# Clean up
$graphics.Dispose()
$bitmap.Dispose()
$brush.Dispose()
$pen.Dispose()
$font.Dispose()
$textBrush.Dispose()

Write-Host "Icon created at: $iconPath"