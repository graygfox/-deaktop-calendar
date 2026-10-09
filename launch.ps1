$ErrorActionPreference = 'Stop'
try {
    Start-Process -FilePath (Join-Path $PSScriptRoot 'PaperCalendar.exe') -WindowStyle Hidden
} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Paper Calendar - Startup Error') | Out-Null
}
