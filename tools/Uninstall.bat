@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manage-Install.ps1" -Mode Uninstall %*
set "result=%errorlevel%"
pause
exit /b %result%
