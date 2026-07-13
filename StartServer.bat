@echo off
REM Vamsurlike Windows Dedicated Server launch script.
REM Update GAME_EXE below if the build output path changes.
REM Server and client must be built from the same commit + same data assets
REM so the CatalogVersionUtility hash matches on connect.

REM Switch console codepage to UTF-8 so Korean log output renders correctly.
chcp 65001 >nul

set GAME_EXE=%~dp0Builds\Server\Vamsurlike.exe
set SERVER_IP=0.0.0.0
set SERVER_PORT=7777

if not exist "%GAME_EXE%" (
    echo [StartServer] Build not found: %GAME_EXE%
    echo [StartServer] Build a Dedicated Server build in Unity first.
    pause
    exit /b 1
)

echo [StartServer] %GAME_EXE% -batchmode -nographics -server -ip %SERVER_IP% -port %SERVER_PORT%
"%GAME_EXE%" -batchmode -nographics -server -ip %SERVER_IP% -port %SERVER_PORT%
