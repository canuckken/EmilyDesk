param(
    [switch]$SkipInstaller,
    [switch]$FullVerification,
    [string]$SignCertificateThumbprint = $env:EMILYDESK_SIGN_CERT_SHA1,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing

$version = "2.10.31"
$logs = Join-Path $PSScriptRoot "BuildLogs"
$stage = Join-Path $PSScriptRoot "Stage"
$output = Join-Path $PSScriptRoot "Output"
$packageOutput = Join-Path $output "Packages"
$themePackageOutput = Join-Path $output "Themes"
$optionalWidgetPackageOutput = Join-Path $output "OptionalWidgets"

$tempRoot = Join-Path $PSScriptRoot (
    "EmilyDeskBuild-" + [Guid]::NewGuid().ToString("N")
)
$installerOutput = Join-Path $tempRoot "Installer"
$tempFull = Join-Path $tempRoot "Full-Payload"
$tempDashboard = Join-Path $tempRoot "Dashboard-Portable"

Remove-Item $logs, $stage, $output -Recurse -Force -ErrorAction SilentlyContinue
New-Item $logs, $stage, $output, $installerOutput, $packageOutput, `
    $themePackageOutput, $optionalWidgetPackageOutput, `
    $tempFull, $tempDashboard `
    -ItemType Directory -Force | Out-Null

function Find-MSBuild {
    # Prefer 32-bit MSBuild for this AnyCPU/.NET Framework 4.0 solution.
    # The previous 64-bit-first order caused noisy AMD64/MSIL warnings and
    # could resolve framework assemblies from the wrong architecture.
    $items = @(
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe",
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
    )
    return $items | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function Write-BuildFailureSummary {
    param([Parameter(Mandatory=$true)][string]$LogPath)

    $summaryPath = Join-Path $logs "build-errors.txt"
    $errors = @()

    if (Test-Path -LiteralPath $LogPath) {
        $errors = Get-Content -LiteralPath $LogPath | Where-Object {
            $_ -match '(:|\s)error (CS|MSB)[0-9]+' -or
            $_ -match ': error '
        }
    }

    if ($errors.Count -eq 0) {
        "MSBuild failed, but no standard compiler error line was detected." |
            Set-Content -LiteralPath $summaryPath -Encoding UTF8
    } else {
        $errors | Set-Content -LiteralPath $summaryPath -Encoding UTF8
    }

    Write-Host ""
    Write-Host "BUILD ERROR SUMMARY" -ForegroundColor Red
    Write-Host "-------------------" -ForegroundColor Red
    Get-Content -LiteralPath $summaryPath | ForEach-Object { Write-Host $_ }
    Write-Host ""
    Write-Host "A copy was saved to BuildLogs\build-errors.txt"
}

function Find-InnoSetup {
    $items = @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )
    foreach ($candidate in $items) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    $roots = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )
    foreach ($entry in (Get-ItemProperty $roots -ErrorAction SilentlyContinue)) {
        if ($entry.DisplayName -match 'Inno Setup' -and $entry.InstallLocation) {
            $candidate = Join-Path $entry.InstallLocation 'ISCC.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    return $null
}

function Find-SignTool {
    $command = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $kitsRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (Test-Path -LiteralPath $kitsRoot -PathType Container) {
        $candidate = Get-ChildItem -LiteralPath $kitsRoot -Directory |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "x64\signtool.exe" } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1
        if ($candidate) { return $candidate }
    }
    return $null
}

function Sign-ReleaseFile {
    param([Parameter(Mandatory=$true)][string]$Path)
    if ([string]::IsNullOrWhiteSpace($SignCertificateThumbprint)) { return }
    if (-not $script:SignToolPath) {
        $script:SignToolPath = Find-SignTool
        if (-not $script:SignToolPath) {
            throw "A signing certificate was requested, but signtool.exe was not found."
        }
    }
    & $script:SignToolPath sign /sha1 $SignCertificateThumbprint.Trim() `
        /fd SHA256 /tr $TimestampUrl /td SHA256 $Path
    if ($LASTEXITCODE -ne 0) { throw "Authenticode signing failed: $Path" }
    & $script:SignToolPath verify /pa $Path | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Authenticode verification failed: $Path" }
}

function Assert-File {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description is missing: $Path"
    }
}

function Copy-FileWithRetry {
    param(
        [Parameter(Mandatory=$true)][string]$Source,
        [Parameter(Mandatory=$true)][string]$Destination,
        [int]$Attempts = 12,
        [int]$DelayMilliseconds = 500
    )

    if (-not [System.IO.Path]::IsPathRooted($Source)) {
        $Source = Join-Path $PSScriptRoot $Source
    }
    Assert-File $Source "Source file"

    $destinationDirectory = Split-Path -Parent $Destination
    if ($destinationDirectory) {
        New-Item $destinationDirectory -ItemType Directory -Force | Out-Null
    }

    $lastError = $null
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            [System.IO.File]::Copy($Source, $Destination, $true)
            Assert-File $Destination "Copied file"
            return
        } catch {
            $lastError = $_
            if ($attempt -lt $Attempts) {
                Start-Sleep -Milliseconds $DelayMilliseconds
            }
        }
    }

    throw (
        "Unable to copy the file after $Attempts attempts." +
        [Environment]::NewLine +
        "Source: $Source" +
        [Environment]::NewLine +
        "Destination: $Destination" +
        [Environment]::NewLine +
        "Last error: $($lastError.Exception.Message)"
    )
}

function Copy-DirectorySnapshot {
    param(
        [Parameter(Mandatory=$true)][string]$SourceDirectory,
        [Parameter(Mandatory=$true)][string]$DestinationDirectory
    )

    if (-not [System.IO.Path]::IsPathRooted($SourceDirectory)) {
        $SourceDirectory = Join-Path $PSScriptRoot $SourceDirectory
    }
    if (-not (Test-Path -LiteralPath $SourceDirectory -PathType Container)) {
        throw "Source directory is missing: $SourceDirectory"
    }

    $sourceRoot = (Resolve-Path -LiteralPath $SourceDirectory).Path.TrimEnd('\')
    Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($sourceRoot.Length).TrimStart('\')
        $destination = Join-Path $DestinationDirectory $relative
        Copy-FileWithRetry $_.FullName $destination
    }
}

function Create-ZipFromSnapshot {
    param(
        [Parameter(Mandatory=$true)][string]$SnapshotDirectory,
        [Parameter(Mandatory=$true)][string]$DestinationZip
    )

    if (Test-Path -LiteralPath $DestinationZip) {
        Remove-Item -LiteralPath $DestinationZip -Force
    }

    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $SnapshotDirectory,
        $DestinationZip,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false
    )

    Assert-File $DestinationZip "Completed ZIP package"
}


function New-DashboardVerificationRunner {
    param(
        [Parameter(Mandatory=$true)][string]$StageDirectory,
        [Parameter(Mandatory=$true)][string]$DestinationDirectory
    )

    if (Test-Path -LiteralPath $DestinationDirectory) {
        Remove-Item -LiteralPath $DestinationDirectory -Recurse -Force
    }
    New-Item $DestinationDirectory -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $StageDirectory '*') $DestinationDirectory `
        -Recurse -Force

    $runner = Join-Path $DestinationDirectory 'EmilyDesk.exe'
    Assert-File $runner 'Dashboard verification runner'
    return [IO.Path]::GetFullPath($runner)
}

function Invoke-WidgetPackageInstallVerification {
    param(
        [Parameter(Mandatory=$true)][string]$DashboardPath,
        [Parameter(Mandatory=$true)][string]$PackagePath,
        [Parameter(Mandatory=$true)][string]$WidgetsRoot
    )

    New-Item $WidgetsRoot -ItemType Directory -Force | Out-Null
    $arguments = (
        '--verify-widget-package-install "{0}" "{1}"' -f `
            $PackagePath, $WidgetsRoot
    )
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $DashboardPath
    $startInfo.Arguments = $arguments
    $startInfo.WorkingDirectory = Split-Path -Parent $DashboardPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.EnvironmentVariables["EMILYDESK_THEME_ROOT"] = `
        (Join-Path $PSScriptRoot "ThemePackages")

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Dashboard package verification process could not be started."
    }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        $errorPath = Join-Path $WidgetsRoot "install-verification-error.txt"
        if (Test-Path -LiteralPath $errorPath) {
            throw "Dashboard rejected the widget package: $(
                Get-Content -LiteralPath $errorPath -Raw
            )"
        }
        throw "Dashboard rejected the widget package with exit code $($process.ExitCode)."
    }
}

function Invoke-GalleryPreviewGeneration {
    param(
        [Parameter(Mandatory=$true)][string]$DashboardPath,
        [Parameter(Mandatory=$true)][string]$DestinationDirectory
    )

    Assert-File $DashboardPath "Gallery preview renderer"
    New-Item $DestinationDirectory -ItemType Directory -Force | Out-Null
    $errorPath = Join-Path (Split-Path -Parent $DashboardPath) `
        "gallery-preview-error.txt"
    Remove-Item -LiteralPath $errorPath -Force -ErrorAction SilentlyContinue

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $DashboardPath
    $startInfo.Arguments = (
        '--generate-gallery-previews "{0}"' -f $DestinationDirectory
    )
    $startInfo.WorkingDirectory = Split-Path -Parent $DashboardPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Gallery preview rendering could not be started."
    }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $errorPath) {
            throw "Gallery preview rendering failed: $(
                Get-Content -LiteralPath $errorPath -Raw
            )"
        }
        throw "Gallery preview rendering failed with exit code $($process.ExitCode)."
    }

    $expectedThemes = @(
        "modern", "vintage", "steampunk", "industrial",
        "art-deco", "botanical-nature", "woodland-nature",
        "ember-glow"
    )
    $expectedWidgets = @("calendar", "clock", "recycle-bin", "weather")
    foreach ($theme in $expectedThemes) {
        foreach ($widget in $expectedWidgets) {
            $path = Join-Path $DestinationDirectory `
                ($theme + "-" + $widget + ".png")
            Assert-File $path "Generated Gallery preview"
        }
    }
    $previewCount = @(
        Get-ChildItem -LiteralPath $DestinationDirectory -File `
            -Filter "*.png"
    ).Count
    if ($previewCount -ne 32) {
        throw "Expected 32 generated Gallery previews, found $previewCount."
    }
}

