@echo off
cd /d "%~dp0"
if not exist "Data" mkdir "Data"
if not exist "Data\trueautohdr.log" (
  echo No log has been created yet.
  echo.
  echo Run TrueAutoHDR.exe once, then try this file again.
  pause
  exit /b 0
)
start "" notepad "Data\trueautohdr.log"
