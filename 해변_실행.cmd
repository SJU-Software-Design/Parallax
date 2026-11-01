@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-stage.ps1" -Stage Beach
if errorlevel 1 pause
endlocal
