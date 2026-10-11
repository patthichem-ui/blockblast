@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 exit /b 1

dotnet run --project "src\BlockBlast.WinForms\BlockBlast.WinForms.csproj" -c Release
set "game_exit_code=%errorlevel%"
popd

if not "%game_exit_code%"=="0" (
    echo.
    echo Game chua chay duoc. Hay kiem tra .NET 9 SDK 9.0.305 hoac 9.0.306.
    echo Neu vua tai source moi, hay giai nen vao thu muc moi roi thu lai.
    pause
)
exit /b %game_exit_code%
