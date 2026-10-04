param([string]$PayloadDirectory = (Join-Path $PSScriptRoot 'Stage'))

$ErrorActionPreference = 'Stop'
$dashboardExe = Join-Path $PayloadDirectory 'EmilyDesk.exe'
$engineExe = Join-Path $PayloadDirectory 'EmilyDesk.Engine.exe'
$productDirectory = Join-Path $env:LOCALAPPDATA 'EmilyDesk'
$stateDirectory = Join-Path $productDirectory 'WidgetState'
$statusPath = Join-Path $productDirectory 'engine.status'
$dashboardLogPath = Join-Path $productDirectory 'Logs\dashboard.log'
$backupRoot = Join-Path $env:TEMP ('EmilyDesk-Stage2-State-' + [Guid]::NewGuid().ToString('N'))
$stateBackup = Join-Path $backupRoot 'WidgetState'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class EmilyDeskWindowOrder {
    public delegate bool EnumCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtr")] static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint="GetWindowLong")] static extern IntPtr GetWindowLongPtr32(IntPtr window, int index);
    static IntPtr GetWindowLongPtr(IntPtr window, int index) { return IntPtr.Size == 8 ? GetWindowLongPtr64(window, index) : GetWindowLongPtr32(window, index); }
    public static IntPtr[] VisibleWindowsForProcess(int processId, bool topMost) {
        var result = new List<IntPtr>();
        EnumWindows(delegate(IntPtr window, IntPtr parameter) {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            bool isTopMost = (GetWindowLongPtr(window, -20).ToInt64() & 8) != 0;
            if (owner == processId && IsWindowVisible(window) && isTopMost == topMost) result.Add(window);
            return true;
        }, IntPtr.Zero);
        return result.ToArray();
    }
    public static IntPtr[] VisibleWindowsInZOrder() {
        var result = new List<IntPtr>();
        EnumWindows(delegate(IntPtr window, IntPtr parameter) { if (IsWindowVisible(window)) result.Add(window); return true; }, IntPtr.Zero);
        return result.ToArray();
    }
}
'@

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    Write-Host ('PASS: ' + $Message)
}

function Wait-Until([scriptblock]$Condition, [int]$Seconds, [string]$Failure) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 100
    }
    throw $Failure
}

function Read-Status {
    $result = @{}
    if (-not (Test-Path -LiteralPath $statusPath)) { return $result }
    try { $lines = @(Get-Content -LiteralPath $statusPath -ErrorAction Stop) }
    catch [IO.IOException] { return $result }
    foreach ($line in $lines) {
        $equals = $line.IndexOf('=')
        if ($equals -gt 0) { $result[$line.Substring(0, $equals)] = $line.Substring($equals + 1) }
    }
    return $result
}

function Send-WidgetCommand([string]$Action, [string]$WidgetId) {
    $commands = Join-Path $productDirectory 'Commands'
    New-Item -ItemType Directory -Path $commands -Force | Out-Null
    $name = [Guid]::NewGuid().ToString('N')
    $temporary = Join-Path $commands ($name + '.tmp')
    $command = Join-Path $commands ($name + '.command')
    [IO.File]::WriteAllText($temporary, $Action + [Environment]::NewLine + [Uri]::EscapeDataString($WidgetId) + [Environment]::NewLine)
    Move-Item -LiteralPath $temporary -Destination $command
    $signal = [Threading.EventWaitHandle]::OpenExisting('Local\XWidgetRebornEngineCommand')
    try { $signal.Set() | Out-Null } finally { $signal.Dispose() }
}

function Signal-Event([string]$Name) {
    $signal = [Threading.EventWaitHandle]::OpenExisting($Name)
    try { $signal.Set() | Out-Null } finally { $signal.Dispose() }
}

function Set-TestLayer([string]$WidgetId, [string]$Layer) {
    [IO.File]::WriteAllText(
        (Join-Path $stateDirectory ($WidgetId + '.host_layerMode.setting')),
        $Layer)
    [IO.File]::WriteAllText(
        (Join-Path $stateDirectory ($WidgetId + '.topmost')),
        ([string]($Layer -eq 'AlwaysOnTop')))
}

