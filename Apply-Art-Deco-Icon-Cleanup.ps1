$ErrorActionPreference = "Stop"

$source = Join-Path $PSScriptRoot "Assets\WeatherIconPacks\ArtDeco"
$names = @("3.png", "4.png", "6.png", "11.png", "32.png")

foreach ($name in $names) {
    $path = Join-Path $source $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Repair asset is missing: $path"
    }
}

$candidates = @(
    (Join-Path $env:LOCALAPPDATA "EmilyDesk\Assets\WeatherIconPacks\ArtDeco"),
    (Join-Path $env:ProgramFiles "EmilyDesk\Assets\WeatherIconPacks\ArtDeco")
)
if (${env:ProgramFiles(x86)}) {
    $candidates += Join-Path ${env:ProgramFiles(x86)} "EmilyDesk\Assets\WeatherIconPacks\ArtDeco"
}

$destination = $candidates | Where-Object {
    Test-Path -LiteralPath $_ -PathType Container
} | Select-Object -First 1

if (-not $destination) {
    throw "No installed EmilyDesk Art Deco icon folder was found. EmilyDesk was not changed."
}

foreach ($name in $names) {
    Copy-Item -LiteralPath (Join-Path $source $name) `
        -Destination (Join-Path $destination $name) -Force
}

Write-Host "Updated five Art Deco weather icon files in:"
Write-Host $destination
