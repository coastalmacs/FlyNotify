@echo off
echo Publishing FlyNotify as a single-file executable...
dotnet publish "FlyNotify\FlyNotify.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=true

if %ERRORLEVEL% neq 0 (
    echo.
    echo Publish failed! Please check the errors above.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Publish successful! Creating dist folder...
if not exist "dist" mkdir "dist"

echo Copying executable to dist folder...
copy "FlyNotify\bin\Release\net10.0-windows\win-x64\publish\FlyNotify.exe" "dist\"

echo.
echo Standalone executable is ready in the 'dist' folder.
echo Opening dist folder...
explorer "dist"

pause
