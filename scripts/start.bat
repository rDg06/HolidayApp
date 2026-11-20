@echo off
rem HolidayApp on the home server (see README, "Home server").
rem update.bat copies this file to C:\HolidayApp\start.bat.
title HolidayApp

set ASPNETCORE_URLS=http://0.0.0.0:5180
set ASPNETCORE_ENVIRONMENT=Production
set ConnectionStrings__Default=Data Source=C:\HolidayApp\dados\holidayapp.db

rem Accounts (always on): C:\HolidayApp\dados\invite-code.txt (one line, only on the server) is the
rem INVITE CODE needed to create an account. Each person then signs in with their own email (or user
rem name) and password. The older senha.txt still works. Without either file, a random code is
rem created in invite-code.txt the first time.
set INVITE_FILE=
if exist C:\HolidayApp\dados\senha.txt set INVITE_FILE=C:\HolidayApp\dados\senha.txt
if exist C:\HolidayApp\dados\invite-code.txt set INVITE_FILE=C:\HolidayApp\dados\invite-code.txt

rem (outside an "if ( )" block: the PowerShell command has brackets)
if defined INVITE_FILE goto have_code
if not exist C:\HolidayApp\dados mkdir C:\HolidayApp\dados
powershell -NoProfile -Command "$c = -join ((48..57) + (97..122) | Get-Random -Count 12 | ForEach-Object { [char]$_ }); Set-Content C:\HolidayApp\dados\invite-code.txt -Value $c -Encoding ascii -NoNewline"
set INVITE_FILE=C:\HolidayApp\dados\invite-code.txt
echo New invite code created in C:\HolidayApp\dados\invite-code.txt

:have_code
set /p Auth__InviteCode=<"%INVITE_FILE%"
echo Accounts on ^(the invite code to create an account is in %INVITE_FILE%^).

cd /d C:\HolidayApp\app
dotnet HolidayApp.Web.dll
