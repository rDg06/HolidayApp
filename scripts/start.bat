@echo off
rem HolidayApp on the home server (see README, "Home server").
rem update.bat copies this file to C:\HolidayApp\start.bat.
title HolidayApp

set ASPNETCORE_URLS=http://0.0.0.0:5180
set ASPNETCORE_ENVIRONMENT=Production
set ConnectionStrings__Default=Data Source=C:\HolidayApp\dados\holidayapp.db

rem Accounts: C:\HolidayApp\dados\invite-code.txt (one line, only on the server) is the INVITE CODE
rem needed to create an account. Each person then signs in with their own email and password.
rem The older senha.txt still works. Without either file the app opens with no sign-in: home network only.
set INVITE_FILE=
if exist C:\HolidayApp\dados\senha.txt set INVITE_FILE=C:\HolidayApp\dados\senha.txt
if exist C:\HolidayApp\dados\invite-code.txt set INVITE_FILE=C:\HolidayApp\dados\invite-code.txt

if defined INVITE_FILE (
    set /p Auth__InviteCode=<"%INVITE_FILE%"
    echo Accounts on ^(the invite code to create an account is in %INVITE_FILE%^).
) else (
    set Auth__Disabled=true
    echo.
    echo  WARNING: no accounts and no sign-in ^(C:\HolidayApp\dados\invite-code.txt is missing^).
    echo  Use it on the home network only and do NOT turn on Tailscale Funnel.
    echo.
)

cd /d C:\HolidayApp\app
dotnet HolidayApp.Web.dll
