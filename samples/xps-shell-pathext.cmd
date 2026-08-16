@echo off
if not "%~1"=="safe" exit /b 9
echo SHELL-WINDOWS-RESOLUTION=OK>shell-windows-resolution.txt
exit /b 0
