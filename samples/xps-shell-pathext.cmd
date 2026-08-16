@echo off
if not "%~1"=="safe" exit /b 9
if "%XPS_SHELL_MARKER%"=="" exit /b 10
echo SHELL-WINDOWS-RESOLUTION=OK>"%XPS_SHELL_MARKER%"
exit /b 0
