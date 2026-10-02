@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Install the .NET 8 SDK or newer, then run this file again.
  pause
  exit /b 1
)
dotnet build Si_Formation.csproj -c Release --ignore-failed-sources
if errorlevel 1 (
  echo BUILD FAILED. Nothing was installed.
  pause
  exit /b 1
)
echo.
echo Built: %~dp0bin\Release\netstandard2.1\Si_Formation.dll
echo Nothing was installed. See README.md for manual installation and testing.
pause
