@echo off
setlocal
cd /d "%~dp0.."

set "MCP_PROJECT=tools\KitLib.Mcp\KitLib.Mcp.csproj"
set "MCP_OUT=tools\KitLib.Mcp\bin\Release\net10.0"
set "MCP_EXE=%MCP_OUT%\KitLib.Mcp.exe"
set "MCP_STAGING=build\mcp-cursor-staging"

rem Cursor must connect to stdio quickly. Build only when missing or KITLIB_MCP_REBUILD=1.
if "%KITLIB_MCP_REBUILD%"=="1" goto build
if exist "%MCP_EXE%" goto run

:build
dotnet build "%MCP_PROJECT%" -c Release -v q
if not errorlevel 1 goto run

echo [KitLib.Mcp] Build to bin failed; retrying to staging... >&2
dotnet build "%MCP_PROJECT%" -c Release -v q -o "%MCP_STAGING%"
if not errorlevel 1 (
  set "MCP_EXE=%MCP_STAGING%\KitLib.Mcp.exe"
  echo [KitLib.Mcp] Using staging build (bin may be locked by a running MCP server). >&2
  goto run
)

if exist "%MCP_OUT%\KitLib.Mcp.exe" (
  set "MCP_EXE=%MCP_OUT%\KitLib.Mcp.exe"
  echo [KitLib.Mcp] Build failed; using existing bin EXE (may be stale). Set KITLIB_MCP_REBUILD=1 after stopping MCP. >&2
  goto run
)

echo [KitLib.Mcp] Build failed and no executable found. Requires .NET 10 runtime. >&2
exit /b 1

:run
if "%KITLIB_MCP_PORT%"=="" set "KITLIB_MCP_PORT=9877"
"%MCP_EXE%" --port %KITLIB_MCP_PORT%
exit /b %ERRORLEVEL%
