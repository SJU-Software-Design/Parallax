@echo off
setlocal
set "PSModulePath="
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-stage.ps1" -Stage Gallery
if errorlevel 1 goto launch_failed
endlocal
exit /b 0
:launch_failed
pause
endlocal
exit /b 1
