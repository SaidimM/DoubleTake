$assetsDir = "C:\Users\Saidi\IdeaProjects\QuickTranslator\Assets"
New-Item -ItemType Directory -Force -Path $assetsDir

function Create-DummyImage {
    param([string]$Path, [int]$Width, [int]$Height)
    
    Add-Type -AssemblyName System.Drawing
    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::DeepSkyBlue)
    $graphics.FillRectangle($brush, 0, 0, $Width, $Height)
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

Create-DummyImage -Path "$assetsDir\StoreLogo.png" -Width 50 -Height 50
Create-DummyImage -Path "$assetsDir\Square150x150Logo.png" -Width 150 -Height 150
Create-DummyImage -Path "$assetsDir\Square44x44Logo.png" -Width 44 -Height 44
Create-DummyImage -Path "$assetsDir\Wide310x150Logo.png" -Width 310 -Height 150
Create-DummyImage -Path "$assetsDir\SplashScreen.png" -Width 620 -Height 300
