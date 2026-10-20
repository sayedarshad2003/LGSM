@echo off
setlocal
echo ========================================================
echo         Building LGMS Desktop Standalone Release
echo ========================================================
echo.

set PROJECT_PATH=LGMS.Desktop\LGMS.Desktop.csproj
set OUTPUT_PATH=publish\desktop-standalone

echo Running dotnet publish...
dotnet publish "%PROJECT_PATH%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUTPUT_PATH%"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Copying README instructions...
    if exist "%OUTPUT_PATH%\README.txt" (
        echo README.txt already exists.
    )
    echo Packaging ZIP archive for sharing...
    powershell -NoProfile -Command "Compress-Archive -Path '%OUTPUT_PATH%\LGMS.Desktop.exe', '%OUTPUT_PATH%\README.txt' -DestinationPath 'publish\LGMS-Desktop-Windows-x64.zip' -Force"
    echo.
    echo ========================================================
    echo  SUCCESS! Ready to share:
    echo  1. Executable: %~dp0%OUTPUT_PATH%\LGMS.Desktop.exe
    echo  2. Zip Package: %~dp0publish\LGMS-Desktop-Windows-x64.zip
    echo ========================================================
) else (
    echo.
    echo [ERROR] Build failed! Check the errors above.
)

echo.
pause