function Stop-TestProcesses {
    $dashboard = Get-Process -Name 'EmilyDesk' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($PayloadDirectory, [StringComparison]::OrdinalIgnoreCase) }
    if ($dashboard) {
        try { Signal-Event 'Local\XWidgetRebornExitDashboard' } catch {}
        $deadline=[DateTime]::UtcNow.AddSeconds(12)
        while($dashboard | Where-Object {-not $_.HasExited}) {
            if([DateTime]::UtcNow -ge $deadline){break}
            Start-Sleep -Milliseconds 100
            $dashboard | ForEach-Object {$_.Refresh()}
        }
    }
    Get-Process -Name 'EmilyDesk.Engine' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($PayloadDirectory, [StringComparison]::OrdinalIgnoreCase) } |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

Assert-True (Test-Path -LiteralPath $dashboardExe -PathType Leaf) 'staged Dashboard executable exists'
Assert-True (Test-Path -LiteralPath $engineExe -PathType Leaf) 'staged Engine executable exists'
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
if (Test-Path -LiteralPath $stateDirectory) { Copy-Item -LiteralPath $stateDirectory -Destination $stateBackup -Recurse -Force }

try {
    Stop-TestProcesses
    $trayLogBaseline = if(Test-Path $dashboardLogPath){@(Get-Content $dashboardLogPath).Count}else{0}
    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    @('native.clock','native.calendar','native.weather','native.recyclebin') | ForEach-Object {
        Set-TestLayer $_ 'NormalDesktop'
    }
    $dashboard = Start-Process -FilePath $dashboardExe -ArgumentList '--tray' -WorkingDirectory $PayloadDirectory -PassThru
    Wait-Until {
        $status = Read-Status
        $engineProcess = Get-Process -Name 'EmilyDesk.Engine' -ErrorAction SilentlyContinue | Select-Object -First 1
        (Get-Process -Id $dashboard.Id -ErrorAction SilentlyContinue) -and
            $engineProcess -and $status.pid -and
            [int]$status.pid -eq $engineProcess.Id -and
            $engineProcess.Path.StartsWith($PayloadDirectory, [StringComparison]::OrdinalIgnoreCase)
    } 10 'Dashboard or Engine did not become ready with a fresh matching heartbeat'
    $firstStatus = Read-Status
    $firstEnginePid = [int]$firstStatus.pid
    Assert-True (@(Get-Process -Name 'EmilyDesk' -ErrorAction Stop).Count -eq 1) 'only one Dashboard instance is running'
    Assert-True (@(Get-Process -Name 'EmilyDesk.Engine' -ErrorAction Stop).Count -eq 1) 'only one Engine instance is running'

    $duplicateDashboard = Start-Process -FilePath $dashboardExe -ArgumentList '--tray' -WorkingDirectory $PayloadDirectory -PassThru
    $duplicateEngine = Start-Process -FilePath $engineExe -WorkingDirectory $PayloadDirectory -PassThru
    Wait-Until { $duplicateDashboard.HasExited -and $duplicateEngine.HasExited } 5 'duplicate Dashboard or Engine did not exit'
    Assert-True (@(Get-Process -Name 'EmilyDesk' -ErrorAction Stop).Count -eq 1) 'duplicate Dashboard launch was rejected'
    Assert-True (@(Get-Process -Name 'EmilyDesk.Engine' -ErrorAction Stop).Count -eq 1) 'duplicate Engine launch was rejected'
    1..5 | ForEach-Object {
        $repeat=Start-Process -FilePath $dashboardExe -ArgumentList '--tray' -WorkingDirectory $PayloadDirectory -PassThru
        Wait-Until {$repeat.HasExited} 5 'repeated Dashboard launch did not return'
    }
    Assert-True (@(Get-Process -Name 'EmilyDesk' -ErrorAction Stop).Count -eq 1) 'repeated launches retain one Dashboard tray owner'
    $trayRunLog=@(Get-Content $dashboardLogPath | Select-Object -Skip $trayLogBaseline)
    Assert-True (@($trayRunLog | Where-Object {$_ -match 'Dashboard tray icon initialized'}).Count -eq 1) 'repeated launches initialize exactly one live tray icon owner'

    @('native.clock','native.calendar','native.weather','native.recyclebin') | ForEach-Object { Send-WidgetCommand 'open' $_ }
    Wait-Until { ((Read-Status).widgets -split ',' | Where-Object { $_ }).Count -eq 4 } 10 'four native widgets did not open'
    $open = @((Read-Status).widgets -split ',' | Sort-Object -Unique)
    Assert-True (($open -join ',') -eq 'native.calendar,native.clock,native.recyclebin,native.weather') 'Clock, Calendar, Weather, and Recycle Bin run simultaneously without duplicates'
    Assert-True (-not ((Read-Status).availableWidgets -match '(^|;)legacy\.')) 'Engine catalog contains no legacy XWidget runtime entries'
    $savedPositions = @{}
    foreach ($widgetId in @('native.clock','native.calendar','native.weather','native.recyclebin')) {
        $positionPath = Join-Path $stateDirectory ($widgetId + '.position')
        Assert-True (Test-Path -LiteralPath $positionPath -PathType Leaf) ($widgetId + ' saved position exists')
        $savedPositions[$widgetId] = Get-Content -LiteralPath $positionPath -Raw
    }

    $show = Start-Process -FilePath $dashboardExe -WorkingDirectory $PayloadDirectory -PassThru
    Wait-Until { $show.HasExited } 5 'single-instance Dashboard activation did not return'
    Start-Sleep -Seconds 1
    $dashboard.Refresh()
    $engineProcess = Get-Process -Name 'EmilyDesk.Engine' -ErrorAction Stop | Select-Object -First 1
    $normalWidgetWindows = @([EmilyDeskWindowOrder]::VisibleWindowsForProcess($engineProcess.Id, $false))
    $zOrder = @([EmilyDeskWindowOrder]::VisibleWindowsInZOrder())
    $dashboardIndex = [Array]::IndexOf($zOrder, [IntPtr]$dashboard.MainWindowHandle)
    Assert-True ($dashboardIndex -ge 0) 'Dashboard is present in the visible window z-order'
    Assert-True ($normalWidgetWindows.Count -eq 3) 'three restored normal-layer widget windows are visible'
    foreach ($widgetWindow in $normalWidgetWindows) {
        Assert-True ([Array]::IndexOf($zOrder, $widgetWindow) -gt $dashboardIndex) 'Dashboard is above each restored normal-layer widget'
    }
    if ($dashboard.MainWindowHandle -ne 0) { $dashboard.CloseMainWindow() | Out-Null }
    Start-Sleep -Seconds 1
    Assert-True (-not $dashboard.HasExited) 'ordinary Dashboard close keeps the tray process alive'
    Assert-True (((Read-Status).widgets -split ',').Count -eq 3) 'ordinary Dashboard close keeps active widgets alive'
    1..3 | ForEach-Object {
        $reopen=Start-Process -FilePath $dashboardExe -WorkingDirectory $PayloadDirectory -PassThru
        Wait-Until {$reopen.HasExited} 5 'Dashboard reopen signal did not return'
        Start-Sleep -Milliseconds 300
        $dashboard.Refresh()
        if($dashboard.MainWindowHandle -ne 0){$dashboard.CloseMainWindow() | Out-Null}
    }
    Assert-True (@(Get-Process -Name 'EmilyDesk' -ErrorAction Stop).Count -eq 1) 'Dashboard close and reopen retain one tray owner process'
    $trayRunLog=@(Get-Content $dashboardLogPath | Select-Object -Skip $trayLogBaseline)
    Assert-True (@($trayRunLog | Where-Object {$_ -match 'Dashboard tray icon initialized'}).Count -eq 1) 'Dashboard close and reopen do not initialize additional tray icons'

    Stop-Process -Id $firstEnginePid -Force
    Wait-Until { -not (Get-Process -Id $firstEnginePid -ErrorAction SilentlyContinue) } 5 'forced Engine crash did not complete'
    $recover = Start-Process -FilePath $dashboardExe -WorkingDirectory $PayloadDirectory -PassThru
    Wait-Until { $recover.HasExited } 5 'Dashboard recovery activation did not return'
    Wait-Until { $status = Read-Status; $status.pid -and [int]$status.pid -ne $firstEnginePid -and (($status.widgets -split ',').Count -eq 3) } 12 'Engine crash recovery did not restore all native widgets'
    Assert-True (@(Get-Process -Name 'EmilyDesk.Engine' -ErrorAction Stop).Count -eq 1) 'crash recovery leaves one Engine instance'
    Assert-True (((Read-Status).widgets -split ',' | Sort-Object -Unique).Count -eq 3) 'restart restores intended widgets without duplicates'
    foreach ($widgetId in $savedPositions.Keys) {
        $positionPath = Join-Path $stateDirectory ($widgetId + '.position')
        Assert-True ((Get-Content -LiteralPath $positionPath -Raw) -eq $savedPositions[$widgetId]) ($widgetId + ' position survives restart')
    }

    Signal-Event 'Local\XWidgetRebornExitDashboard'
    Wait-Until { -not (Get-Process -Id $dashboard.Id -ErrorAction SilentlyContinue) -and -not (Get-Process -Name 'EmilyDesk.Engine' -ErrorAction SilentlyContinue) } 12 'coordinated tray exit left Dashboard or Engine running'
    Assert-True (-not (Get-Process -Name 'EmilyDesk','EmilyDesk.Engine' -ErrorAction SilentlyContinue)) 'coordinated exit leaves no EmilyDesk processes'
    $trayRunLog=@(Get-Content $dashboardLogPath | Select-Object -Skip $trayLogBaseline)
    Assert-True (@($trayRunLog | Where-Object {$_ -match 'Dashboard tray icon cleanup completed'}).Count -eq 1) 'normal Exit deterministically cleans the tray icon once'

    Set-TestLayer 'native.clock' 'AlwaysOnTop'
    Set-TestLayer 'native.calendar' 'NormalDesktop'
    Set-TestLayer 'native.weather' 'NormalDesktop'
    $mixedDashboard = Start-Process -FilePath $dashboardExe -WorkingDirectory $PayloadDirectory -PassThru
    Wait-Until {
        $mixedDashboard.Refresh()
        $mixedEngine = Get-Process -Name 'EmilyDesk.Engine' -ErrorAction SilentlyContinue | Select-Object -First 1
        $mixedEngine -and $mixedDashboard.MainWindowHandle -ne 0 -and
            ((Read-Status).widgets -split ',' | Where-Object { $_ }).Count -eq 3
    } 12 'mixed per-widget layer restart did not become ready'
    Start-Sleep -Seconds 1
    $mixedDashboard.Refresh()
    $mixedEngine = Get-Process -Name 'EmilyDesk.Engine' -ErrorAction Stop | Select-Object -First 1
    $mixedNormal = @([EmilyDeskWindowOrder]::VisibleWindowsForProcess($mixedEngine.Id, $false))
    $mixedTopMost = @([EmilyDeskWindowOrder]::VisibleWindowsForProcess($mixedEngine.Id, $true))
    $mixedZOrder = @([EmilyDeskWindowOrder]::VisibleWindowsInZOrder())
    $mixedDashboardIndex = [Array]::IndexOf($mixedZOrder, [IntPtr]$mixedDashboard.MainWindowHandle)
    Assert-True ($mixedNormal.Count -eq 2) 'two Desktop Layer widgets restore as normal windows'
    Assert-True ($mixedTopMost.Count -eq 1) 'one independently selected Always on Top widget restores topmost'
    foreach ($widgetWindow in $mixedNormal) {
        Assert-True ([Array]::IndexOf($mixedZOrder, $widgetWindow) -gt $mixedDashboardIndex) 'Desktop Layer widget restores behind Dashboard'
    }
    Assert-True ([Array]::IndexOf($mixedZOrder, $mixedTopMost[0]) -lt $mixedDashboardIndex) 'Always on Top widget restores above Dashboard'
    Signal-Event 'Local\XWidgetRebornExitDashboard'
    Wait-Until { -not (Get-Process -Id $mixedDashboard.Id -ErrorAction SilentlyContinue) -and -not (Get-Process -Name 'EmilyDesk.Engine' -ErrorAction SilentlyContinue) } 12 'mixed-layer test cleanup left EmilyDesk running'
    $trayRunLog=@(Get-Content $dashboardLogPath | Select-Object -Skip $trayLogBaseline)
    Assert-True (@($trayRunLog | Where-Object {$_ -match 'Dashboard tray icon initialized'}).Count -eq 2) 'restart creates one replacement tray icon owner'
    Assert-True (@($trayRunLog | Where-Object {$_ -match 'Dashboard tray icon cleanup completed'}).Count -eq 2) 'restart and second Exit clean each tray owner exactly once'
    Write-Host 'STAGE 2 LIFECYCLE TESTS PASSED'
}
finally {
    Stop-TestProcesses
    if (Test-Path -LiteralPath $stateDirectory) { Remove-Item -LiteralPath $stateDirectory -Recurse -Force }
    if (Test-Path -LiteralPath $stateBackup) { Copy-Item -LiteralPath $stateBackup -Destination $stateDirectory -Recurse -Force }
    if (Test-Path -LiteralPath $backupRoot) { Remove-Item -LiteralPath $backupRoot -Recurse -Force }
}
