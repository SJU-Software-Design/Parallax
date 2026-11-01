@echo off
set "gameDir=%~dp0builds\archive\gallery-smooth-shadows-0.8.2"
if not exist "%gameDir%\Parallax.exe" (echo Archived Gallery player is missing. & pause & exit /b 1)
echo Opening archived Gallery v0.8.2. Source fixes require a new Unity Editor build.
pushd "%gameDir%"
start "" "Parallax.exe"
popd
