@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-complete.ps1" %*
set "PARALLAX_LAUNCH_EXIT=%ERRORLEVEL%"
if not "%PARALLAX_LAUNCH_EXIT%"=="0" pause
exit /b %PARALLAX_LAUNCH_EXIT%
