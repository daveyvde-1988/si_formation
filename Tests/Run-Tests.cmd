@echo off
setlocal
cd /d "%~dp0"
set "DOTNET_CLI_HOME=%~dp0.dotnet-home"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
dotnet run --project Tests.csproj --configuration Release --ignore-failed-sources
set "result=%errorlevel%"
echo.
pause
exit /b %result%
