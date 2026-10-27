@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\ui-preview.ps1" start
if errorlevel 1 pause
