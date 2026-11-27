@echo off
rem ---------------------------------------------------------------------
rem  Portal de Matricula MPAZ · pruebas automatizadas
rem  Atajo de Windows para scripts\pruebas-automatizadas.ps1
rem
rem  Uso:  scripts\pruebas-automatizadas.bat
rem        scripts\pruebas-automatizadas.bat -Configuracion Debug
rem        scripts\pruebas-automatizadas.bat -SinCompilar
rem ---------------------------------------------------------------------
chcp 65001 >nul
setlocal

set "SCRIPT=%~dp0pruebas-automatizadas.ps1"

if not exist "%SCRIPT%" (
    echo No se encuentra %SCRIPT%
    exit /b 2
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
exit /b %ERRORLEVEL%
