@echo off
chcp 65001 >nul
cd /d "%~dp0"
dotnet build "ProductionLine.UserApp\ProductionLine.UserApp.csproj" --artifacts-path "%~dp0artifacts-ui" --nologo
if errorlevel 1 goto failed
dotnet "%~dp0artifacts-ui\bin\ProductionLine.UserApp\debug\ProductionLine.UserApp.Moon.dll"
exit /b %errorlevel%
:failed
if errorlevel 1 pause
