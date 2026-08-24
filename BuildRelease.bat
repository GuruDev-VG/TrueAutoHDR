@echo off
setlocal EnableExtensions
set TRUEAUTOHDR_NO_PAUSE=1
cd /d "%~dp0"
title TrueAuto HDR 1.2.4 - Release Builder

echo ========================================
echo   TrueAuto HDR 1.2.4 Release Builder
echo ========================================
echo.

echo [1/2] Building portable release...
call "%~dp0BuildPortable.bat"
if errorlevel 1 goto :failed

echo.
echo [2/2] Building installer release...
call "%~dp0BuildInstaller.bat"
if errorlevel 1 goto :failed

echo.
echo ========================================
echo RELEASE BUILD COMPLETE
echo ========================================
echo Portable:
echo   release\Portable\TrueAutoHDR-1.2.4-Portable.zip
echo Installer:
echo   release\Installer\TrueAutoHDR-1.2.4-Setup.exe
echo.
pause
exit /b 0

:failed
echo.
echo ========================================
echo RELEASE BUILD FAILED
echo ========================================
echo Packaging stopped because one of the release targets failed.
pause
exit /b 1
