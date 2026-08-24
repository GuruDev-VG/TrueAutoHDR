@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title TrueAuto HDR 1.2.6 - Portable Builder

set "OUT=%~dp0release\Portable\TrueAutoHDR"
set "ZIP=%~dp0release\Portable\TrueAutoHDR-1.2.6-Portable.zip"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] .NET 8 SDK not found in PATH.
  if not defined TRUEAUTOHDR_NO_PAUSE pause
  exit /b 1
)

if exist "%OUT%" rmdir /s /q "%OUT%"
if exist "%ZIP%" del /q "%ZIP%"
mkdir "%OUT%" >nul 2>nul

echo [1/6] Building updater...
call "%~dp0BuildUpdater.bat"
if errorlevel 1 goto :failed

echo [2/6] Publishing portable app...
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

echo [3/6] Adding updater and portable marker...
copy /y "%~dp0publish-updater\TrueAutoHDR.Updater.exe" "%OUT%\TrueAutoHDR.Updater.exe" >nul
if errorlevel 1 goto :failed
echo portable>"%OUT%\portable.mode"
copy /y "%~dp0OpenPortableLog.bat" "%OUT%\OpenPortableLog.bat" >nul

echo [4/6] Verifying payload...
if not exist "%OUT%\TrueAutoHDR.exe" goto :missing
if not exist "%OUT%\TrueAutoHDR.Updater.exe" goto :missing
if not exist "%OUT%\Database\native_hdr_database.json" goto :missing
if not exist "%OUT%\Database\community_hdr_names.json" goto :missing

echo [5/6] Running portable self-test...
"%OUT%\TrueAutoHDR.exe" --self-test --portable
if errorlevel 1 (
  echo [ERROR] Portable build failed self-test.
  goto :failed
)

echo [6/6] Creating archive...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Compress-Archive -Path '%OUT%\*' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 goto :failed

echo.
echo Portable build complete:
echo   %ZIP%
if not defined TRUEAUTOHDR_NO_PAUSE pause
exit /b 0

:missing
echo [ERROR] Required release file is missing.
goto :failed

:failed
echo.
echo ========================================
echo PORTABLE BUILD FAILED
echo ========================================
if not defined TRUEAUTOHDR_NO_PAUSE pause
exit /b 1
