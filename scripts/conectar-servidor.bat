@echo off
setlocal

REM ============================================================
REM  Conexion SSH al servidor del colegio (mpaz-server)
REM ============================================================
REM  Sin argumentos usa Tailscale (funciona desde cualquier red):
REM      conectar-servidor.bat
REM  Con la IP de la red del colegio:
REM      conectar-servidor.bat 192.168.50.233
REM
REM  Requiere que Tailscale este activo en este equipo, o estar
REM  en la red del colegio. La clave la pide el propio SSH.
REM ============================================================

set "USUARIO=mpaz-server"
set "HOST=%~1"
if "%HOST%"=="" set "HOST=100.93.143.61"

echo.
echo  Servidor del colegio: %USUARIO%@%HOST%
echo  (Ctrl+C para cancelar)
echo.

ssh -o ConnectTimeout=10 %USUARIO%@%HOST%

if errorlevel 1 (
    echo.
    echo  No se pudo conectar.
    echo   - Fuera del colegio: revisa que Tailscale este activo y el servidor encendido.
    echo   - Dentro del colegio: prueba la red local con
    echo       %~nx0 192.168.50.233
    echo.
    pause
)

endlocal
