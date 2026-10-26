# Starts a packaged UsageWidget.exe, waits for its window and closes it like Alt+F4 does.
# Fails if no window appears or the app does not exit. Usage: powershell -File tools/smoke-windows.ps1 <exe>
param([Parameter(Mandatory)][string]$Exe)
$ErrorActionPreference = 'Stop'

$process = Start-Process $Exe -PassThru
for ($i = 0; $i -lt 30 -and $process.MainWindowHandle -eq 0; $i++) {
    Start-Sleep -Seconds 1
    $process.Refresh()
    if ($process.HasExited) { throw "Exited early with code $($process.ExitCode)" }
}
if ($process.MainWindowHandle -eq 0) { $process.Kill(); throw 'No window appeared' }
"Window '$($process.MainWindowTitle)' is up"

Start-Sleep -Seconds 5
[void]$process.CloseMainWindow()
if (-not $process.WaitForExit(15000)) { $process.Kill(); throw 'Did not exit after closing' }
'Closed cleanly'
