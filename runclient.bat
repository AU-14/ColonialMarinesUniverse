@echo off
REM CMU14
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\build-and-run.ps1" -Project Content.Client
set "exitCode=%errorlevel%"
pause
exit /b %exitCode%
