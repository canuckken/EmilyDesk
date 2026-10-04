@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
if errorlevel 1 (
    echo.
    echo Build failed. Check BuildLogs\build-errors.txt and the message above.
    pause
    exit /b 1
)
echo.
echo Finished installer: "%~dp0Output\EmilyDeskInstaller.exe"
pause
endlocal
