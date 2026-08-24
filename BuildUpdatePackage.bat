@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title TrueAuto HDR 1.3.0 - App Update Package

echo ========================================
echo TrueAuto HDR 1.3.0 App Update Package
echo ========================================
echo.

where powershell.exe >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Windows PowerShell was not found.
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0MakeAppUpdatePackage.ps1"
set "RESULT=%ERRORLEVEL%"

echo.
if not "%RESULT%"=="0" (
    echo ========================================
    echo UPDATE PACKAGE FAILED
    echo ========================================
    echo.
    echo Open this file and send its contents if needed:
    echo   %~dp0MakeAppUpdatePackage.log
) else (
    echo ========================================
    echo UPDATE PACKAGE READY
    echo ========================================
    echo.
    echo Output folder:
    echo   %~dp0UpdatePackages
)

echo.
pause
exit /b %RESULT%
