@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title TrueAuto HDR - Canary Update Builder
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0MakeAppUpdatePackage.ps1" -Version "1.5.0-canary.1" -Channel Canary
set "RESULT=%ERRORLEVEL%"
echo.
if not "%RESULT%"=="0" (echo CANARY UPDATE BUILD FAILED) else (echo Canary update ready in UpdatePackages\Canary)
echo.
pause
exit /b %RESULT%
