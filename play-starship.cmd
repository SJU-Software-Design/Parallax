@echo off
set "gameDir=%~dp0builds\windows\all-stages\Parallax_Unity_v0.13.1_Starship"
if not exist "%gameDir%\Parallax.exe" (echo Starship build is missing. Build projects\all-stages first. & pause & exit /b 1)
pushd "%gameDir%"
start "" "Parallax.exe"
popd
