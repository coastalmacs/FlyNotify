@echo off
echo ==============================================
echo  FlyNotify - Building and Packaging for Docker
echo ==============================================

set BUILD_DIR=%TEMP%\flynotify-build
set PUBLISH_DIR=%BUILD_DIR%\publish

if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
mkdir "%PUBLISH_DIR%"
if not exist "dist" mkdir "dist"

echo Compiling FlyNotify.Web for Linux...
dotnet publish "FlyNotify.Web\FlyNotify.Web.csproj" -c Release -o "%PUBLISH_DIR%" --no-self-contained

if %ERRORLEVEL% neq 0 (
    echo.
    echo Compilation failed! Please check the errors above.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Copying Docker files to package directory...
copy /y "Dockerfile" "%BUILD_DIR%\" >nul
copy /y "flynotify.yml" "%BUILD_DIR%\" >nul

echo Packaging deployment archive...
set ZIP_FILE=%CD%\dist\flynotify-docker.zip
powershell -NoProfile -Command "if (Test-Path '%ZIP_FILE%') { Remove-Item '%ZIP_FILE%' -Force; Start-Sleep -Milliseconds 300 }; Compress-Archive -Path '%PUBLISH_DIR%', '%BUILD_DIR%\Dockerfile', '%BUILD_DIR%\flynotify.yml' -DestinationPath '%ZIP_FILE%' -Force"

echo Cleaning temporary files...
rmdir /s /q "%BUILD_DIR%"

echo.
echo Packaging successful!
echo Upload 'dist\flynotify-docker.zip' to your QNAP NAS and extract it.
