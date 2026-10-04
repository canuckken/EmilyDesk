@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
set "RESULT=%ERRORLEVEL%"
echo.
if not "%RESULT%"=="0" (
  echo BUILD FAILED. Review the BuildLogs folder.
) else (
  echo Build completed successfully.
)
pause
exit /b %RESULT%
