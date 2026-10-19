@echo off
REM One-click build for Dism-Plus-Next (double-click to run).
REM Right-click -> Run as administrator if you also want to launch the app elevated.
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0build.ps1"
pause
