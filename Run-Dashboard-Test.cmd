@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-Dashboard-Test.ps1"
set "RESULT=%ERRORLEVEL%"

echo.
if not "%RESULT%"=="0" (
  echo EmilyDesk Dashboard test failed with code %RESULT%.
) else (
  echo EmilyDesk Dashboard startup test completed successfully.
)
pause
exit /b %RESULT%
