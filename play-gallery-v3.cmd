@echo off
set "gameDir=%~dp0builds\windows\gallery-v3"
if not exist "%gameDir%\Parallax.exe" (echo Gallery v3 build is missing. & pause & exit /b 1)
pushd "%gameDir%"
start "" "Parallax.exe"
popd
