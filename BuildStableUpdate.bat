@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title TrueAuto HDR - Stable Update Builder
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0MakeAppUpdatePackage.ps1" -Version "1.5.0" -Channel Stable
set "RESULT=%ERRORLEVEL%"
echo.
if not "%RESULT%"=="0" (echo STABLE UPDATE BUILD FAILED) else (echo Stable update ready in UpdatePackages\Stable)
echo.
pause
exit /b %RESULT%
