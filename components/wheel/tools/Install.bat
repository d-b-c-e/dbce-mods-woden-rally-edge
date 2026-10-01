@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Manage-Install.ps1" -Mode Install %*
set "result=%errorlevel%"
pause
exit /b %result%
