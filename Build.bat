@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title TrueAuto HDR 1.3.1 Builder

echo ========================================
echo       TrueAuto HDR 1.3.1 Builder
echo ========================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] .NET 8 SDK not found in PATH.
  pause
  exit /b 1
)

set "OUT=%~dp0publish"
if exist "%OUT%" rmdir /s /q "%OUT%"

echo [1/5] Building updater...
call "%~dp0BuildUpdater.bat"
if errorlevel 1 goto :failed

echo [2/5] Publishing TrueAuto HDR...
dotnet publish "%~dp0AutoHDR.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:PublishReadyToRun=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%OUT%"
if errorlevel 1 goto :failed

echo [3/5] Adding updater...
copy /y "%~dp0publish-updater\TrueAutoHDR.Updater.exe" "%OUT%\TrueAutoHDR.Updater.exe" >nul
if errorlevel 1 goto :failed

echo [4/5] Running build self-test...
"%OUT%\TrueAutoHDR.exe" --self-test
if errorlevel 1 (
  echo [ERROR] Built application failed self-test.
  goto :failed
)

echo [5/5] Build finished successfully.
echo.
echo Output:
echo   %OUT%\TrueAutoHDR.exe
echo   %OUT%\TrueAutoHDR.Updater.exe
echo.
explorer "%OUT%"
pause
exit /b 0

:failed
echo.
echo ========================================
echo BUILD FAILED
echo ========================================
echo Review the errors above and trueautohdr.log if self-test failed.
pause
exit /b 1
