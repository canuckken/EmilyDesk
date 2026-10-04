@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -FullVerification
set "RESULT=%ERRORLEVEL%"

echo.
if not "%RESULT%"=="0" (
    echo RELEASE CHECK FAILED. Review the BuildLogs folder.
) else (
    echo Full release verification completed successfully.
)
pause
exit /b %RESULT%
