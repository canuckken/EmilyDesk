param(
    [string]$InstalledPath = "$env:ProgramFiles\XWidget Reborn\XWidgetReborn.exe"
)

Write-Host "XWidget Reborn payload diagnostic"
Write-Host "Installed path: $InstalledPath"

if (Test-Path $InstalledPath) {
    $item = Get-Item $InstalledPath
    $hash = (Get-FileHash $InstalledPath -Algorithm SHA256).Hash
    Write-Host "Status: PRESENT"
    Write-Host "Size: $($item.Length) bytes"
    Write-Host "SHA-256: $hash"
    exit 0
}

Write-Host "Status: MISSING"
Write-Host ""
Write-Host "Check Windows Security:"
Write-Host "  Virus & threat protection > Protection history"
Write-Host ""
Write-Host "Also check whether an older installation path is being reused:"
Write-Host "  $env:ProgramFiles\XWidget Weather Bridge\XWidgetReborn.exe"
exit 2
