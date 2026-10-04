$ErrorActionPreference = "Stop"
# Compile the exact production geometry helper with its regression tests.
# No widget is launched and no user settings or desktop positions are changed.
Add-Type -AssemblyName System.Drawing
Add-Type -Path @(
    (Join-Path $PSScriptRoot "src\XWidgetReborn.Runtime\Core\NatureWeatherPlacement.cs"),
    (Join-Path $PSScriptRoot "Tests\NatureWeatherPlacementRegression.cs")
) -ReferencedAssemblies ([System.Drawing.Point].Assembly.Location)
$checks = [EmilyDesk.Tests.NatureWeatherPlacementRegression]::Run()
"PASS: $checks Nature weather placement checks (click, detach, reattach, scale, DPI, monitors)."
