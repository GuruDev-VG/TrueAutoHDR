@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title TrueAuto HDR 1.2.4 - Installer Builder

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] .NET 8 SDK not found in PATH.
  if not defined TRUEAUTOHDR_NO_PAUSE pause
  exit /b 1
)

set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo [ERROR] Inno Setup 6 was not found.
  if not defined TRUEAUTOHDR_NO_PAUSE pause
  exit /b 1
)

set "OUT=%~dp0release\InstallerInput"
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%" >nul 2>nul
if not exist "%~dp0release\Installer" mkdir "%~dp0release\Installer" >nul 2>nul

echo [1/6] Building updater...
call "%~dp0BuildUpdater.bat"
if errorlevel 1 goto :failed

echo [2/6] Publishing installer payload...
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

echo [3/6] Adding updater...
copy /y "%~dp0publish-updater\TrueAutoHDR.Updater.exe" "%OUT%\TrueAutoHDR.Updater.exe" >nul
if errorlevel 1 goto :failed

echo [4/6] Verifying payload...
if not exist "%OUT%\TrueAutoHDR.exe" goto :missing
if not exist "%OUT%\TrueAutoHDR.Updater.exe" goto :missing
if not exist "%OUT%\Database\native_hdr_database.json" goto :missing
if not exist "%OUT%\Database\community_hdr_names.json" goto :missing

echo [5/6] Running installer-payload self-test...
"%OUT%\TrueAutoHDR.exe" --self-test
if errorlevel 1 (
  echo [ERROR] Installer payload failed self-test.
  goto :failed
)

echo [6/6] Compiling installer...
"%ISCC%" "%~dp0Installer\TrueAutoHDR.iss"
if errorlevel 1 goto :failed

echo.
echo Installer complete:
echo   %~dp0release\Installer\TrueAutoHDR-1.2.4-Setup.exe
if not defined TRUEAUTOHDR_NO_PAUSE pause
exit /b 0

:missing
echo [ERROR] Required release file is missing.
goto :failed

:failed
echo.
echo ========================================
echo INSTALLER BUILD FAILED
echo ========================================
if not defined TRUEAUTOHDR_NO_PAUSE pause
exit /b 1
