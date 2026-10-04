@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Apply-Art-Deco-Icon-Cleanup.ps1"
if errorlevel 1 (
  echo.
  echo The icon cleanup could not be applied. No other EmilyDesk files were changed.
) else (
  echo.
  echo Art Deco icon cleanup applied. Restart the Art Deco Weather widget.
)
pause
