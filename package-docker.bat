@echo off
echo Packaging Docker deployable files...
if not exist "dist" mkdir "dist"

if exist "dist\flynotify-docker.zip" del /f /q "dist\flynotify-docker.zip"

powershell -Command "Get-Item 'FlyNotify.Core', 'FlyNotify.Web', 'Dockerfile', 'docker-compose.yml', 'flynotify.yml' -ErrorAction SilentlyContinue | Compress-Archive -DestinationPath 'dist\flynotify-docker.zip' -Force"

echo.
echo Packaging successful! 
echo Upload the single file 'dist\flynotify-docker.zip' to your QNAP and extract it.
