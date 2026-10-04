param([string]$PayloadDirectory = (Join-Path $env:LOCALAPPDATA 'EmilyDesk'))

$ErrorActionPreference = 'Stop'
$productDirectory = Join-Path $env:LOCALAPPDATA 'EmilyDesk'
$stateDirectory = Join-Path $productDirectory 'WidgetState'
$statusPath = Join-Path $productDirectory 'engine.status'
$dashboardExe = Join-Path $PayloadDirectory 'EmilyDesk.exe'
$backup = Join-Path $env:TEMP ('EmilyDesk-Stage3-' + [Guid]::NewGuid().ToString('N'))

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class EmilyDeskStage3Windows {
  public delegate bool Callback(IntPtr window, IntPtr value);
  [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback, IntPtr value);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
  public struct Rect { public int Left, Top, Right, Bottom; }
  public static int[] Size(string title) {
    int[] size = null;
    EnumWindows(delegate(IntPtr window, IntPtr value) {
      var text = new StringBuilder(256); GetWindowText(window, text, text.Capacity);
      if (IsWindowVisible(window) && string.Equals(text.ToString(), title, StringComparison.Ordinal)) {
        Rect rect; if (GetWindowRect(window, out rect)) size = new[]{rect.Right-rect.Left, rect.Bottom-rect.Top};
      }
      return true;
    }, IntPtr.Zero);
    return size;
  }
}
'@

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    Write-Host ('PASS: ' + $message)
}
function Wait-Until([scriptblock]$condition, [int]$seconds, [string]$failure) {
    $until = [DateTime]::UtcNow.AddSeconds($seconds)
    while ([DateTime]::UtcNow -lt $until) {
        if (& $condition) { return }
        Start-Sleep -Milliseconds 100
    }
    throw $failure
}
function Read-Status {
    $value = @{}
    try { $lines = @(Get-Content -LiteralPath $statusPath -ErrorAction Stop) } catch { return $value }
    foreach ($line in $lines) { $at=$line.IndexOf('='); if($at -gt 0){$value[$line.Substring(0,$at)]=$line.Substring($at+1)} }
    return $value
}
function Write-State([string]$widget, [string]$key, [string]$value) {
    [IO.File]::WriteAllText((Join-Path $stateDirectory ($widget + '.' + $key.Replace('.','_') + '.setting')), $value)
}
function Exit-EmilyDesk {
    try { $event=[Threading.EventWaitHandle]::OpenExisting('Local\XWidgetRebornExitDashboard'); try{$event.Set()|Out-Null}finally{$event.Dispose()} } catch {}
    Wait-Until { -not (Get-Process -Name 'EmilyDesk','EmilyDesk.Engine' -ErrorAction SilentlyContinue) } 12 'EmilyDesk did not exit'
}
function Stop-ExistingEmilyDesk {
    $dashboard=Get-Process -Name 'EmilyDesk' -ErrorAction SilentlyContinue
    if($dashboard){try{Exit-EmilyDesk}catch{}}
    Get-Process -Name 'EmilyDesk.Engine' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}
function Start-Installed {
    Remove-Item -LiteralPath $statusPath -Force -ErrorAction SilentlyContinue
    $process=Start-Process -FilePath $dashboardExe -ArgumentList '--tray' -WorkingDirectory $PayloadDirectory -PassThru
    Wait-Until {
        $engine=Get-Process -Name 'EmilyDesk.Engine' -ErrorAction SilentlyContinue | Select-Object -First 1
        $engine -and $engine.Path -and
            $engine.Path.StartsWith($PayloadDirectory,[StringComparison]::OrdinalIgnoreCase) -and
            ((Read-Status).widgets -split ',' | Where-Object {$_}).Count -eq 3 -and
            $null -ne [EmilyDeskStage3Windows]::Size('Clock') -and
            $null -ne [EmilyDeskStage3Windows]::Size('Calendar') -and
            $null -ne [EmilyDeskStage3Windows]::Size('Weather')
    } 15 'official widgets did not become ready with visible installed-payload surfaces'
    return $process
}

