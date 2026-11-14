@echo off
rem Vai buscar a versao nova ao GitHub, prepara a app em C:\HolidayApp\app e arranca-a.
rem Os dados (C:\HolidayApp\dados) nao sao tocados.
title Atualizar HolidayApp

echo Fecha a janela "HolidayApp" (a app a correr), se estiver aberta.
pause

cd /d "%~dp0.."
git pull || goto erro
dotnet publish src\HolidayApp.Web -c Release -o C:\HolidayApp\app || goto erro
if not exist C:\HolidayApp\dados mkdir C:\HolidayApp\dados
copy /y scripts\iniciar.bat C:\HolidayApp\iniciar.bat >nul || goto erro

echo.
echo Atualizado. A arrancar a app...
start "" C:\HolidayApp\iniciar.bat
exit /b 0

:erro
echo.
echo Algo correu mal (ver as mensagens acima). A app nao foi atualizada.
echo Se diz que um ficheiro esta "em uso", fecha a janela "HolidayApp" e tenta outra vez.
pause
exit /b 1
