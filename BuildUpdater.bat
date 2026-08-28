@echo off
setlocal EnableExtensions
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] .NET 8 SDK not found in PATH.
  exit /b 1
)

set "OUT=%~dp0publish-updater"
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%" >nul 2>nul

echo Building self-contained TrueAuto HDR updater...
dotnet publish "%~dp0Updater\TrueAutoHDR.Updater.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%OUT%"
if errorlevel 1 exit /b 1

if not exist "%OUT%\TrueAutoHDR.Updater.exe" (
  echo [ERROR] Updater publish completed but TrueAutoHDR.Updater.exe is missing.
  exit /b 1
)

echo Updater built successfully.
exit /b 0
