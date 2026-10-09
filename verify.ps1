$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'verification'
$process = Start-Process -FilePath (Join-Path $PSScriptRoot 'PaperCalendar.exe') -ArgumentList @('--self-test', ('"'+$output+'"')) -WindowStyle Hidden -PassThru -Wait
if($process.ExitCode -ne 0) {throw 'Desktop verification failed'}
Get-Content (Join-Path $output 'verification.txt')
