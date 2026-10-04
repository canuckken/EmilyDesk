@echo off
setlocal
cd /d "%~dp0"

where git.exe >nul 2>&1
if errorlevel 1 (
  echo Git is not installed or is not available in PATH.
  echo Install Git for Windows, then run this file again.
  pause
  exit /b 1
)

if exist ".git" (
  echo This folder is already a Git repository.
  git status --short
  pause
  exit /b 0
)

git init
if errorlevel 1 goto :failed

git add .
if errorlevel 1 goto :failed

git commit -m "XWidget Reborn 2.2.1 compatibility audit baseline"
if errorlevel 1 (
  echo.
  echo Git could not create the commit.
  echo Configure your Git name and email, then run:
  echo   git add .
  echo   git commit -m "XWidget Reborn 2.2.1 compatibility audit baseline"
  pause
  exit /b 2
)

git tag -a v2.2.1-baseline -m "Working installer, service, dashboard, and compatibility audit"
if errorlevel 1 goto :failed

echo.
echo Git repository created successfully.
echo Baseline tag: v2.2.1-baseline
pause
exit /b 0

:failed
echo Git initialization failed.
pause
exit /b 3
