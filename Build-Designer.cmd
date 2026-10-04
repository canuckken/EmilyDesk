@echo off
setlocal
cd /d "%~dp0"
set "EMILYDESK_MSBUILD=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
if not exist "%EMILYDESK_MSBUILD%" set "EMILYDESK_MSBUILD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist "%EMILYDESK_MSBUILD%" (
  echo MSBuild could not be found.
  pause
  exit /b 1
)
"%EMILYDESK_MSBUILD%" "src\EmilyDesk.Designer\EmilyDesk.Designer.csproj" /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /nologo /verbosity:minimal
if errorlevel 1 (
  echo.
  echo DESIGNER BUILD FAILED.
  pause
  exit /b 1
)
echo.
echo DESIGNER BUILD SUCCEEDED.
if not exist "Output" mkdir "Output"
copy /y "src\EmilyDesk.Designer\bin\Release\EmilyDesk.Designer.exe" "Output\EmilyDesk.Designer.exe" >nul
if errorlevel 1 (
  echo Could not copy the Designer executable to the Output folder.
  pause
  exit /b 1
)
copy /y "src\EmilyDesk.Designer\bin\Release\*.dll" "Output\" >nul
if errorlevel 1 (
  echo Could not copy the Designer dependencies to the Output folder.
  pause
  exit /b 1
)
echo Designer executable and dependencies copied to Output.
echo Opening EmilyDesk Designer...
start "" "Output\EmilyDesk.Designer.exe"
endlocal
