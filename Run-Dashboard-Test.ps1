$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$exe = Join-Path $PSScriptRoot "EmilyDesk.exe"

if (-not (Test-Path $exe)) {
    Write-Host "ERROR: EmilyDesk.exe is missing."
    exit 1
}

Write-Host "Starting EmilyDesk Gallery..."

try {
    $process = Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot -PassThru
} catch {
    Write-Host "ERROR: The dashboard could not be started."
    Write-Host $_.Exception.Message
    exit 2
}

Start-Sleep -Seconds 4
$process.Refresh()

if ($process.HasExited) {
    Write-Host "ERROR: The dashboard exited during startup."
    Write-Host "Exit code: $($process.ExitCode)"
    Write-Host "Check Malwarebytes Detection History and Windows Security Protection history."
    exit 3
}

Write-Host "PASS: EmilyDesk Dashboard process is running."
Write-Host "Process ID: $($process.Id)"
Write-Host ""
Write-Host "Now verify:"
Write-Host "  1. The tray icon is visible."
Write-Host "  2. Closing the dashboard leaves the tray icon running."
Write-Host "  3. Double-clicking the tray icon restores the dashboard."
Write-Host "  4. Right-clicking the tray icon displays the Reborn menu."
exit 0
