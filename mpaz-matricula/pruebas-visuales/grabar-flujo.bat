@echo off
REM ============================================================
REM  Grabación del flujo completo · Portal de Matrícula MPAZ
REM
REM  Qué hace:
REM    1) Comprueba Node y Playwright.
REM    2) Levanta el portal .NET si no responde en localhost:5199.
REM    3) Abre el navegador en el portal y ESPERA a que pulse ENTER en
REM       esta ventana para comenzar la grabacion (o arranca solo a los
REM       90 segundos). Recorre TODOS los flujos de todos los roles.
REM    4) Graba un recorrido de al menos 100 segundos.
REM
REM  Resultado:
REM    pruebas-visuales\evidencias\grabar-flujo\flujo-mpaz.webm
REM
REM  Variables opcionales:
REM    BASE_URL=http://localhost:5199
REM    USUARIO=mteresa.lagos@mpaz.cl
REM    CLAVE=...  CLAVE_NUEVA=...
REM    HEADLESS=1 para grabar sin mostrar el navegador
REM ============================================================
setlocal EnableExtensions

cd /d "%~dp0"

if not defined BASE_URL set "BASE_URL=http://localhost:5199"
if not defined MIN_VIDEO_MS set "MIN_VIDEO_MS=100000"
if not defined HEADLESS set "HEADLESS=0"

echo.
echo ============================================================
echo  Portal de Matrícula MPAZ - Grabación del flujo completo
echo ============================================================
echo  Destino del video: %CD%\evidencias\grabar-flujo\flujo-mpaz.webm
echo  Duración mínima:   100 segundos
echo.

where node >nul 2>nul
if errorlevel 1 (
  echo [ERROR] No se encontró Node.js. Instálelo desde https://nodejs.org/ y vuelva a ejecutar.
  pause
  exit /b 1
)

for /f "tokens=*" %%V in ('node --version') do set NODE_VERSION=%%V
echo Node: %NODE_VERSION%

if not exist "package.json" (
  echo [ERROR] No se encontró package.json en %CD%.
  pause
  exit /b 1
)

if not exist "node_modules\playwright" (
  echo Instalando Playwright...
  call npm install
  if errorlevel 1 (
    echo [ERROR] No se pudo instalar Playwright.
    pause
    exit /b 1
  )
)

echo Comprobando Chromium de Playwright...
call npx playwright install chromium
if errorlevel 1 (
  echo [ERROR] No se pudo instalar Chromium.
  pause
  exit /b 1
)

echo.
echo Iniciando la grabación. El navegador se abrirá en la pantalla.
echo Puede cerrar esta ventana al terminar; el video quedará guardado.
echo.

node grabar-flujo.mjs --levantar-servidor %*
set EXIT_CODE=%ERRORLEVEL%

echo.
if "%EXIT_CODE%"=="0" (
  echo [OK] Grabación terminada.
  echo Video: %CD%\evidencias\grabar-flujo\flujo-mpaz.webm
) else (
  echo [ERROR] La grabación terminó con código %EXIT_CODE%.
  echo Revise la consola y la carpeta evidencias\grabar-flujo.
)

pause
exit /b %EXIT_CODE%
