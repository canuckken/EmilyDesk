@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass ^
  -File "%~dp0Compatibility-Audit.ps1" -OpenReport

set "RESULT=%ERRORLEVEL%"
echo.
if "%RESULT%"=="0" (
  echo Compatibility audit passed.
) else (
  echo Compatibility audit found one or more failed checks.
)
pause
exit /b %RESULT%
