@echo off
rem Gets the new version from GitHub, publishes the app to C:\HolidayApp\app and starts it.
rem The data (C:\HolidayApp\dados) is not touched.
title Update HolidayApp

echo Close the "HolidayApp" window (the running app), if it is open.
pause

cd /d "%~dp0.."
git pull || goto failed
dotnet publish src\HolidayApp.Web -c Release -o C:\HolidayApp\app || goto failed
if not exist C:\HolidayApp\dados mkdir C:\HolidayApp\dados
copy /y scripts\start.bat C:\HolidayApp\start.bat >nul || goto failed

rem The script used to be called iniciar.bat: shortcuts to it keep working and start the new one
if exist C:\HolidayApp\iniciar.bat (echo @call "%%~dp0start.bat") > C:\HolidayApp\iniciar.bat

echo.
echo Updated. Starting the app...
start "" C:\HolidayApp\start.bat
exit /b 0

:failed
echo.
echo Something went wrong (see the messages above). The app was not updated.
echo If it says a file is "in use", close the "HolidayApp" window and try again.
pause
exit /b 1
