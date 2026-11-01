@echo off
set "gameDir=%~dp0builds\windows\gallery-v4"
if not exist "%gameDir%\Parallax.exe" (echo Gallery v4 build is missing. & pause & exit /b 1)
pushd "%gameDir%"
start "" "Parallax.exe"
popd