function Invoke-LegacyWidgetPackageVerification {
    param(
        [Parameter(Mandatory=$true)][string]$DashboardPath,
        [Parameter(Mandatory=$true)][string]$PackagePath,
        [Parameter(Mandatory=$true)][string]$LibraryRoot
    )

    New-Item $LibraryRoot -ItemType Directory -Force | Out-Null
    $arguments = (
        '--verify-legacy-widget-package "{0}" "{1}"' -f `
            $PackagePath, $LibraryRoot
    )
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $DashboardPath
    $startInfo.Arguments = $arguments
    $startInfo.WorkingDirectory = Split-Path -Parent $DashboardPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Dashboard legacy package verification could not be started."
    }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        $errorPath = Join-Path $LibraryRoot `
            "legacy-install-verification-error.txt"
        if (Test-Path -LiteralPath $errorPath) {
            throw "Dashboard rejected the legacy widget package: $(
                Get-Content -LiteralPath $errorPath -Raw
            )"
        }
        throw "Dashboard rejected the legacy widget package with exit code $(
            $process.ExitCode
        )."
    }
}

try {
    & (Join-Path $PSScriptRoot "Verify-NatureWeatherPlacement.ps1") |
        Tee-Object -FilePath (Join-Path $logs "nature-weather-placement-tests.txt") |
        ForEach-Object { Write-Host $_ }
    Write-Host "[1/8] Building EmilyDesk $version..."
    $msbuild = Find-MSBuild
    if (-not $msbuild) { throw "MSBuild could not be found." }
    Write-Host "Using MSBuild: $msbuild"

    # Remove stale compiler output before rebuilding. This prevents an old DLL
    # from being packaged when a project fails before producing a replacement.
    Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot "src") -Directory |
        ForEach-Object {
            Remove-Item (Join-Path $_.FullName "bin"), (Join-Path $_.FullName "obj") `
                -Recurse -Force -ErrorAction SilentlyContinue
        }

    $msbuildLog = Join-Path $logs "msbuild.log"
    & $msbuild "EmilyDesk.sln" /t:Rebuild /p:Configuration=Release `
        /p:Platform="Any CPU" /nologo /verbosity:minimal 2>&1 |
        Tee-Object $msbuildLog

    if ($LASTEXITCODE -ne 0) {
        Write-BuildFailureSummary -LogPath $msbuildLog
        throw "MSBuild failed. See BuildLogs\build-errors.txt."
    }

    Write-Host "[1/8] Verifying 21 editable widgets and 28 template project routes..."
    $designerDirectory = Join-Path $PSScriptRoot "src\EmilyDesk.Designer\bin\Release"
    $designerCheck = Start-Process -FilePath (Join-Path $designerDirectory "EmilyDesk.Designer.exe") `
        -ArgumentList "--verify-designer-saves" -WorkingDirectory $designerDirectory -PassThru -Wait
    if ($designerCheck.ExitCode -ne 0) {
        $designerError = Join-Path $designerDirectory "designer-save-error.txt"
        if (Test-Path -LiteralPath $designerError) {
            Copy-Item $designerError (Join-Path $logs "designer-save-error.txt") -Force
            Get-Content -LiteralPath $designerError | Write-Host
        }
        throw "Designer save/apply verification failed. See BuildLogs\designer-save-error.txt."
    }
    Copy-Item (Join-Path $designerDirectory "designer-save-tests.txt") `
        (Join-Path $logs "designer-save-tests.txt") -Force
    Write-Host "[1/8] Rendering and validating current Gallery previews..."
    Invoke-GalleryPreviewGeneration `
        -DashboardPath (Join-Path $PSScriptRoot `
            "src\XWidgetReborn.Dashboard\bin\Release\EmilyDesk.exe") `
        -DestinationDirectory (Join-Path $PSScriptRoot `
            "Widgets\GalleryPreviews")

    $files = [ordered]@{
        "EmilyDesk.exe" =
            "src\XWidgetReborn.Dashboard\bin\Release\EmilyDesk.exe"
        "EmilyDesk.Engine.exe" =
            "src\XWidgetReborn.Runtime\bin\Release\EmilyDesk.Engine.exe"
        "EmilyDesk.Service.exe" =
            "src\XWidgetReborn.Service\bin\Release\EmilyDesk.Service.exe"
        "EmilyDesk.SetupHelper.exe" =
            "src\XWidgetReborn.SetupHelper\bin\Release\EmilyDesk.SetupHelper.exe"
        "EmilyDesk.Designer.exe" =
            "src\EmilyDesk.Designer\bin\Release\EmilyDesk.Designer.exe"
        "XWidgetReborn.Shared.dll" =
            "src\XWidgetReborn.Shared\bin\Release\XWidgetReborn.Shared.dll"
        "XWidgetReborn.WidgetSdk.dll" =
            "src\XWidgetReborn.WidgetSdk\bin\Release\XWidgetReborn.WidgetSdk.dll"
        "XWidgetReborn.Widgets.Weather.dll" =
            "src\XWidgetReborn.Widgets.Weather\bin\Release\XWidgetReborn.Widgets.Weather.dll"
        "XWidgetReborn.Widgets.Clock.dll" =
            "src\XWidgetReborn.Widgets.Clock\bin\Release\XWidgetReborn.Widgets.Clock.dll"
        "XWidgetReborn.Widgets.Calendar.dll" =
            "src\XWidgetReborn.Widgets.Calendar\bin\Release\XWidgetReborn.Widgets.Calendar.dll"
        "XWidgetReborn.Widgets.RecycleBin.dll" =
            "src\XWidgetReborn.Widgets.RecycleBin\bin\Release\XWidgetReborn.Widgets.RecycleBin.dll"
        "XWidgetReborn.WeatherCore.dll" =
            "src\XWidgetReborn.WeatherCore\bin\Release\XWidgetReborn.WeatherCore.dll"
    }

    if ($env:EMILYDESK_REQUIRE_SIGNATURE -eq "1" -and
        [string]::IsNullOrWhiteSpace($SignCertificateThumbprint)) {
        throw "This release requires EMILYDESK_SIGN_CERT_SHA1 to name a trusted code-signing certificate."
    }
    if (-not [string]::IsNullOrWhiteSpace($SignCertificateThumbprint)) {
        Write-Host "[1/8] Authenticode-signing application binaries..."
        foreach ($entry in $files.GetEnumerator()) {
            Sign-ReleaseFile (Join-Path $PSScriptRoot $entry.Value)
        }
    }

    # Generate package artwork from the native renderer, never from desktop
    # screenshots.  The Clock export uses a fixed preview time and RGBA output.
    $designerExporter = Join-Path $PSScriptRoot "src\EmilyDesk.Designer\bin\Release\EmilyDesk.Designer.exe"
    & $designerExporter --export-clock-package-artwork $PSScriptRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Clock package artwork export failed."
    }

    Write-Host "[2/8] Staging and hashing required components..."
    $manifest = @(
        "Version=$version",
        "BuiltUTC=$([DateTime]::UtcNow.ToString('o'))"
    )

    foreach ($entry in $files.GetEnumerator()) {
        $source = Join-Path $PSScriptRoot $entry.Value
        $destination = Join-Path $stage $entry.Key

        Copy-FileWithRetry $source $destination

        $item = Get-Item -LiteralPath $destination
        $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if ($sourceHash -ne $hash) {
            throw "Staged component does not match its freshly rebuilt source: $($entry.Key)"
        }
        $manifest += "$($entry.Key).SizeBytes=$($item.Length)"
        $manifest += "$($entry.Key).SourceSHA256=$sourceHash"
        $manifest += "$($entry.Key).SHA256=$hash"
    }

    $nativeWidgetPackages = @()
    $widgetSources = Get-ChildItem (Join-Path $PSScriptRoot "Widgets") `
        -Directory
    foreach ($widgetSource in $widgetSources) {
        $widgetManifestPath = Join-Path $widgetSource.FullName "manifest.json"
        if (-not (Test-Path -LiteralPath $widgetManifestPath -PathType Leaf)) {
            continue
        }

        $widgetManifest = Get-Content -LiteralPath $widgetManifestPath -Raw |
            ConvertFrom-Json
        foreach ($field in @(
            "id", "name", "version", "author", "description",
            "assembly", "type"
        )) {
            if ([string]::IsNullOrWhiteSpace($widgetManifest.$field)) {
                throw "$widgetManifestPath requires '$field'."
            }
        }

        $assemblyName = [IO.Path]::GetFileName($widgetManifest.assembly)
        # Project references copy dependency DLLs into their own output folders.
        # Resolve the widget's authoritative project output instead of counting
        # those harmless dependency copies as additional widget assemblies.
        $assemblyProject = [IO.Path]::GetFileNameWithoutExtension($assemblyName)
        $authoritativeAssembly = Join-Path $PSScriptRoot `
            "src\$assemblyProject\bin\Release\$assemblyName"
        $assemblyCandidates = @()
        if (Test-Path -LiteralPath $authoritativeAssembly -PathType Leaf) {
            $assemblyCandidates = @(
                Get-Item -LiteralPath $authoritativeAssembly
            )
        }
        if ($assemblyCandidates.Count -ne 1) {
            throw "Expected one Release assembly for widget '$(
                $widgetManifest.id
            )', found $($assemblyCandidates.Count)."
        }

        $widgetStage = Join-Path (Join-Path $stage "Widgets") `
            $widgetSource.Name
        Copy-DirectorySnapshot $widgetSource.FullName $widgetStage
        Copy-FileWithRetry $assemblyCandidates[0].FullName `
            (Join-Path $widgetStage $widgetManifest.assembly)

        $packageBaseName = $widgetManifest.name -replace '[^A-Za-z0-9]', ''
        if ([string]::IsNullOrWhiteSpace($packageBaseName)) {
            $packageBaseName = $widgetManifest.id -replace '[^A-Za-z0-9]', ''
        }
        $nativeWidgetPackages += [PSCustomObject]@{
            Id = $widgetManifest.id
            Name = $widgetManifest.name
            Manifest = $widgetManifest
            StageDirectory = $widgetStage
            PackagePath = Join-Path $packageOutput `
                ($packageBaseName + ".xwrwidget")
        }
    }
    if ($nativeWidgetPackages.Count -eq 0) {
        throw "No native widget manifests were found under Widgets."
    }

    # Optional widgets are compiled with the solution, but deliberately staged
    # outside the installer payload. Each one becomes a self-contained
    # .emilywidget file that users may import or remove independently.
    $optionalWidgetPackages = @()
    # Only widgets that have completed their visual and functional review are
    # published. Other prototype projects may remain in the solution without
    # appearing in Output\OptionalWidgets.
    $approvedOptionalWidgetIds = @(
        "utility.calculator",
        "utility.system-info",
        "utility.currency-converter",
        "utility.industrial-calculator",
        "utility.industrial-system-info",
        "utility.industrial-currency-converter",
        "utility.woodland-calculator",
        "utility.woodland-system-info",
        "utility.woodland-currency-converter",
        "utility.botanical-calculator",
        "utility.botanical-system-info",
        "utility.botanical-currency-converter",
        "utility.art-deco-calculator",
        "utility.art-deco-system-info",
        "utility.art-deco-currency-converter",
        "utility.ember-glow-calculator",
        "utility.ember-glow-system-info",
        "utility.ember-glow-currency-converter"
    )
    $optionalWidgetStageRoot = Join-Path $tempRoot `
        "Optional-Widget-Packages"
    New-Item $optionalWidgetStageRoot -ItemType Directory -Force |
        Out-Null
    $optionalWidgetSources = Get-ChildItem `
        (Join-Path $PSScriptRoot "OptionalWidgets") -Directory
    foreach ($widgetSource in $optionalWidgetSources) {
        $widgetManifestPath = Join-Path $widgetSource.FullName `
            "manifest.json"
        if (-not (Test-Path -LiteralPath $widgetManifestPath `
            -PathType Leaf)) {
            continue
        }

        $widgetManifest = Get-Content -LiteralPath $widgetManifestPath -Raw |
            ConvertFrom-Json
        foreach ($field in @(
            "id", "name", "version", "author", "description",
            "assembly", "type"
        )) {
            if ([string]::IsNullOrWhiteSpace($widgetManifest.$field)) {
                throw "$widgetManifestPath requires '$field'."
            }
        }
        if ($widgetManifest.official -or $widgetManifest.bundled -or
            $widgetManifest.id.StartsWith("native.", `
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Optional package '$($widgetManifest.id)' cannot be bundled or use a native widget id."
        }
        if ($approvedOptionalWidgetIds -notcontains $widgetManifest.id) {
            continue
        }
        if ($widgetManifest.minimumSdkVersion -ne "2.8.3.0") {
            throw "$($widgetManifest.name) must require Widget SDK 2.8.3.0."
        }
        if (@($widgetManifest.capabilities) -notcontains "theme-switching") {
            throw "$($widgetManifest.name) must declare theme-switching support."
        }
        if ($widgetManifest.id -eq "utility.calculator") {
            if ($widgetManifest.version -ne "1.1.4") {
                throw "Steampunk Calculator must be version 1.1.4."
            }
        }
        if ($widgetManifest.id -eq "utility.system-info") {
            if ($widgetManifest.version -ne "1.0.5") {
                throw "Steampunk System Information must be version 1.0.5."
            }
            $systemInfoSkin = Join-Path $widgetSource.FullName `
                "steampunk-system-info-skin.png"
            Assert-File $systemInfoSkin `
                "Approved Steampunk System Information skin"
            $systemInfoSkinHash = (Get-FileHash -LiteralPath `
                $systemInfoSkin -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($systemInfoSkinHash -ne `
                "0236cf90b4228358e0a9d229f8126beb50ce84ff80aaf1cb5ada5570fbe0b1cf") {
                throw "The approved Steampunk System Information skin was changed."
            }
        }
        if ($widgetManifest.id -eq "utility.currency-converter") {
            if ($widgetManifest.version -ne "1.0.2") {
                throw "Steampunk Currency Converter must be version 1.0.2."
            }
            $currencySkin = Join-Path $widgetSource.FullName `
                "steampunk-currency-converter-skin.png"
            Assert-File $currencySkin `
                "Approved Steampunk Currency Converter skin"
            $currencySkinHash = (Get-FileHash -LiteralPath `
                $currencySkin -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($currencySkinHash -ne `
                "a847ce46585093e66b3fc6d5ffc79c5a050583d10f5eb4df5616269c130dbf92") {
                throw "The approved Steampunk Currency Converter skin was changed."
            }
        }
        if ($widgetManifest.id.StartsWith("utility.industrial-", `
                [StringComparison]::OrdinalIgnoreCase)) {
            if ($widgetManifest.version -ne "1.0.2") {
                throw "$($widgetManifest.name) must be version 1.0.2."
            }
            $industrialSkinName = if ($widgetManifest.id -eq `
                "utility.industrial-calculator") {
                    "industrial-calculator-skin.png"
                } elseif ($widgetManifest.id -eq `
                    "utility.industrial-system-info") {
                    "industrial-system-info-skin.png"
                } else {
                    "industrial-currency-converter-skin.png"
                }
            Assert-File (Join-Path $widgetSource.FullName `
                $industrialSkinName) "$($widgetManifest.name) skin"
        }
        if ($widgetManifest.id.StartsWith("utility.woodland-", `
                [StringComparison]::OrdinalIgnoreCase)) {
            if ($widgetManifest.version -ne "1.0.1") {
                throw "$($widgetManifest.name) must be version 1.0.1."
            }
            $woodlandSkinName = if ($widgetManifest.id -eq `
                "utility.woodland-calculator") {
                    "woodland-calculator-skin.png"
                } elseif ($widgetManifest.id -eq `
                    "utility.woodland-system-info") {
                    "woodland-system-info-skin.png"
                } else {
                    "woodland-currency-converter-skin.png"
                }
            Assert-File (Join-Path $widgetSource.FullName `
                $woodlandSkinName) "$($widgetManifest.name) skin"
        }
        if ($widgetManifest.id.StartsWith("utility.botanical-", `
                [StringComparison]::OrdinalIgnoreCase)) {
            if ($widgetManifest.version -ne "1.0.0") {
                throw "$($widgetManifest.name) must be version 1.0.0."
            }
            $botanicalSkinName = if ($widgetManifest.id -eq `
                "utility.botanical-calculator") {
                    "botanical-calculator-skin.png"
                } elseif ($widgetManifest.id -eq `
                    "utility.botanical-system-info") {
                    "botanical-system-info-skin.png"
                } else {
                    "botanical-currency-converter-skin.png"
                }
            Assert-File (Join-Path $widgetSource.FullName `
                $botanicalSkinName) "$($widgetManifest.name) skin"
        }
        if ($widgetManifest.id.StartsWith("utility.art-deco-", `
                [StringComparison]::OrdinalIgnoreCase)) {
            $expectedArtDecoVersion = if ($widgetManifest.id -eq `
                "utility.art-deco-calculator") { "1.0.4" } else { "1.0.3" }
            if ($widgetManifest.version -ne $expectedArtDecoVersion) {
                throw "$($widgetManifest.name) must be version $expectedArtDecoVersion."
            }
            $artDecoSkinName = if ($widgetManifest.id -eq `
                "utility.art-deco-calculator") {
                    "art-deco-calculator-skin.png"
                } elseif ($widgetManifest.id -eq `
                    "utility.art-deco-system-info") {
                    "art-deco-system-info-skin.png"
                } else {
                    "art-deco-currency-converter-skin.png"
                }
            Assert-File (Join-Path $widgetSource.FullName `
                $artDecoSkinName) "$($widgetManifest.name) skin"
        }
        if ($widgetManifest.id.StartsWith("utility.ember-glow-", `
                [StringComparison]::OrdinalIgnoreCase)) {
            $expectedEmberGlowVersion = "1.0.1"
            if ($widgetManifest.id -eq "utility.ember-glow-calculator") {
                $expectedEmberGlowVersion = "1.0.12"
            }
            if ($widgetManifest.id -eq "utility.ember-glow-currency-converter") {
                $expectedEmberGlowVersion = "1.0.13"
            }
            if ($widgetManifest.id -eq "utility.ember-glow-system-info") {
                $expectedEmberGlowVersion = "1.0.7"
            }
            if ($widgetManifest.version -ne $expectedEmberGlowVersion) {
                throw "$($widgetManifest.name) must be version $expectedEmberGlowVersion."
            }
            $emberGlowSkinName = if ($widgetManifest.id -eq `
                "utility.ember-glow-calculator") {
                    "ember-glow-calculator-skin.png"
                } elseif ($widgetManifest.id -eq `
                    "utility.ember-glow-system-info") {
                    "ember-glow-system-info-skin.png"
                } else {
                    "ember-glow-currency-converter-skin.png"
                }
            Assert-File (Join-Path $widgetSource.FullName `
                $emberGlowSkinName) "$($widgetManifest.name) skin"
            if ($widgetManifest.id -eq "utility.ember-glow-calculator" -or
                $widgetManifest.id -eq "utility.ember-glow-currency-converter") {
                Assert-File (Join-Path $widgetSource.FullName `
                    "ember-glow-system-info-skin.png") `
                    "$($widgetManifest.name) reflective frame source"
            }
            if ($widgetManifest.id -eq `
                    "utility.ember-glow-currency-converter") {
                Assert-File (Join-Path $widgetSource.FullName `
                    "ember-glow-swap-button.png") `
                    "Ember Glow currency swap button"
                Assert-File (Join-Path $widgetSource.FullName `
                    "ember-glow-calculator-lcd.png") `
                    "Ember Glow currency editable LCD"
            }
        }

        $assemblyName = [IO.Path]::GetFileName(
            $widgetManifest.assembly)
        $assemblyProject = [IO.Path]::GetFileNameWithoutExtension(
            $assemblyName)
        $authoritativeAssembly = Join-Path $PSScriptRoot `
            "src\$assemblyProject\bin\Release\$assemblyName"
        Assert-File $authoritativeAssembly `
            "$($widgetManifest.name) Release assembly"
        $widgetAssembly = [Reflection.Assembly]::LoadFrom(
            $authoritativeAssembly)
        $widgetType = $widgetAssembly.GetType(
            $widgetManifest.type, $true, $false)
        if ($null -eq $widgetType) {
            throw "$($widgetManifest.name) type could not be loaded."
        }
        if ($widgetType.GetInterfaces().Name -notcontains
            "IWidgetDesignerProvider") {
            throw "$($widgetManifest.name) does not expose its Designer layout."
        }
        if ($widgetType.GetInterfaces().Name -notcontains
            "IWidgetDesignerBackgroundLayerProvider") {
            throw "$($widgetManifest.name) does not expose an editable Designer background."
        }
        if ($widgetType.GetInterfaces().Name -notcontains
            "IWidgetThemeProvider") {
            throw "$($widgetManifest.name) does not expose installed-theme switching."
        }
        $widgetInstance = [Activator]::CreateInstance($widgetType)
        try {
            $menuText = @($widgetInstance.GetMenuCommands() |
                ForEach-Object { $_.Text })
            if ($menuText -notcontains "Edit in Designer") {
                throw "$($widgetManifest.name) is missing Edit in Designer."
            }
            $availableThemes = @($widgetInstance.Themes)
            if ($availableThemes -notcontains $widgetInstance.Theme) {
                throw "$($widgetManifest.name) does not include its current theme in the theme menu."
            }
            $designerLayout = $widgetInstance.CreateDesignerLayout()
            if ($null -eq $designerLayout -or
                $designerLayout.Elements.Count -eq 0) {
                throw "$($widgetManifest.name) has no editable Designer layers."
            }
            $designerBackground = @($designerLayout.Elements | Where-Object {
                $_.Id -eq "main-background" -and $_.Kind -eq 1
            })
            if ($designerBackground.Count -ne 1 -or
                [string]::IsNullOrWhiteSpace(
                    $designerBackground[0].ImagePath)) {
                throw "$($widgetManifest.name) has no selectable background layer."
            }
            if ($widgetManifest.id.EndsWith("currency-converter", `
                    [StringComparison]::OrdinalIgnoreCase)) {
                $currencyField = $widgetType.GetField(
                    "Currencies", [Reflection.BindingFlags]"NonPublic,Static")
                if ($null -eq $currencyField -or
                    @($currencyField.GetValue($null)) -notcontains "PHP") {
                    throw "The Currency Converter does not include PHP."
                }
            }
        }
        finally {
            if ($widgetInstance -is [IDisposable]) {
                $widgetInstance.Dispose()
            }
        }
        if ($widgetManifest.id -eq "utility.calculator") {
            $calculatorAssemblyVersion = `
                [Reflection.AssemblyName]::GetAssemblyName(
                    $authoritativeAssembly).Version.ToString()
            if ($calculatorAssemblyVersion -ne "1.1.4.0") {
                throw "Steampunk Calculator assembly must be version 1.1.4.0."
            }
        }
        if ($widgetManifest.id -eq "utility.system-info") {
            $systemInfoAssemblyVersion = `
                [Reflection.AssemblyName]::GetAssemblyName(
                    $authoritativeAssembly).Version.ToString()
            if ($systemInfoAssemblyVersion -ne "1.0.5.0") {
                throw "Steampunk System Information assembly must be version 1.0.5.0."
            }
        }
        if ($widgetManifest.id -eq "utility.currency-converter") {
            $currencyAssemblyVersion = `
                [Reflection.AssemblyName]::GetAssemblyName(
                    $authoritativeAssembly).Version.ToString()
            if ($currencyAssemblyVersion -ne "1.0.2.0") {
                throw "Steampunk Currency Converter assembly must be version 1.0.2.0."
            }
        }
        if ($widgetManifest.id.StartsWith("utility.industrial-", `
                [StringComparison]::OrdinalIgnoreCase)) {
            $industrialAssemblyVersion = `
                [Reflection.AssemblyName]::GetAssemblyName(
                    $authoritativeAssembly).Version.ToString()
            if ($industrialAssemblyVersion -ne "1.0.2.0") {
                throw "$($widgetManifest.name) assembly must be version 1.0.2.0."
            }
        }
        if ($widgetManifest.id.StartsWith("utility.woodland-", `
                [StringComparison]::OrdinalIgnoreCase)) {
            $woodlandAssemblyVersion = `
                [Reflection.AssemblyName]::GetAssemblyName(
                    $authoritativeAssembly).Version.ToString()
            if ($woodlandAssemblyVersion -ne "1.0.1.0") {
                throw "$($widgetManifest.name) assembly must be version 1.0.1.0."
            }
        }
        if ($widgetManifest.id.StartsWith("utility.botanical-", `
                [StringComparison]::OrdinalIgnoreCase)) {
            $botanicalAssemblyVersion = `
                [Reflection.AssemblyName]::GetAssemblyName(
                    $authoritativeAssembly).Version.ToString()
            if ($botanicalAssemblyVersion -ne "1.0.0.0") {
                throw "$($widgetManifest.name) assembly must be version 1.0.0.0."
            }
        }
        if ($widgetManifest.id.StartsWith("utility.art-deco-", `
                [StringComparison]::OrdinalIgnoreCase)) {
            $artDecoAssemblyVersion = `
                [Reflection.AssemblyName]::GetAssemblyName(
                    $authoritativeAssembly).Version.ToString()
            $expectedArtDecoAssemblyVersion = if ($widgetManifest.id -eq `
                "utility.art-deco-calculator") { "1.0.4.0" } else { "1.0.3.0" }
            if ($artDecoAssemblyVersion -ne `
                    $expectedArtDecoAssemblyVersion) {
                throw "$($widgetManifest.name) assembly must be version $expectedArtDecoAssemblyVersion."
            }
        }
        if ($widgetManifest.id.StartsWith("utility.ember-glow-", `
                [StringComparison]::OrdinalIgnoreCase)) {
            $emberGlowAssemblyVersion = `
                [Reflection.AssemblyName]::GetAssemblyName(
                    $authoritativeAssembly).Version.ToString()
            $expectedEmberGlowAssemblyVersion = "1.0.1.0"
            if ($widgetManifest.id -eq "utility.ember-glow-calculator") {
                $expectedEmberGlowAssemblyVersion = "1.0.12.0"
            }
            if ($widgetManifest.id -eq "utility.ember-glow-currency-converter") {
                $expectedEmberGlowAssemblyVersion = "1.0.13.0"
            }
            if ($widgetManifest.id -eq "utility.ember-glow-system-info") {
                $expectedEmberGlowAssemblyVersion = "1.0.7.0"
            }
            if ($emberGlowAssemblyVersion -ne `
                    $expectedEmberGlowAssemblyVersion) {
                throw "$($widgetManifest.name) assembly must be version $expectedEmberGlowAssemblyVersion."
            }
        }

        $widgetStage = Join-Path $optionalWidgetStageRoot `
            $widgetSource.Name
        Copy-DirectorySnapshot $widgetSource.FullName $widgetStage
        Copy-FileWithRetry $authoritativeAssembly `
            (Join-Path $widgetStage $widgetManifest.assembly)

        if ($widgetManifest.id.StartsWith("utility.ember-glow-", `
                [StringComparison]::OrdinalIgnoreCase)) {
            $previewError = Join-Path $PSScriptRoot `
                "src\XWidgetReborn.Dashboard\bin\Release\optional-preview-error.txt"
            Remove-Item -LiteralPath $previewError -Force -ErrorAction SilentlyContinue
            $dashboardExe = Join-Path $PSScriptRoot `
                "src\XWidgetReborn.Dashboard\bin\Release\EmilyDesk.exe"
            $previewArguments = '--render-optional-widget-preview "{0}" "{1}" "{2}"' -f `
                (Join-Path $widgetStage $widgetManifest.assembly), `
                $widgetManifest.type, $widgetStage
            $previewProcess = Start-Process -FilePath $dashboardExe `
                -ArgumentList $previewArguments -WorkingDirectory `
                (Split-Path -Parent $dashboardExe) -PassThru -Wait
            if ($previewProcess.ExitCode -ne 0) {
                if (Test-Path -LiteralPath $previewError) {
                    throw "Optional widget preview failed: $(Get-Content -LiteralPath $previewError -Raw)"
                }
                throw "Optional widget preview failed for $($widgetManifest.id)."
            }
            foreach ($artworkName in @("preview.png", "icon.png")) {
                Copy-FileWithRetry (Join-Path $widgetStage $artworkName) `
                    (Join-Path $widgetSource.FullName $artworkName)
            }
        }

        $packageBaseName = $widgetManifest.name `
            -replace '[^A-Za-z0-9]', ''
        if ([string]::IsNullOrWhiteSpace($packageBaseName)) {
            $packageBaseName = $widgetManifest.id `
                -replace '[^A-Za-z0-9]', ''
        }
        $optionalWidgetPackages += [PSCustomObject]@{
            Id = $widgetManifest.id
            Name = $widgetManifest.name
            Manifest = $widgetManifest
            StageDirectory = $widgetStage
            PackagePath = Join-Path $optionalWidgetPackageOutput `
                ($packageBaseName + "-" +
                    $widgetManifest.version + ".emilywidget")
        }
    }
    if ($optionalWidgetPackages.Count -ne $approvedOptionalWidgetIds.Count) {
        throw "Expected three optional widget packages for each approved EmilyDesk theme."
    }

    Copy-FileWithRetry (Join-Path $PSScriptRoot "EmilyDeskIconV2.ico") `
        (Join-Path $stage "EmilyDeskIconV2.ico")
    Copy-FileWithRetry "Compatibility-Audit.ps1" `
        (Join-Path $stage "Compatibility-Audit.ps1")
    Copy-FileWithRetry "Compatibility-Audit.cmd" `
        (Join-Path $stage "Compatibility-Audit.cmd")
    $wallpaperThemes = @(
        "Modern", "Vintage", "Steampunk", "Industrial", "Art_Deco",
        "Botanical_Nature", "Woodland_Nature"
    )
    foreach ($wallpaperTheme in $wallpaperThemes) {
        foreach ($wallpaperDesign in 1, 2) {
            foreach ($wallpaperSize in "3840x2160", "3840x2400") {
                $wallpaperName = "EmilyDesk_{0}_Wallpaper_{1}_{2}.png" -f `
                    $wallpaperTheme, $wallpaperDesign, $wallpaperSize
                Assert-File (Join-Path "Wallpapers" $wallpaperName) `
                    "Approved wallpaper"
            }
        }
    }
    $recycleBinThemes = @(
        "Modern", "Vintage", "Steampunk", "Industrial", "ArtDeco",
        "BotanicalNature", "WoodlandNature"
    )
    foreach ($recycleBinTheme in $recycleBinThemes) {
        foreach ($recycleBinState in "empty", "full") {
            Assert-File (Join-Path "Assets\RecycleBins" (
                "$recycleBinTheme\$recycleBinState.ico")) `
                "Themed Recycle Bin icon"
        }
    }
    $recycleWidgetSkins = @(
        @("Modern", "modern"),
        @("Vintage", "vintage"),
        @("Steampunk", "steampunk"),
        @("Industrial", "industrial"),
        @("ArtDeco", "art-deco"),
        @("BotanicalNature", "botanical"),
        @("WoodlandNature", "woodland")
    )
    foreach ($recycleWidgetSkin in $recycleWidgetSkins) {
        foreach ($recycleBinState in "empty", "full") {
            Assert-File (Join-Path "Assets\Themes" (
                "$($recycleWidgetSkin[0])\$($recycleWidgetSkin[1])-recycle-bin-$recycleBinState.png")) `
                "Themed Recycle Bin widget skin"
        }
    }

    $clockAndNatureArtwork = @(
        "Assets\Themes\Industrial\industrial-hour-hand.png",
        "Assets\Themes\Industrial\industrial-minute-hand.png",
        "Assets\Themes\Industrial\industrial-second-hand.png",
        "Assets\Themes\BotanicalNature\botanical-hour-hand.png",
        "Assets\Themes\BotanicalNature\botanical-minute-hand.png",
        "Assets\Themes\BotanicalNature\botanical-second-hand.png",
        "Assets\Themes\BotanicalNature\botanical-dock-skin.png",
        "Assets\Themes\WoodlandNature\woodland-hour-hand.png",
        "Assets\Themes\WoodlandNature\woodland-minute-hand.png",
        "Assets\Themes\WoodlandNature\woodland-second-hand.png",
        "Assets\Themes\WoodlandNature\woodland-custom-hour-hand.png",
        "Assets\Themes\WoodlandNature\woodland-custom-minute-hand.png"
    )
    foreach ($artwork in $clockAndNatureArtwork) {
        Assert-File $artwork "Clock or dock artwork"
    }

    $suppliedDesignerLayouts = @(
        "Assets\DesignerLayouts\botanical-calendar.v2.layout.json",
        "Assets\DesignerLayouts\botanical-weather.v2.layout.json",
        "Assets\DesignerLayouts\industrial-calendar.layout.json",
        "Assets\DesignerLayouts\industrial-weather.layout.json",
        "Assets\DesignerLayouts\steampunk-calculator.layout.json",
        "Assets\DesignerLayouts\steampunk-calendar.layout.json",
        "Assets\DesignerLayouts\steampunk-currency-converter.layout.json",
        "Assets\DesignerLayouts\steampunk-system-info.layout.json",
        "Assets\DesignerLayouts\steampunk-weather.layout.json",
        "Assets\DesignerLayouts\vintage-calendar.layout.json",
        "Assets\DesignerLayouts\woodland-calendar.layout.json",
        "Assets\DesignerLayouts\woodland-clock.layout.json",
        "Assets\DesignerLayouts\woodland-weather.v2.layout.json",
        "ThemePackages\EmberGlow\layouts\ember-glow-clock.layout.json",
        "ThemePackages\EmberGlow\layouts\ember-glow-weather.layout.json"
    )
    foreach ($layoutFile in $suppliedDesignerLayouts) {
        Assert-File $layoutFile "Supplied Designer layout"
        $layoutText = Get-Content -LiteralPath $layoutFile -Raw
        if ($layoutText -match '[A-Za-z]:\\\\Users\\\\') {
            throw "Bundled Designer layout contains a machine-specific user path: $layoutFile"
        }
    }

    Copy-DirectorySnapshot "ProviderProfiles" (Join-Path $stage "ProviderProfiles")
    Copy-DirectorySnapshot "Wallpapers" (Join-Path $stage "Wallpapers")
    Copy-DirectorySnapshot "Assets" (Join-Path $stage "Assets")
    $emberGlowSource = Join-Path $PSScriptRoot "ThemePackages\EmberGlow"
    $emberGlowManifestPath = Join-Path $emberGlowSource "theme.json"
    $emberGlowClockLayoutPath = Join-Path $emberGlowSource `
        "layouts\ember-glow-clock.layout.json"
    Assert-File $emberGlowManifestPath "Ember Glow theme manifest"
    Assert-File $emberGlowClockLayoutPath "Ember Glow clock layout"
    $emberGlowManifest = Get-Content -LiteralPath $emberGlowManifestPath -Raw |
        ConvertFrom-Json
    $emberGlowClockLayout = Get-Content -LiteralPath `
        $emberGlowClockLayoutPath -Raw | ConvertFrom-Json
    if ($emberGlowManifest.version -ne "1.2.8") {
        throw "The bundled Ember Glow theme must be version 1.2.8."
    }
    foreach ($clockHandId in @(
        "clock-hour-hand", "clock-minute-hand", "clock-second-hand"
    )) {
        $clockHand = $emberGlowClockLayout.Elements | Where-Object {
            $_.Id -eq $clockHandId
        } | Select-Object -First 1
        if (-not $clockHand -or [Math]::Abs(
            [double]$clockHand.PreviewRotation) -gt 0.001) {
            throw "Ember Glow $clockHandId contains a live clock rotation offset."
        }
        $pivotX = [double]$clockHand.X +
            [double]$clockHand.Width * [double]$clockHand.HandPivotX
        $pivotY = [double]$clockHand.Y +
            [double]$clockHand.Height * [double]$clockHand.HandPivotY
        if ([Math]::Abs($pivotX - 180.0) -gt 0.1 -or
            [Math]::Abs($pivotY - 180.0) -gt 0.1) {
            throw "Ember Glow $clockHandId is not aligned to the clock centre."
        }
    }
    $clockHub = $emberGlowClockLayout.Elements | Where-Object {
        $_.Id -eq "clock-centre-pivot"
    } | Select-Object -First 1
    if (-not $clockHub -or
        [Math]::Abs(([double]$clockHub.X + [double]$clockHub.Width / 2) - 180.0) -gt 0.1 -or
        [Math]::Abs(([double]$clockHub.Y + [double]$clockHub.Height / 2) - 180.0) -gt 0.1) {
        throw "The Ember Glow centre cap is not aligned to the hand pivot."
    }
    $secondHand = $emberGlowClockLayout.Elements | Where-Object {
        $_.Id -eq "clock-second-hand"
    } | Select-Object -First 1
    $secondHandAssetPath = Join-Path $emberGlowSource `
        "assets\ember-glow-second-hand-v2.png"
    Assert-File $secondHandAssetPath "Ember Glow second-hand artwork"
    $secondHandBitmap = New-Object System.Drawing.Bitmap($secondHandAssetPath)
    try {
        $sourcePivotY = [Math]::Ceiling(
            $secondHandBitmap.Height * [double]$secondHand.HandPivotY)
        $firstVisibleY = $secondHandBitmap.Height
        for ($y = 0; $y -lt $secondHandBitmap.Height; $y++) {
            for ($x = 0; $x -lt $secondHandBitmap.Width; $x++) {
                $alpha = $secondHandBitmap.GetPixel($x, $y).A
                if ($alpha -gt 0 -and $y -lt $firstVisibleY) {
                    $firstVisibleY = $y
                }

            }
        }
        $tailLength = ($secondHandBitmap.Height - $sourcePivotY) /
            [double]$secondHandBitmap.Height * [double]$secondHand.Height
        if ($tailLength -gt 36.0) {
            throw "The Ember Glow second-hand tail is too long."
        }
        $forwardLength = (
            [double]$secondHand.HandPivotY -
            [double]$firstVisibleY / $secondHandBitmap.Height
        ) * [double]$secondHand.Height
        if ($forwardLength -lt 137.0 -or $forwardLength -gt 143.0) {
            throw "The Ember Glow second hand does not finish at the dial markers."
        }
    }
    finally {
        $secondHandBitmap.Dispose()
    }
    Copy-DirectorySnapshot $emberGlowSource `
        (Join-Path $stage "Themes\ember-glow")
    $copperGlowSource = Join-Path $PSScriptRoot "ThemePackages\CopperGlow"
    foreach ($sharedArtwork in @("wallpapers", "weather-icons")) {
        $targetArtwork = Join-Path $copperGlowSource $sharedArtwork
        if (-not (Test-Path -LiteralPath $targetArtwork)) {
            Copy-DirectorySnapshot (Join-Path $emberGlowSource $sharedArtwork) `
                $targetArtwork
        }
    }
    Copy-DirectorySnapshot $copperGlowSource `
        (Join-Path $stage "Themes\copper-glow")
    Copy-DirectorySnapshot "Widgets\GalleryPreviews" `
        (Join-Path $stage "Widgets\GalleryPreviews")
    $manifest | Set-Content "$logs\payload-manifest.txt"

    Write-Host "[3/8] Creating private full-payload snapshot..."
    Copy-DirectorySnapshot $stage $tempFull
    Copy-FileWithRetry "$logs\payload-manifest.txt" `
        (Join-Path $tempFull "payload-manifest.txt")

    Write-Host "[4/8] Creating private dashboard snapshot..."
    Copy-FileWithRetry "$stage\EmilyDesk.exe" `
        "$tempDashboard\EmilyDesk.exe"
    Copy-FileWithRetry "$stage\XWidgetReborn.Shared.dll" `
        "$tempDashboard\XWidgetReborn.Shared.dll"
    Copy-FileWithRetry "$stage\XWidgetReborn.WidgetSdk.dll" `
        "$tempDashboard\XWidgetReborn.WidgetSdk.dll"
    Copy-FileWithRetry "$stage\EmilyDesk.Engine.exe" `
        "$tempDashboard\EmilyDesk.Engine.exe"
    Copy-FileWithRetry "$stage\EmilyDesk.SetupHelper.exe" `
        "$tempDashboard\EmilyDesk.SetupHelper.exe"
    Copy-FileWithRetry "$stage\EmilyDeskIconV2.ico" `
        "$tempDashboard\EmilyDeskIconV2.ico"
    Copy-DirectorySnapshot "$stage\Widgets" `
        (Join-Path $tempDashboard "Widgets")
    Copy-DirectorySnapshot "$stage\Wallpapers" `
        (Join-Path $tempDashboard "Wallpapers")
    Copy-DirectorySnapshot "$stage\Assets" `
        (Join-Path $tempDashboard "Assets")
    Copy-FileWithRetry "$logs\payload-manifest.txt" `
        "$tempDashboard\payload-manifest.txt"
    Copy-FileWithRetry "Run-Dashboard-Test.cmd" `
        "$tempDashboard\Run-Dashboard-Test.cmd"
    Copy-FileWithRetry "Run-Dashboard-Test.ps1" `
        "$tempDashboard\Run-Dashboard-Test.ps1"
    Copy-FileWithRetry "Compatibility-Audit.ps1" `
        "$tempDashboard\Compatibility-Audit.ps1"
    Copy-FileWithRetry "Compatibility-Audit.cmd" `
        "$tempDashboard\Compatibility-Audit.cmd"

    Write-Host "[5/8] Creating ZIPs and widget packages..."
    $fullZip = Join-Path $output "EmilyDesk-$version-Full-Payload.zip"
    $dashboardZip = Join-Path $output "EmilyDesk-$version-Gallery-Portable.zip"

    Create-ZipFromSnapshot $tempFull $fullZip
    Create-ZipFromSnapshot $tempDashboard $dashboardZip
    $allWidgetPackages = @($nativeWidgetPackages) + `
        @($optionalWidgetPackages)
    foreach ($widgetPackage in $allWidgetPackages) {
        Create-ZipFromSnapshot `
            $widgetPackage.StageDirectory `
            $widgetPackage.PackagePath
    }
    $emberGlowThemePackage = Join-Path $themePackageOutput (
        "EmberGlow-{0}.emilytheme" -f $emberGlowManifest.version
    )
    Create-ZipFromSnapshot $emberGlowSource $emberGlowThemePackage
    $copperGlowThemePackage = Join-Path $themePackageOutput "CopperGlow-1.2.4.emilytheme"
    Create-ZipFromSnapshot $copperGlowSource $copperGlowThemePackage

    Write-Host "[6/8] Validating package contents..."
    $required = @(
        "EmilyDesk.exe",
        "EmilyDesk.Engine.exe",
        "EmilyDesk.Service.exe",
        "EmilyDesk.SetupHelper.exe",
        "EmilyDesk.Designer.exe",
        "EmilyDeskIconV2.ico",
        "XWidgetReborn.Shared.dll",
        "XWidgetReborn.WidgetSdk.dll",
        "XWidgetReborn.Widgets.Weather.dll",
        "XWidgetReborn.Widgets.Clock.dll",
        "XWidgetReborn.Widgets.Calendar.dll",
        "XWidgetReborn.Widgets.RecycleBin.dll",
        "XWidgetReborn.WeatherCore.dll"
    )

    $report = @()
    foreach ($name in $required) {
        Assert-File (Join-Path $tempFull $name) "Full snapshot component"
        $report += "PASS: Full payload contains $name"
    }

    foreach ($widgetPackage in $allWidgetPackages) {
        Assert-File $widgetPackage.PackagePath `
            "$($widgetPackage.Name) installable package"

        $archive = [IO.Compression.ZipFile]::OpenRead(
            $widgetPackage.PackagePath)
        try {
            $archivePaths = @($archive.Entries | ForEach-Object {
                $_.FullName.Replace('\', '/')
            })
            foreach ($requiredEntry in @(
                "manifest.json",
                $widgetPackage.Manifest.assembly.Replace('\', '/')
            )) {
                if ($archivePaths -notcontains $requiredEntry) {
                    throw "$($widgetPackage.PackagePath) is missing $requiredEntry."
                }
            }
            foreach ($imageField in @("icon", "preview")) {
                $imagePath = $widgetPackage.Manifest.$imageField
                if (-not [string]::IsNullOrWhiteSpace($imagePath) -and
                    $archivePaths -notcontains $imagePath.Replace('\', '/')) {
                    throw "$($widgetPackage.PackagePath) is missing $imagePath."
                }
            }
            if ($widgetPackage.Id -eq "utility.calculator" -and
                $archivePaths -notcontains
                    "steampunk-calculator-skin.png") {
                throw "$($widgetPackage.PackagePath) is missing the approved calculator skin."
            }
            if ($widgetPackage.Id -eq "utility.calculator" -and
                (Split-Path -Leaf $widgetPackage.PackagePath) -ne
                    "SteampunkCalculator-1.1.4.emilywidget") {
                throw "The Calculator package filename or version is incorrect."
            }
            if ($widgetPackage.Id -eq "utility.system-info" -and
                $archivePaths -notcontains
                    "steampunk-system-info-skin.png") {
                throw "$($widgetPackage.PackagePath) is missing the approved system-information skin."
            }
            if ($widgetPackage.Id -eq "utility.system-info" -and
                (Split-Path -Leaf $widgetPackage.PackagePath) -ne
                    "SteampunkSystemInformation-1.0.5.emilywidget") {
                throw "The System Information package filename or version is incorrect."
            }
            if ($widgetPackage.Id -eq "utility.currency-converter" -and
                $archivePaths -notcontains
                    "steampunk-currency-converter-skin.png") {
                throw "$($widgetPackage.PackagePath) is missing the approved currency-converter skin."
            }
            if ($widgetPackage.Id -eq "utility.currency-converter" -and
                (Split-Path -Leaf $widgetPackage.PackagePath) -ne
                    "SteampunkCurrencyConverter-1.0.2.emilywidget") {
                throw "The Currency Converter package filename or version is incorrect."
            }
            $industrialPackageFiles = @{
                "utility.industrial-calculator" = @(
                    "industrial-calculator-skin.png",
                    "IndustrialCalculator-1.0.2.emilywidget")
                "utility.industrial-system-info" = @(
                    "industrial-system-info-skin.png",
                    "IndustrialSystemInformation-1.0.2.emilywidget")
                "utility.industrial-currency-converter" = @(
                    "industrial-currency-converter-skin.png",
                    "IndustrialCurrencyConverter-1.0.2.emilywidget")
            }
            if ($industrialPackageFiles.ContainsKey($widgetPackage.Id)) {
                $expectedIndustrial = $industrialPackageFiles[
                    $widgetPackage.Id]
                if ($archivePaths -notcontains $expectedIndustrial[0]) {
                    throw "$($widgetPackage.PackagePath) is missing $($expectedIndustrial[0])."
                }
                if ((Split-Path -Leaf $widgetPackage.PackagePath) -ne
                    $expectedIndustrial[1]) {
                    throw "$($widgetPackage.Name) package filename or version is incorrect."
                }
            }
            $woodlandPackageFiles = @{
                "utility.woodland-calculator" = @(
                    "woodland-calculator-skin.png",
                    "WoodlandNatureCalculator-1.0.1.emilywidget")
                "utility.woodland-system-info" = @(
                    "woodland-system-info-skin.png",
                    "WoodlandNatureSystemInformation-1.0.1.emilywidget")
                "utility.woodland-currency-converter" = @(
                    "woodland-currency-converter-skin.png",
                    "WoodlandNatureCurrencyConverter-1.0.1.emilywidget")
            }
            if ($woodlandPackageFiles.ContainsKey($widgetPackage.Id)) {
                $expectedWoodland = $woodlandPackageFiles[
                    $widgetPackage.Id]
                if ($archivePaths -notcontains $expectedWoodland[0]) {
                    throw "$($widgetPackage.PackagePath) is missing $($expectedWoodland[0])."
                }
                if ((Split-Path -Leaf $widgetPackage.PackagePath) -ne
                    $expectedWoodland[1]) {
                    throw "$($widgetPackage.Name) package filename or version is incorrect."
                }
            }
            $botanicalPackageFiles = @{
                "utility.botanical-calculator" = @(
                    "botanical-calculator-skin.png",
                    "BotanicalNatureCalculator-1.0.0.emilywidget")
                "utility.botanical-system-info" = @(
                    "botanical-system-info-skin.png",
                    "BotanicalNatureSystemInformation-1.0.0.emilywidget")
                "utility.botanical-currency-converter" = @(
                    "botanical-currency-converter-skin.png",
                    "BotanicalNatureCurrencyConverter-1.0.0.emilywidget")
            }
            if ($botanicalPackageFiles.ContainsKey($widgetPackage.Id)) {
                $expectedBotanical = $botanicalPackageFiles[
                    $widgetPackage.Id]
                if ($archivePaths -notcontains $expectedBotanical[0]) {
                    throw "$($widgetPackage.PackagePath) is missing $($expectedBotanical[0])."
                }
                if ((Split-Path -Leaf $widgetPackage.PackagePath) -ne
                    $expectedBotanical[1]) {
                    throw "$($widgetPackage.Name) package filename or version is incorrect."
                }
            }
            $artDecoPackageFiles = @{
                "utility.art-deco-calculator" = @(
                    "art-deco-calculator-skin.png",
                    "ArtDecoCalculator-1.0.4.emilywidget")
                "utility.art-deco-system-info" = @(
                    "art-deco-system-info-skin.png",
                    "ArtDecoSystemInformation-1.0.3.emilywidget")
                "utility.art-deco-currency-converter" = @(
                    "art-deco-currency-converter-skin.png",
                    "ArtDecoCurrencyConverter-1.0.3.emilywidget")
            }
            if ($artDecoPackageFiles.ContainsKey($widgetPackage.Id)) {
                $expectedArtDeco = $artDecoPackageFiles[
                    $widgetPackage.Id]
                if ($archivePaths -notcontains $expectedArtDeco[0]) {
                    throw "$($widgetPackage.PackagePath) is missing $($expectedArtDeco[0])."
                }
                if ((Split-Path -Leaf $widgetPackage.PackagePath) -ne
                    $expectedArtDeco[1]) {
                    throw "$($widgetPackage.Name) package filename or version is incorrect."
                }
            }
            $emberGlowPackageFiles = @{
                "utility.ember-glow-calculator" = @(
                    "ember-glow-calculator-skin.png",
                    "EmberGlowCalculator-1.0.12.emilywidget")
                "utility.ember-glow-system-info" = @(
                    "ember-glow-system-info-skin.png",
                    "EmberGlowSystemInformation-1.0.7.emilywidget")
                "utility.ember-glow-currency-converter" = @(
                    "ember-glow-currency-converter-skin.png",
                    "EmberGlowCurrencyConverter-1.0.13.emilywidget")
            }
            if ($emberGlowPackageFiles.ContainsKey($widgetPackage.Id)) {
                $expectedEmberGlow = $emberGlowPackageFiles[
                    $widgetPackage.Id]
                if ($archivePaths -notcontains $expectedEmberGlow[0]) {
                    throw "$($widgetPackage.PackagePath) is missing $($expectedEmberGlow[0])."
                }
                if (($widgetPackage.Id -eq "utility.ember-glow-calculator" -or
                     $widgetPackage.Id -eq "utility.ember-glow-currency-converter") -and
                    $archivePaths -notcontains "ember-glow-system-info-skin.png") {
                    throw "$($widgetPackage.PackagePath) is missing the reflective frame source."
                }
                if ($widgetPackage.Id -eq "utility.ember-glow-calculator" -and
                    $archivePaths -notcontains "ember-glow-calculator-lcd.png") {
                    throw "$($widgetPackage.PackagePath) is missing the editable LCD image."
                }
                if ($widgetPackage.Id -eq "utility.ember-glow-currency-converter" -and
                    $archivePaths -notcontains "ember-glow-calculator-lcd.png") {
                    throw "$($widgetPackage.PackagePath) is missing the two LCD layers' image."
                }
                if ((Split-Path -Leaf $widgetPackage.PackagePath) -ne
                    $expectedEmberGlow[1]) {
                    throw "$($widgetPackage.Name) package filename or version is incorrect."
                }
                if ($widgetPackage.Id -eq `
                        "utility.ember-glow-currency-converter" -and
                    $archivePaths -notcontains "ember-glow-swap-button.png") {
                    throw "$($widgetPackage.PackagePath) is missing the swap button."
                }
            }
        }
        finally {
            $archive.Dispose()
        }

        $verificationRoot = Join-Path $tempRoot `
            ("Widget-Package-Install-" + $widgetPackage.Id)
        $verificationRunner = New-DashboardVerificationRunner `
            $stage `
            (Join-Path $tempRoot ("Dashboard-Verify-" + $widgetPackage.Id))
        Invoke-WidgetPackageInstallVerification `
            $verificationRunner `
            $widgetPackage.PackagePath `
            $verificationRoot
        # VerifyBuildScenarios performs the complete install, same-version,
        # cancellation, replacement, manifest, and assembly validation inside
        # the dashboard process. Do not repeat filesystem assertions here:
        # security software can remove the temporary verification tree as soon
        # as the child process exits, causing a false build failure even though
        # the dashboard verification returned exit code 0.
        $report += "PASS: Dashboard verified fresh install, same-version detection, cancellation, newer-version replacement, and removal for $(
            Split-Path -Leaf $widgetPackage.PackagePath
        )"
    }

    foreach ($optionalWidgetPackage in $optionalWidgetPackages) {
        Assert-File $optionalWidgetPackage.PackagePath `
            "$($optionalWidgetPackage.Name) optional widget package"
        $report += "PASS: Optional widget is packaged separately from the installer: $(
            Split-Path -Leaf $optionalWidgetPackage.PackagePath
        )"
    }

    Assert-File $fullZip "Full payload ZIP"
    Assert-File $dashboardZip "Dashboard portable ZIP"
    Assert-File $emberGlowThemePackage "Ember Glow theme package"
    Assert-File (Join-Path $tempFull "Themes\ember-glow\theme.json") `
        "Bundled Ember Glow fresh-install manifest"
    Assert-File (Join-Path $tempFull `
        "Themes\ember-glow\layouts\ember-glow-clock.layout.json") `
        "Bundled corrected Ember Glow clock layout"
    $emberGlowArchive = [IO.Compression.ZipFile]::OpenRead(
        $emberGlowThemePackage)
    try {
        $emberGlowEntries = @($emberGlowArchive.Entries | ForEach-Object {
            $_.FullName.Replace('\', '/')
        })
        foreach ($requiredThemeEntry in @(
            "theme.json",
            "assets/ember-glow-dock-skin.png",
            "wallpapers/ember-glow-wallpaper-1-3840x2160.png",
            "wallpapers/ember-glow-wallpaper-1-3840x2400.png",
            "wallpapers/ember-glow-wallpaper-2-3840x2160.png",
            "wallpapers/ember-glow-wallpaper-2-3840x2400.png"
        )) {
            if ($emberGlowEntries -notcontains $requiredThemeEntry) {
                throw "Ember Glow theme package is missing $requiredThemeEntry."
            }
        }
    }
    finally {
        $emberGlowArchive.Dispose()
    }
    $report += "PASS: Full payload ZIP exists"
    $report += "PASS: Dashboard portable ZIP exists"
    $report += "PASS: Fresh-install payload includes Ember Glow 1.2.8"
    $report += "PASS: Ember Glow clock hands and centre share the 180,180 pivot"
    $report += "PASS: Ember Glow theme package includes its revised dock and wallpapers"
    $report += "PASS: ZIPs were created from isolated build snapshots"
    $report | Set-Content "$logs\package-validation.txt"

    Write-Host "[7/8] Running architecture checks..."
    $dashboardMatches = Get-ChildItem "src\XWidgetReborn.Dashboard" -Filter *.cs |
        Select-String -SimpleMatch "--install-service"

    if ($dashboardMatches) {
        throw "Dashboard source still contains service-install commands."
    }

    @(
        "PASS: dashboard has no service installation commands",
        "PASS: independent widget runtime is a dedicated executable",
        "PASS: legacy XWidget runtime discovery and launch entry points are disabled",
        "PASS: service is a dedicated executable",
        "PASS: privileged operations are isolated in SetupHelper",
        "PASS: packaging uses an isolated project-local snapshot",
        "PASS: packaging does not archive files from Output"
    ) | Set-Content "$logs\architecture-checks.txt"

    # Run UI verification from the build output. Executing the staged installer
    # source caused real-time security scanners to hold or remove that payload
    # between validation and Inno Setup compilation.
    $stage4DashboardDirectory = Join-Path $PSScriptRoot `
        "src\XWidgetReborn.Dashboard\bin\Release"
    $stage4TestExecutable = Join-Path $stage4DashboardDirectory `
        "EmilyDesk.Stage4Test.exe"
    $stage5TestExecutable = Join-Path $stage4DashboardDirectory `
        "EmilyDesk.Stage5Test.exe"
    Copy-Item (Join-Path $stage4DashboardDirectory "EmilyDesk.exe") `
        $stage4TestExecutable -Force
    Copy-Item (Join-Path $stage4DashboardDirectory "EmilyDesk.exe") `
        $stage5TestExecutable -Force
    $stage4StartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $stage4StartInfo.FileName = $stage4TestExecutable
    $stage4StartInfo.Arguments = "--verify-stage4-ui"
    $stage4StartInfo.WorkingDirectory = $stage4DashboardDirectory
    $stage4StartInfo.UseShellExecute = $false
    $stage4StartInfo.CreateNoWindow = $true
    $stage4Process = New-Object System.Diagnostics.Process
    $stage4Process.StartInfo = $stage4StartInfo
    if (-not $stage4Process.Start()) {
        throw "Stage 4 UI verification process could not be started."
    }
    $stage4Process.WaitForExit()
    if ($stage4Process.ExitCode -ne 0) {
        $stage4ErrorPath = Join-Path $stage4DashboardDirectory `
            "stage4-ui-error.txt"
        if (Test-Path -LiteralPath $stage4ErrorPath) {
            Copy-Item $stage4ErrorPath `
                (Join-Path $logs "stage4-ui-error.txt") -Force
            Write-Host "STAGE 4 ERROR DETAILS" -ForegroundColor Red
            Get-Content -LiteralPath $stage4ErrorPath |
                Write-Host -ForegroundColor Red
        }
        throw "Stage 4 UI verification failed with exit code $($stage4Process.ExitCode)."
    }

    $stage5StartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $stage5StartInfo.FileName = $stage5TestExecutable
    $stage5StartInfo.Arguments = "--verify-stage5-themes"
    $stage5StartInfo.WorkingDirectory = $stage4DashboardDirectory
    $stage5StartInfo.UseShellExecute = $false
    $stage5StartInfo.CreateNoWindow = $true
    # Stage 5 asserts factory geometry. Imported/personal layouts are rendered
    # separately by --verify-designer-saves, without changing these baselines.
    $stage5StartInfo.EnvironmentVariables["EMILYDESK_IGNORE_DESIGNER_LAYOUT"] = "1"
    $stage5Process = New-Object System.Diagnostics.Process
    $stage5Process.StartInfo = $stage5StartInfo
    if (-not $stage5Process.Start()) {
        throw "Stage 5 theme verification process could not be started."
    }
    $stage5Process.WaitForExit()
    if ($stage5Process.ExitCode -ne 0) {
        $stage5ErrorPath = Join-Path $stage4DashboardDirectory `
            "stage5-theme-error.txt"
        if (Test-Path -LiteralPath $stage5ErrorPath) {
            Copy-Item $stage5ErrorPath `
                (Join-Path $logs "stage5-theme-error.txt") -Force
            Write-Host ""
            Write-Host "STAGE 5 ERROR DETAILS" -ForegroundColor Red
            Get-Content -LiteralPath $stage5ErrorPath |
                ForEach-Object { Write-Host $_ }
            Write-Host ""
        }
        throw "Stage 5 theme verification failed with exit code $($stage5Process.ExitCode)."
    }
    @(
        "PASS: seven approved theme families are centralized in one catalog",
        "PASS: all theme primary text palettes meet 4.5:1 contrast",
        "PASS: Clock, Calendar, Weather, and Recycle Bin expose and persist every theme",
        "PASS: production Art Deco and Industrial PNG skins render without theme fallback",
        "PASS: each Art Deco and Industrial PNG is loaded once by the shared image cache",
        "PASS: Industrial Clock, Calendar, and Weather use production preferred footprints",
        "PASS: Industrial Weather uses the fixed-parent reusable SlidePanel and passes eight toggle cycles",
        "PASS: all four official widgets render at 50%, 75%, 100%, 125%, 150%, 175%, and 200%",
        "PASS: static theme rendering completed within the safety limit",
        "PASS: wallpaper discovery and preview do not change Windows",
        "PASS: every theme maps to a recommended wallpaper asset name"
    ) | Set-Content "$logs\stage5-theme-tests.txt"

    if ($FullVerification) {
    $weatherLaunchDirectory = Join-Path $PSScriptRoot `
        "src\XWidgetReborn.Runtime\bin\Release"
    $industrialWeatherLaunchExecutable = Join-Path $weatherLaunchDirectory `
        "EmilyDesk.IndustrialWeatherLaunchTest.exe"
    Copy-Item (Join-Path $weatherLaunchDirectory "EmilyDesk.Engine.exe") `
        $industrialWeatherLaunchExecutable -Force
    $industrialStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $industrialStartInfo.FileName = $industrialWeatherLaunchExecutable
    $industrialStartInfo.Arguments = '--verify-theme-weather-launch Industrial'
    $industrialStartInfo.WorkingDirectory = $weatherLaunchDirectory
    $industrialStartInfo.UseShellExecute = $false
    $industrialStartInfo.CreateNoWindow = $true
    # Release verification must test the shipped fallback geometry, not the
    # developer's personal live Designer layout in LocalAppData.
    $industrialStartInfo.EnvironmentVariables[
        "EMILYDESK_IGNORE_DESIGNER_LAYOUT"
    ] = "1"
    $industrialProcess = New-Object System.Diagnostics.Process
    $industrialProcess.StartInfo = $industrialStartInfo
    Write-Host "Industrial Weather verification"
    if (-not $industrialProcess.Start()) {
        throw "Industrial Weather launch verifier could not be started."
    }
    $industrialProcess.WaitForExit()
    if ($industrialProcess.ExitCode -ne 0) {
        $industrialErrorPath = Join-Path $weatherLaunchDirectory `
            "theme-weather-launch-error.txt"
        if (Test-Path -LiteralPath $industrialErrorPath) {
            Copy-Item $industrialErrorPath `
                (Join-Path $logs "industrial-weather-launch-error.txt") -Force
            Write-Host "INDUSTRIAL WEATHER ERROR DETAILS" -ForegroundColor Red
            Get-Content -LiteralPath $industrialErrorPath |
                Write-Host -ForegroundColor Red
        }
        throw "Industrial Weather launch verifier failed with exit code $($industrialProcess.ExitCode)."
    }
    $industrialMeasurements = Join-Path $weatherLaunchDirectory `
        "industrial-weather-measurements.txt"
    if (-not (Test-Path $industrialMeasurements)) {
        throw "Industrial Weather verifier did not produce runtime measurements."
    }
    $industrialTypographyMeasurements = Join-Path $weatherLaunchDirectory `
        "industrial-weather-typography-measurements.txt"
    if (-not (Test-Path $industrialTypographyMeasurements)) {
        throw "Industrial Weather verifier did not produce typography measurements."
    }
    $industrialPreview = Join-Path $weatherLaunchDirectory `
        "industrial-weather-runtime-preview.png"
    if (-not (Test-Path $industrialPreview)) {
        throw "Industrial Weather verifier did not produce its runtime preview."
    }
    Copy-Item $industrialPreview `
        "$logs\industrial-weather-runtime-preview.png" -Force
    @(
        "PASS: native.weather loads with the explicitly selected Industrial appearance",
        "PASS: Industrial activates its dedicated reusable SlidePanel",
        (Get-Content $industrialMeasurements),
        (Get-Content $industrialTypographyMeasurements)
    ) | Set-Content "$logs\industrial-weather-launch-test.txt"

    $weatherLaunchExecutable = Join-Path $weatherLaunchDirectory `
        "EmilyDesk.WeatherLaunchTest.exe"
    Copy-Item (Join-Path $weatherLaunchDirectory "EmilyDesk.Engine.exe") `
        $weatherLaunchExecutable -Force
    $weatherLaunchStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $weatherLaunchStartInfo.FileName = $weatherLaunchExecutable
    $weatherLaunchStartInfo.Arguments = "--verify-artdeco-weather-launch"
    $weatherLaunchStartInfo.WorkingDirectory = $weatherLaunchDirectory
    $weatherLaunchStartInfo.UseShellExecute = $false
    $weatherLaunchStartInfo.CreateNoWindow = $true
    $weatherLaunchProcess = New-Object System.Diagnostics.Process
    $weatherLaunchProcess.StartInfo = $weatherLaunchStartInfo
    Write-Host "Art Deco Weather regression verification"
    if (-not $weatherLaunchProcess.Start()) {
        throw "Art Deco Weather launch verifier could not be started."
    }
    $weatherLaunchProcess.WaitForExit()
    if ($weatherLaunchProcess.ExitCode -ne 0) {
        throw "Art Deco Weather launch verifier failed with exit code $($weatherLaunchProcess.ExitCode)."
    }
    @(
        "PASS: native.weather loads with the Art Deco appearance",
        "PASS: the production WidgetWindow creates a rebased 1081x494 SlidePanel surface",
        "PASS: initial Art Deco rendering completes without a zero scale factor"
    ) | Set-Content "$logs\artdeco-weather-launch-test.txt"
    }
    else {
        "SKIPPED: run Release-Check.cmd for full Industrial Weather launch verification." |
            Set-Content "$logs\industrial-weather-launch-test.txt"
        "SKIPPED: run Release-Check.cmd for full Art Deco Weather launch verification." |
            Set-Content "$logs\artdeco-weather-launch-test.txt"
        Write-Host "Full weather launch verification skipped (use Release-Check.cmd)."
    }
    @(
        "PASS: Dashboard exposes Home, Widgets, Wallpaper, Docks, Designer, Widget Templates, and Settings navigation",
        "PASS: Dashboard exposes one primary Add Widget / Open Gallery action",
        "PASS: Dashboard exposes Industrial and Steampunk Dock launch actions plus shared settings",
        "PASS: obsolete coming-soon controls are absent",
        "PASS: Gallery retains its owner-drawn virtual surface",
        "PASS: Gallery creates Weather, Clock, Calendar, and Recycle Bin cards",
        "PASS: Gallery exposes Clear Search and a card action"
    ) | Set-Content "$logs\stage4-ui-tests.txt"

    if (-not $SkipInstaller) {
        Write-Host "[8/8] Compiling and validating installer..."
        $iscc = Find-InnoSetup
        if (-not $iscc) { throw "Inno Setup 6 was not found." }

        $stdoutLog = Join-Path $logs "inno-compiler-stdout.log"
        $stderrLog = Join-Path $logs "inno-compiler-stderr.log"

        $scriptPath = Join-Path $PSScriptRoot "Installer-UserMode.iss"
        Assert-File $scriptPath "Inno Setup script"

        $installerScript = Get-Content -LiteralPath $scriptPath -Raw
        foreach ($installerComponent in $required) {
            $installerSource = "Stage\$installerComponent"
            if ($installerScript -notmatch [regex]::Escape($installerSource)) {
                throw "Installer script does not include $installerComponent."
            }
        }
        if ($installerScript -notmatch [regex]::Escape('Stage\EmilyDesk.Engine.exe')) {
            throw "Installer script does not include EmilyDesk.Engine.exe."
        }
        if ($installerScript -notmatch [regex]::Escape('Stage\EmilyDesk.exe')) {
            throw "Installer script does not include EmilyDesk.exe."
        }
        if ($installerScript -notmatch [regex]::Escape('Stage\Wallpapers\*')) {
            throw "Installer script does not include the wallpaper resource directory."
        }
        if ($installerScript -notmatch [regex]::Escape('Stage\Assets\*')) {
            throw "Installer script does not include the production theme assets."
        }
        if ($installerScript -notmatch [regex]::Escape(
            'Stage\Themes\ember-glow\*')) {
            throw "Installer script does not include the corrected Ember Glow theme."
        }
        if ($installerScript -notmatch [regex]::Escape('DefaultDirName={autopf}\EmilyDesk')) {
            throw "Installer does not protect EmilyDesk binaries under Program Files."
        }
        if ($installerScript -notmatch '(?m)^PrivilegesRequired=admin\s*$') {
            throw "Installer is not elevated for its LocalSystem service deployment."
        }
        if ($installerScript -match 'api\.accuweather\.com') {
            throw "Installer still contains the legacy hosts-file weather bridge."
        }
        if ($installerScript -notmatch 'deploy-service-and-verify' -or
            $installerScript -notmatch [regex]::Escape("{app}\WeatherService")) {
            throw "Installer does not deploy the protected weather service payload."
        }
        if ($installerScript -notmatch [regex]::Escape(
            'Parameters: "remove-service-deployment"')) {
            throw "Installer does not remove the protected service on uninstall."
        }
        if ($installerScript -match '(?m)^Source: .*Compatibility-Bootstrap' -or
            $installerScript -match '(?m)^Name: .*One-time Compatibility Bootstrap') {
            throw "Installer still installs or exposes the obsolete compatibility bootstrap."
        }
        if ($installerScript -notmatch '(?m)^UsePreviousAppDir=no\s*$') {
            throw "Installer may reuse a legacy product installation directory."
        }
        if ($installerScript -notmatch '(?m)^UsePreviousTasks=no\s*$') {
            throw "Installer may reuse a stale shortcut task selection."
        }
        if ($installerScript -notmatch [regex]::Escape(
            'Name: "{userdesktop}\EmilyDesk"; Filename: "{app}\EmilyDesk.exe"')) {
            throw "Desktop shortcut does not target the installed Dashboard executable."
        }
        if ($installerScript -notmatch [regex]::Escape(
            'Name: "{userstartup}\EmilyDesk"; Filename: "{app}\EmilyDesk.exe"')) {
            throw "Startup shortcut does not target the installed Dashboard executable."
        }
        if ($installerScript -notmatch 'Sleep\(15000\)') {
            throw "Installer does not allow real-time security scanning before final payload validation."
        }

        # Start-Process joins string-array arguments without preserving quoting.
        # Because the project path may contain spaces, ISCC previously parsed
        # pieces of the output path as additional script filenames.
        $argumentString =
            ('/O"{0}" /F"EmilyDeskInstaller" "{1}"' -f `
                $installerOutput, $scriptPath)

        @(
            "ISCC.Path=$iscc",
            "ISCC.Arguments=$argumentString",
            "ISCC.WorkingDirectory=$PSScriptRoot"
        ) | Set-Content "$logs\inno-command.txt"

        # Use Process directly instead of Start-Process. Some Windows sessions
        # expose duplicate Path/PATH environment entries; Start-Process attempts
        # to copy both into a case-insensitive dictionary and fails before ISCC
        # can launch.
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $iscc
        $startInfo.Arguments = $argumentString
        $startInfo.WorkingDirectory = $PSScriptRoot
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        if (-not $process.Start()) {
            throw "Inno Setup process could not be started."
        }
        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        $standardOutput | Set-Content -LiteralPath $stdoutLog
        $standardError | Set-Content -LiteralPath $stderrLog

        if ($process.ExitCode -ne 0) {
            throw "Inno Setup failed with exit code $($process.ExitCode)."
        }

        $compiledInstaller = Join-Path $installerOutput "EmilyDeskInstaller.exe"

        if (-not (Test-Path -LiteralPath $compiledInstaller -PathType Leaf)) {
            $generatedInstallers = Get-ChildItem -LiteralPath $installerOutput `
                -Filter "*.exe" -File -ErrorAction SilentlyContinue

            @(
                "ExpectedInstaller=$compiledInstaller",
                "GeneratedInstallers=" +
                    (($generatedInstallers | ForEach-Object { $_.FullName }) -join ";")
            ) | Set-Content "$logs\installer-output-discovery.txt"

            if ($generatedInstallers.Count -eq 1) {
                $compiledInstaller = $generatedInstallers[0].FullName
            } else {
                throw "Compiled installer was not found. " +
                      "See BuildLogs\installer-output-discovery.txt."
            }
        }

        Assert-File $compiledInstaller "Compiled installer"

        $rootInstaller = Join-Path $output "EmilyDeskInstaller.exe"
        Copy-FileWithRetry $compiledInstaller $rootInstaller
        Sign-ReleaseFile $rootInstaller
        Assert-File $rootInstaller "Final installer"

        $installerItem = Get-Item $rootInstaller
        $installerHash = (Get-FileHash $rootInstaller -Algorithm SHA256).Hash
        @(
            "Installer.SizeBytes=$($installerItem.Length)",
            "Installer.SHA256=$installerHash",
            "Installer.AuthenticodeSigned=$(-not [string]::IsNullOrWhiteSpace($SignCertificateThumbprint))",
            "Installer.Path=$rootInstaller"
        ) | Set-Content "$logs\installer-validation.txt"
    } else {
        Write-Host "[8/8] Installer skipped by request."
    }

    Write-Host ""
    Write-Host "BUILD COMPLETED SUCCESSFULLY"
    Write-Host "Full payload ZIP: $fullZip"
    Write-Host "Dashboard ZIP:    $dashboardZip"
    Write-Host "Widget packages:  $packageOutput"
    Write-Host "Optional widgets: $optionalWidgetPackageOutput"
    Write-Host "Theme packages:   $themePackageOutput"
    if (-not $SkipInstaller) {
        Write-Host "User installer:   $output\EmilyDeskInstaller.exe"
    }
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force `
            -ErrorAction SilentlyContinue
    }
}
