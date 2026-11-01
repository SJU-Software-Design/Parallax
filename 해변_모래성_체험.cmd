@echo off
setlocal
set PSModulePath=
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-beach-castle-demo.ps1"
pause
