$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[void][Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'PaperCalendar.exe'))
$output = Join-Path $PSScriptRoot 'verification\network-data'
$calendar = New-Object ChinaCalendar($output)
$data = $calendar.Download(2026, [DateTime]::Today)
$calendar.Merge($data)
if($data.days.Count -ne 39 -or $calendar.Marker([DateTime]::new(2026,10,1)) -ne ([string][char]0x5047) -or $calendar.Marker([DateTime]::new(2026,10,10)) -ne ([string][char]0x73ED)) { throw 'Holiday online-update validation failed' }
"PASS: downloaded and validated 2026 official holiday data; 39 records; JSON cache; holiday/workday badges." | Set-Content (Join-Path $PSScriptRoot 'verification\network-verification.txt') -Encoding UTF8
Get-Content (Join-Path $PSScriptRoot 'verification\network-verification.txt')
