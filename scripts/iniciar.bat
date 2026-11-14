@echo off
rem HolidayApp no servidor de casa (ver README, "Servidor de casa").
rem O atualizar.bat copia este ficheiro para C:\HolidayApp\iniciar.bat.
title HolidayApp

set ASPNETCORE_URLS=http://0.0.0.0:5180
set ASPNETCORE_ENVIRONMENT=Production
set ConnectionStrings__Default=Data Source=C:\HolidayApp\dados\holidayapp.db

rem Palavra-passe do grupo: uma linha em C:\HolidayApp\dados\senha.txt (fica so no servidor).
rem Sem esse ficheiro a app abre sem login: serve so para a rede de casa.
if exist C:\HolidayApp\dados\senha.txt (
    set /p Auth__GroupPassword=<C:\HolidayApp\dados\senha.txt
    echo Com palavra-passe do grupo.
) else (
    set Auth__Disabled=true
    echo.
    echo  ATENCAO: sem palavra-passe ^(falta C:\HolidayApp\dados\senha.txt^).
    echo  Usa so na rede de casa e NAO ligues o Tailscale Funnel.
    echo.
)

cd /d C:\HolidayApp\app
dotnet HolidayApp.Web.dll
