# Building EmilyDesk on Windows

## Prerequisites

- Windows with Windows PowerShell 5.1.
- MSBuild for the classic .NET Framework projects and the **.NET Framework 4.0 targeting pack/reference assemblies**. The projects target `v4.0`; a runtime alone may produce `MSB3644` warnings or resolve the wrong assemblies.
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) for the installer.

## Build

1. Keep the repository's `src`, `Widgets`, `OptionalWidgets`, `Assets`, `ThemePackages`, `Wallpapers`, and root build files together in one folder.
2. Double-click `Build.cmd` in that folder. It invokes `Build.ps1` and runs the project's validation steps.
3. When it reports success, find the shareable installer at `Output\EmilyDeskInstaller.exe`. The same build produces `Output\OptionalWidgets\*.emilywidget`, `Output\Themes\*.emilytheme`, and the other packages under `Output`.

The build clears and recreates its own `BuildLogs`, `Stage`, and `Output` folders. Save any installer you intend to keep outside `Output` before running another build. It does not remove your source, artwork, or Windows documents.

If you want packages without an installer, run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -SkipInstaller` from a PowerShell window in the repository folder. The normal `Build.cmd` path requires Inno Setup.

## Signing and release

`Build.ps1` signs the installer only when `EMILYDESK_SIGN_CERT_SHA1` names a usable code-signing certificate and `signtool.exe` is installed. Without a certificate, the installer is unsigned and Windows may show a SmartScreen reputation prompt. Do not imply a release is signed unless the generated `BuildLogs\installer-validation.txt` says so.

To share a version, upload **the completed installer** to a GitHub Release. Keep generated `Output`, `Stage`, and `BuildLogs` out of the source repository; they are ignored by Git.
