@echo off
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File create_icon.ps1
dotnet build PeriodicAccessTool.csproj -c Release
pause