Assert-True (Test-Path $dashboardExe -PathType Leaf) 'installed Dashboard exists'
New-Item -ItemType Directory -Path $backup -Force | Out-Null
if(Test-Path $stateDirectory){Copy-Item $stateDirectory (Join-Path $backup 'WidgetState') -Recurse -Force}
try {
    Stop-ExistingEmilyDesk
    if(Test-Path $stateDirectory){Remove-Item $stateDirectory -Recurse -Force}
    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    foreach($widget in @('native.clock','native.calendar','native.weather')){[IO.File]::WriteAllText((Join-Path $stateDirectory ($widget+'.enabled')),'True')}
    $first=Start-Installed
    foreach($title in @('Clock','Calendar','Weather')){Assert-True ($null -ne [EmilyDeskStage3Windows]::Size($title)) ($title+' renders on first launch')}
    Exit-EmilyDesk

    Write-State 'native.clock' 'scale' '0.5'; Write-State 'native.clock' 'opacity' '0.75'; Write-State 'native.clock' 'mode' 'Digital'; Write-State 'native.clock' 'showSeconds' 'False'
    Write-State 'native.calendar' 'scale' '1.25'; Write-State 'native.calendar' 'opacity' '0.9'; Write-State 'native.calendar' 'weekStart' 'Monday'
    Write-State 'native.weather' 'scale' '2'; Write-State 'native.weather' 'opacity' '0.5'; Write-State 'native.weather' 'weather.units' 'Imperial'
    foreach($widget in @('native.clock','native.calendar','native.weather')){Write-State $widget 'host.layerMode' 'NormalDesktop'; Write-State $widget 'host.lockPosition' 'True'}
    $configured=Start-Installed
    $sizes=@{}; foreach($title in @('Clock','Calendar','Weather')){$sizes[$title]=[EmilyDeskStage3Windows]::Size($title); Assert-True ($sizes[$title][0] -gt 0 -and $sizes[$title][1] -gt 0) ($title+' scaled surface is valid')}
    Assert-True ($sizes.Weather[0] -gt $sizes.Calendar[0] -and $sizes.Calendar[0] -gt $sizes.Clock[0]) '50%-200% scale settings produce ordered native-pixel sizes'
    Exit-EmilyDesk
    $restart=Start-Installed
    foreach($title in @('Clock','Calendar','Weather')){$after=[EmilyDeskStage3Windows]::Size($title); Assert-True ($after[0] -eq $sizes[$title][0] -and $after[1] -eq $sizes[$title][1]) ($title+' scale persists across restart')}
    Assert-True ((Get-Content (Join-Path $stateDirectory 'native.clock.mode.setting') -Raw).Trim() -eq 'Digital') 'Clock mode persists'
    Assert-True ((Get-Content (Join-Path $stateDirectory 'native.clock.showSeconds.setting') -Raw).Trim() -eq 'False') 'Clock second-hand option persists'
    Assert-True ((Get-Content (Join-Path $stateDirectory 'native.calendar.weekStart.setting') -Raw).Trim() -eq 'Monday') 'Calendar week start persists'
    foreach($widget in @('native.clock','native.calendar','native.weather')){Assert-True ((Get-Content (Join-Path $stateDirectory ($widget+'.host_lockPosition.setting')) -Raw).Trim() -eq 'True') ($widget+' lock position persists')}
    Exit-EmilyDesk

    $weatherStatus=Invoke-RestMethod -Uri 'http://127.0.0.1:45873/xwidgetbridge/status.json' -TimeoutSec 8
    Assert-True ($null -ne $weatherStatus.providerConsecutiveFailures) 'weather provider health is reported'
    Assert-True ($null -ne $weatherStatus.usingCachedData) 'weather cache-use health is reported'
    $current=Invoke-RestMethod -Uri 'http://127.0.0.1:45873/currentconditions/v1/54704.json?details=true' -TimeoutSec 15
    Assert-True (@($current).Count -gt 0) 'weather live-or-cached data remains available'
    $profilePath=[string]$weatherStatus.providerProfileSource
    Assert-True (Test-Path -LiteralPath $profilePath -PathType Leaf) 'installed weather provider profile exists'
    $profileBackup=[IO.File]::ReadAllText($profilePath)
    try {
        $offlineProfile=$profileBackup -replace 'https://api\.open-meteo\.com/v1/forecast','https://127.0.0.1:9/v1/forecast'
        [IO.File]::WriteAllText($profilePath,$offlineProfile,[Text.UTF8Encoding]::new($false))
        Invoke-RestMethod -Uri 'http://127.0.0.1:45873/xwidgetbridge/provider/reload' -TimeoutSec 8 | Out-Null
        $cached=Invoke-RestMethod -Uri 'http://127.0.0.1:45873/currentconditions/v1/54704.json?details=true' -TimeoutSec 15
        $offlineStatus=Invoke-RestMethod -Uri 'http://127.0.0.1:45873/xwidgetbridge/status.json' -TimeoutSec 8
        Assert-True (@($cached).Count -gt 0) 'offline provider failure serves cached last-known weather'
        Assert-True ([bool]$offlineStatus.usingCachedData) 'offline cache fallback is reported'
        Assert-True ([int]$offlineStatus.providerConsecutiveFailures -gt 0) 'offline provider failure and backoff are tracked'
    }
    finally {
        [IO.File]::WriteAllText($profilePath,$profileBackup,[Text.UTF8Encoding]::new($false))
        Invoke-RestMethod -Uri 'http://127.0.0.1:45873/xwidgetbridge/provider/reload' -TimeoutSec 8 | Out-Null
    }
    Write-Host 'STAGE 3 OFFICIAL WIDGET TESTS PASSED'
}
finally {
    Stop-ExistingEmilyDesk
    if(Test-Path $stateDirectory){Remove-Item $stateDirectory -Recurse -Force}
    if(Test-Path (Join-Path $backup 'WidgetState')){Copy-Item (Join-Path $backup 'WidgetState') $stateDirectory -Recurse -Force}
    Remove-Item $backup -Recurse -Force -ErrorAction SilentlyContinue
}
