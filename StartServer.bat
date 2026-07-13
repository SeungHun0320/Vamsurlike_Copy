@echo off
REM Vamsurlike Windows Dedicated Server 실행 스크립트.
REM 빌드 출력 경로는 실제 빌드 후 맞게 수정하세요 (기본 가정: Builds\Server\Vamsurlike.exe).
REM 서버/클라이언트는 반드시 같은 커밋 + 같은 데이터 에셋으로 빌드해야 CatalogVersionUtility 해시가 일치합니다.

set GAME_EXE=%~dp0Builds\Server\Vamsurlike.exe
set SERVER_IP=0.0.0.0
set SERVER_PORT=7777

if not exist "%GAME_EXE%" (
    echo [StartServer] 빌드 파일을 찾을 수 없습니다: %GAME_EXE%
    echo [StartServer] Unity에서 Dedicated Server Build를 먼저 만들어주세요.
    pause
    exit /b 1
)

echo [StartServer] %GAME_EXE% -batchmode -nographics -server -ip %SERVER_IP% -port %SERVER_PORT%
"%GAME_EXE%" -batchmode -nographics -server -ip %SERVER_IP% -port %SERVER_PORT%
