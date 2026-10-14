@echo off
rem Optional, private: set up Super Woden: Rally Edge's shared wheel controls without Wheelkit.
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Extras\Setup-Controls.ps1" %*
set "result=%errorlevel%"
pause
exit /b %result%
