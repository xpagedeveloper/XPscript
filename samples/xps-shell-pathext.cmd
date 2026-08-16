@echo off
if not "%~1"=="safe" exit /b 9
if "%GITHUB_WORKSPACE%"=="" exit /b 10
echo SHELL-WINDOWS-RESOLUTION=OK>"%GITHUB_WORKSPACE%\shell-windows-resolution.txt"
exit /b 0
