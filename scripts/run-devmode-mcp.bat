@echo off
setlocal
cd /d "%~dp0.."

set "MCP_DLL=tools\DevMode.Mcp\bin\Release\net8.0\KitLib.Mcp.dll"
if not exist "%MCP_DLL%" (
  dotnet build tools\DevMode.Mcp\KitLib.Mcp.csproj -c Release -v q
  if errorlevel 1 exit /b 1
)

if "%KITLIB_MCP_PORT%"=="" set "KITLIB_MCP_PORT=9877"
dotnet exec "%MCP_DLL%" -- --port %KITLIB_MCP_PORT%
exit /b %ERRORLEVEL%
