param(
    [string]$LocationId = "54704",
    [switch]$OpenReport
)

$ErrorActionPreference = "Stop"

$diagnosticsDir = Join-Path $env:LOCALAPPDATA "XWidget Reborn"
New-Item $diagnosticsDir -ItemType Directory -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $diagnosticsDir "compatibility-audit-$timestamp.txt"
$latestPath = Join-Path $diagnosticsDir "compatibility-audit-latest.txt"

function Invoke-JsonEndpoint {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$Uri
    )

    try {
        $response = Invoke-WebRequest `
            -UseBasicParsing `
            -Uri $Uri `
            -TimeoutSec 10

        return [pscustomobject]@{
            Name = $Name
            Uri = $Uri
            StatusCode = [int]$response.StatusCode
            Content = $response.Content
            Error = $null
        }
    }
    catch {
        return [pscustomobject]@{
            Name = $Name
            Uri = $Uri
            StatusCode = 0
            Content = $null
            Error = $_.Exception.Message
        }
    }
}

function Add-Check {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Label,
        [bool]$Passed,
        [string]$Detail
    )

    $state = if ($Passed) { "PASS" } else { "FAIL" }
    $Lines.Add(("{0}: {1} - {2}" -f $state, $Label, $Detail))
}

$lines = New-Object 'System.Collections.Generic.List[string]'
$lines.Add("EmilyDesk 2.10.27 Weather Service Audit")
$lines.Add("Generated: $([DateTime]::UtcNow.ToString('o')) UTC")
$lines.Add("Location ID: $LocationId")
$lines.Add("")

$statusEndpoint = Invoke-JsonEndpoint `
    -Name "Status" `
    -Uri "http://127.0.0.1:45873/xwidgetbridge/status.json"

$currentEndpoint = Invoke-JsonEndpoint `
    -Name "Current conditions" `
    -Uri ("http://127.0.0.1:45873/currentconditions/v1/{0}.json?apikey=xwidget&details=true&language=en" -f $LocationId)

$forecastEndpoint = Invoke-JsonEndpoint `
    -Name "Five-day forecast" `
    -Uri ("http://127.0.0.1:45873/forecasts/v1/daily/5day/{0}.json?apikey=xwidget&details=true&metric=true&language=en" -f $LocationId)

$locationEndpoint = Invoke-JsonEndpoint `
    -Name "Location lookup" `
    -Uri ("http://127.0.0.1:45873/locations/v1/{0}.json?apikey=xwidget&language=en" -f $LocationId)

$lines.Add("Endpoint checks")
$lines.Add("---------------")

foreach ($endpoint in @(
    $statusEndpoint,
    $currentEndpoint,
    $forecastEndpoint,
    $locationEndpoint
)) {
    Add-Check `
        -Lines $lines `
        -Label $endpoint.Name `
        -Passed ($endpoint.StatusCode -eq 200) `
        -Detail (
            if ($endpoint.StatusCode -eq 200) {
                "HTTP 200"
            } else {
                "HTTP $($endpoint.StatusCode); $($endpoint.Error)"
            }
        )
}

$lines.Add("")
$lines.Add("Compatibility field checks")
$lines.Add("--------------------------")

if ($statusEndpoint.Content) {
    try {
        $status = $statusEndpoint.Content | ConvertFrom-Json
        Add-Check $lines "Service status field" `
            ($null -ne $status.status) `
            ("status=" + $status.status)
        Add-Check $lines "Provider field" `
            ($null -ne $status.provider) `
            ("provider=" + $status.provider)
    }
    catch {
        Add-Check $lines "Status JSON parsing" $false $_.Exception.Message
    }
}

if ($currentEndpoint.Content) {
    try {
        $currentArray = $currentEndpoint.Content | ConvertFrom-Json
        $current = @($currentArray)[0]

        $currentChecks = [ordered]@{
            "WeatherText" = $current.WeatherText
            "WeatherIcon" = $current.WeatherIcon
            "IsDayTime" = $current.IsDayTime
            "Temperature.Metric.Value" = $current.Temperature.Metric.Value
            "Temperature.Metric.Unit" = $current.Temperature.Metric.Unit
            "RelativeHumidity" = $current.RelativeHumidity
            "Wind.Speed.Metric.Value" = $current.Wind.Speed.Metric.Value
        }

        foreach ($entry in $currentChecks.GetEnumerator()) {
            Add-Check `
                $lines `
                ("Current field " + $entry.Key) `
                ($null -ne $entry.Value -and "$($entry.Value)" -ne "") `
                ("value=" + $entry.Value)
        }
    }
    catch {
        Add-Check $lines "Current JSON parsing" $false $_.Exception.Message
    }
}

if ($forecastEndpoint.Content) {
    try {
        $forecast = $forecastEndpoint.Content | ConvertFrom-Json
        $days = @($forecast.DailyForecasts)

        Add-Check $lines "Five forecast days" `
            ($days.Count -ge 5) `
            ("count=" + $days.Count)

        $epochDates = @($days | ForEach-Object { $_.EpochDate })
        $uniqueEpochDates = @($epochDates | Sort-Object -Unique)
        $epochsIncrease = $true
        for ($index = 1; $index -lt $epochDates.Count; $index++) {
            if ([long]$epochDates[$index] -le [long]$epochDates[$index - 1]) {
                $epochsIncrease = $false
                break
            }
        }
        Add-Check $lines "Distinct forecast weekdays" `
            ($epochDates.Count -ge 5 -and $uniqueEpochDates.Count -eq $epochDates.Count -and $epochsIncrease) `
            ("unique EpochDate values=" + $uniqueEpochDates.Count)

        if ($days.Count -gt 0) {
            $day = $days[0]
            $forecastChecks = [ordered]@{
                "Temperature.Minimum.Value" = $day.Temperature.Minimum.Value
                "Temperature.Maximum.Value" = $day.Temperature.Maximum.Value
                "Day.Icon" = $day.Day.Icon
                "Day.IconPhrase" = $day.Day.IconPhrase
                "Night.Icon" = $day.Night.Icon
                "Night.IconPhrase" = $day.Night.IconPhrase
                "Sun.Rise" = $day.Sun.Rise
                "Sun.Set" = $day.Sun.Set
            }

            foreach ($entry in $forecastChecks.GetEnumerator()) {
                Add-Check `
                    $lines `
                    ("Forecast field " + $entry.Key) `
                    ($null -ne $entry.Value -and "$($entry.Value)" -ne "") `
                    ("value=" + $entry.Value)
            }
        }
    }
    catch {
        Add-Check $lines "Forecast JSON parsing" $false $_.Exception.Message
    }
}

$failures = @($lines | Where-Object { $_ -like "FAIL:*" }).Count
$passes = @($lines | Where-Object { $_ -like "PASS:*" }).Count

$lines.Add("")
$lines.Add("Summary")
$lines.Add("-------")
$lines.Add("Passed checks: $passes")
$lines.Add("Failed checks: $failures")
$lines.Add(
    if ($failures -eq 0) {
        "RESULT: COMPATIBILITY AUDIT PASSED"
    } else {
        "RESULT: COMPATIBILITY AUDIT NEEDS ATTENTION"
    }
)

$lines | Set-Content -Path $reportPath -Encoding UTF8
Copy-Item $reportPath $latestPath -Force

Write-Host ""
$lines | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "Report: $reportPath"

if ($OpenReport) {
    Start-Process notepad.exe $reportPath
}

if ($failures -eq 0) {
    exit 0
}

exit 1
