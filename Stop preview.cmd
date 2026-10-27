@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\ui-preview.ps1" stop
if errorlevel 1 pause
