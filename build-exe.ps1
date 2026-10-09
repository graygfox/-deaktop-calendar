$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$exe = Join-Path $PSScriptRoot 'PaperCalendar.exe'
Add-Type -AssemblyName System.Drawing
$bitmap=New-Object Drawing.Bitmap(48,48)
$graphics=[Drawing.Graphics]::FromImage($bitmap)
$graphics.Clear([Drawing.Color]::Transparent)
$graphics.FillRectangle([Drawing.Brushes]::DarkOliveGreen,2,2,44,44)
$brush=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(245,232,195))
$graphics.FillRectangle($brush,9,7,31,34)
$pen=New-Object Drawing.Pen([Drawing.Color]::FromArgb(145,122,77),2)
foreach($y in @(15,23,31)) {$graphics.DrawLine($pen,14,$y,34,$y)}
$icon=[Drawing.Icon]::FromHandle($bitmap.GetHicon())
$stream=[IO.File]::Create((Join-Path $PSScriptRoot 'paper.ico'))
$icon.Save($stream); $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $brush.Dispose(); $pen.Dispose(); $icon.Dispose()
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu "/out:$exe" "/win32icon:$(Join-Path $PSScriptRoot 'paper.ico')" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll /reference:System.Web.Extensions.dll "/resource:$(Join-Path $PSScriptRoot 'tips.tsv'),PaperCalendar.Tips" "/resource:$(Join-Path $PSScriptRoot 'corpus\LICENSE.txt'),PaperCalendar.License" "/resource:$(Join-Path $PSScriptRoot 'assets\ink-bamboo.png'),PaperCalendar.Bamboo" "/resource:$(Join-Path $PSScriptRoot 'assets\ukiyo-wave.png'),PaperCalendar.Ocean" "/resource:$(Join-Path $PSScriptRoot 'holidays\2026.json'),PaperCalendar.Holidays2026" "/resource:$(Join-Path $PSScriptRoot 'holidays\LICENSE.txt'),PaperCalendar.HolidayLicense" (Join-Path $PSScriptRoot 'Calendar.cs') (Join-Path $PSScriptRoot 'Holidays.cs') (Join-Path $PSScriptRoot 'Themes.cs')
if($LASTEXITCODE -ne 0) {throw 'EXE compilation failed'}
Write-Output "Built: $exe"

