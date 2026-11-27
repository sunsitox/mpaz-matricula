#requires -Version 5.1
<#
.SYNOPSIS
    Corre todas las pruebas automatizadas del Portal de Matrícula MPAZ y las
    muestra como checklist por módulo.

.DESCRIPTION
    Ejecuta, en este orden:
      1. Compilación Release de la solución (errores y advertencias).
      2. Sintaxis de los JavaScript del portal (node --check).
      3. Pruebas xUnit (dotnet test) con desglose por clase.
      4. Estado del plan manual (docs/plan-pruebas-portal-mpaz.xlsx).

    Al final imprime un checklist por módulo, el estado del plan manual y un
    resumen. Devuelve 0 si compilación y pruebas pasan; 1 si algo falla.

.PARAMETER Configuracion
    Release (por defecto) o Debug.

.PARAMETER SinCompilar
    Reutiliza la última compilación: solo corre pruebas y checklist.

.EXAMPLE
    scripts\pruebas-automatizadas.bat
    powershell -ExecutionPolicy Bypass -File scripts\pruebas-automatizadas.ps1 -Configuracion Debug
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuracion = 'Release',
    [switch]$SinCompilar
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }

$raiz = Split-Path -Parent $PSScriptRoot
$solucion = Join-Path $raiz 'mpaz-matricula\MarcelaPaz.Matriculas.slnx'
$web = Join-Path $raiz 'mpaz-matricula\MarcelaPaz.Web'
$evidencia = Join-Path $raiz 'artifacts\pruebas'

# Clases de prueba -> módulo del plan. Lo que no esté acá se informa aparte.
$modulos = [ordered]@{
    '02 · Asistente de ficha'              = @('FichaEtapa1Tests', 'ValidacionPasos2a5Tests', 'BloqueCTests', 'DocumentoAdultoTests', 'NivelesCursoTests')
    '03 · Estudiantes y expediente'        = @('ExpedienteDocumentosTests', 'ValidacionMatriculaTests', 'AnulacionMatriculaTests', 'CorreccionesAuditoriaTests', 'AutorizacionesTests')
    '05 · Alertas y reportes'              = @('AlertasPaginaTests', 'ReportesCsvTests')
    '06 · Fichas médicas del personal'     = @('FichaSaludFuncionarioTests')
    '08 · Certificados'                    = @('CertificadoAlumnoRegularTests')
    '10 · Transversal y persistencia'      = @('ConexionBaseDatosTests', 'ConexionVivaTests', 'ModeloPersistenciaTests', 'RegistroWebServiceTests')
}

# Módulos que hoy no tienen prueba automatizada y por qué.
$sinAutomaticas = [ordered]@{
    '01 · Acceso y sesión'      = 'login, cookie y bloqueo viven en Identity'
    '04 · Medicación TENS'      = 'las reglas están en triggers PL/pgSQL'
    '07 · Funcionarios y roles' = 'alta de cuentas vía UserManager'
    '09 · Configuración'        = 'AnioEscolar usa SQL crudo'
}

function Linea { param([string]$Texto = '') Write-Host $Texto }
function Titulo { param([string]$Texto) Write-Host ''; Write-Host $Texto -ForegroundColor Cyan; Write-Host ('-' * 78) -ForegroundColor DarkGray }
function Bien   { param([string]$Texto) Write-Host "  [OK] $Texto" -ForegroundColor Green }
function Mal    { param([string]$Texto) Write-Host "  [XX] $Texto" -ForegroundColor Red }
function Aviso  { param([string]$Texto) Write-Host "  [--] $Texto" -ForegroundColor Yellow }

function Ejecutar {
    param([string[]]$Argumentos, [string]$Directorio = $raiz)

    $previo = $PWD.Path
    Push-Location $Directorio
    try {
        $salida = & dotnet @Argumentos 2>&1 | ForEach-Object { $_.ToString() }
        $codigo = $LASTEXITCODE
    } finally {
        Pop-Location
    }

    [pscustomobject]@{ Codigo = $codigo; Salida = ($salida -join "`n") }
}

# ---------------------------------------------------------------- encabezado
$version = (& dotnet --version 2>$null)
if (-not $version) {
    Write-Host 'No se encontró el SDK de .NET (dotnet) en el PATH.' -ForegroundColor Red
    exit 2
}

Write-Host ''
Write-Host ('=' * 78) -ForegroundColor DarkGray
Write-Host ' PORTAL DE MATRÍCULA MPAZ · PRUEBAS AUTOMATIZADAS' -ForegroundColor White
Write-Host (" {0} · {1} · .NET {2}" -f (Get-Date -Format 'dd-MM-yyyy HH:mm'), $Configuracion, $version.Trim()) -ForegroundColor DarkGray
Write-Host ('=' * 78) -ForegroundColor DarkGray

$fallos = 0
$reloj = [Diagnostics.Stopwatch]::StartNew()

# ------------------------------------------------- 1. compilación Release
Titulo '[1/4] Compilación'

if ($SinCompilar) {
    Aviso 'Omitida (-SinCompilar): se usa la última compilación.'
} else {
    $reloj.Restart()
    $build = Ejecutar @('build', $solucion, '-c', $Configuracion, '--nologo', '-v', 'm')
    $reloj.Stop()

    $advertencias = 0
    $m = [regex]::Match($build.Salida, '(?im)^\s*(\d+)\s+Advertencia')
    if ($m.Success) { $advertencias = [int]$m.Groups[1].Value }

    if ($build.Codigo -ne 0) {
        Mal ("La compilación falló ({0:N1} s)" -f $reloj.Elapsed.TotalSeconds)
        Linea
        Linea ($build.Salida -split "`n" | Select-Object -Last 25 | Out-String)
        $fallos++
    }
    elseif ($advertencias -gt 0) {
        Aviso ("Compila con {0} advertencia(s) ({1:N1} s)" -f $advertencias, $reloj.Elapsed.TotalSeconds)
        $fallos++
    }
    else {
        Bien ("Compila sin errores ni advertencias ({0:N1} s)" -f $reloj.Elapsed.TotalSeconds)
    }
}

# ------------------------------------------------- 2. JavaScript del portal
Titulo '[2/4] Sintaxis de JavaScript'

$node = Get-Command node -ErrorAction SilentlyContinue
$js = @(Get-ChildItem (Join-Path $web 'wwwroot\js') -Filter '*.js' -ErrorAction SilentlyContinue)

if (-not $node) {
    Aviso 'Node no está instalado: se omite la revisión de JavaScript.'
}
elseif ($js.Count -eq 0) {
    Aviso 'No se encontraron archivos .js en wwwroot/js.'
}
else {
    $rotos = @()
    foreach ($archivo in $js) {
        & node --check $archivo.FullName 2>$null
        if ($LASTEXITCODE -ne 0) { $rotos += $archivo.Name }
    }

    if ($rotos.Count -eq 0) {
        Bien ("{0} archivo(s) sin errores de sintaxis: {1}" -f $js.Count, (($js | ForEach-Object { $_.Name }) -join ', '))
    }
    else {
        Mal ("Con errores de sintaxis: {0}" -f ($rotos -join ', '))
        $fallos++
    }
}

# ------------------------------------------------- 3. pruebas xUnit
Titulo '[3/4] Pruebas xUnit'

if (Test-Path $evidencia) { Remove-Item $evidencia -Recurse -Force }
New-Item -ItemType Directory -Path $evidencia -Force | Out-Null

$reloj.Restart()
$pruebas = Ejecutar @('test', $solucion, '-c', $Configuracion, '--no-build', '--nologo',
                      '--logger', 'trx;LogFileName=resultados.trx', '--results-directory', $evidencia)
$reloj.Stop()

$trx = Join-Path $evidencia 'resultados.trx'
$porClase = @{}
$resumen = [pscustomobject]@{ Total = 0; Ok = 0; Mal = 0; Omitidas = 0 }
$detalleFallos = @()

if (Test-Path $trx) {
    $xml = [xml](Get-Content $trx -Raw -Encoding UTF8)
    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')

    $mapa = @{}
    foreach ($u in $xml.SelectNodes('//t:UnitTest', $ns)) {
        $clase = $u.SelectSingleNode('t:TestMethod', $ns).className
        if ($clase) { $mapa[$u.id] = ($clase -split '\.')[-1] }
    }

    foreach ($r in $xml.SelectNodes('//t:UnitTestResult', $ns)) {
        $clase = $mapa[$r.testId]
        if (-not $clase) { $clase = 'Desconocida' }

        if (-not $porClase.ContainsKey($clase)) {
            $porClase[$clase] = [pscustomobject]@{ Total = 0; Ok = 0; Mal = 0; Omitidas = 0 }
        }

        $fila = $porClase[$clase]
        $fila.Total++
        $resumen.Total++

        switch ($r.outcome) {
            'Passed'  { $fila.Ok++; $resumen.Ok++ }
            'NotExecuted' { $fila.Omitidas++; $resumen.Omitidas++ }
            default   {
                $fila.Mal++
                $resumen.Mal++
                $detalleFallos += ("{0} · {1}" -f $clase, $r.testName)
            }
        }
    }
}

if ($resumen.Total -eq 0) {
    Mal ("No se obtuvieron resultados de pruebas ({0:N1} s)" -f $reloj.Elapsed.TotalSeconds)
    Linea ($pruebas.Salida -split "`n" | Select-Object -Last 25 | Out-String)
    $fallos++
}
elseif ($resumen.Mal -gt 0 -or $resumen.Omitidas -gt 0) {
    Aviso ("{0} correctas · {1} fallidas · {2} omitidas ({3:N1} s)" -f $resumen.Ok, $resumen.Mal, $resumen.Omitidas, $reloj.Elapsed.TotalSeconds)
    foreach ($d in $detalleFallos) { Mal $d }
    $fallos++
}
else {
    Bien ("{0} correctas · 0 fallidas · 0 omitidas ({1:N1} s)" -f $resumen.Ok, $reloj.Elapsed.TotalSeconds)
}

Linea ("       Evidencia: artifacts\pruebas\resultados.trx")

# ------------------------------------------------- 4. plan manual
Titulo '[4/4] Plan de pruebas manual'

$plan = Join-Path $raiz 'docs\plan-pruebas-portal-mpaz.xlsx'
$hojas = @()
$python = Get-Command python -ErrorAction SilentlyContinue

if (-not (Test-Path $plan)) {
    Aviso 'No está docs\plan-pruebas-portal-mpaz.xlsx: se omite el plan manual.'
}
elseif (-not $python) {
    Aviso 'Python no está instalado: no se puede leer el Excel del plan.'
}
else {
    $codigo = @'
import json, sys
import openpyxl
wb = openpyxl.load_workbook(sys.argv[1])
salida = []
for ws in wb.worksheets[1:]:
    ids = [ws.cell(r, 1).value for r in range(5, ws.max_row + 1)]
    res = [ws.cell(r, 9).value for r in range(5, ws.max_row + 1) if ws.cell(r, 1).value]
    total = len([i for i in ids if i])
    salida.append({
        "hoja": ws.title,
        "total": total,
        "P": sum(1 for x in res if x == "P"),
        "B": sum(1 for x in res if x == "B"),
        "F": sum(1 for x in res if x == "F"),
        "pendiente": sum(1 for x in res if not x),
    })
print(json.dumps(salida, ensure_ascii=False))
'@

    try {
        $json = ($codigo | & python - $plan 2>$null) -join ''
        if ($json) { $hojas = $json | ConvertFrom-Json }
    }
    catch { $hojas = @() }

    if (-not $hojas -or $hojas.Count -eq 0) {
        Aviso 'No se pudo leer el Excel (¿falta openpyxl?). Se omite el plan manual.'
    }
    else {
        $totalP = ($hojas | ForEach-Object { $_.P } | Measure-Object -Sum).Sum
        $totalB = ($hojas | ForEach-Object { $_.B } | Measure-Object -Sum).Sum
        $totalF = ($hojas | ForEach-Object { $_.F } | Measure-Object -Sum).Sum
        $totalCasos = ($hojas | ForEach-Object { $_.total } | Measure-Object -Sum).Sum
        $hechos = $totalP + $totalB + $totalF
        $texto = ("{0} de {1} casos ejecutados · {2} P · {3} B · {4} F" -f $hechos, $totalCasos, $totalP, $totalB, $totalF)

        if ($totalF -gt 0) { Mal $texto } else { Bien $texto }
    }
}

# ------------------------------------------------- checklist por módulo
Titulo 'CHECKLIST POR MÓDULO'

$modulosConPruebas = 0
foreach ($nombre in $modulos.Keys) {
    $clases = $modulos[$nombre]
    $total = 0; $ok = 0; $mal = 0

    foreach ($clase in $clases) {
        if ($porClase.ContainsKey($clase)) {
            $total += $porClase[$clase].Total
            $ok += $porClase[$clase].Ok
            $mal += $porClase[$clase].Mal
        }
    }

    if ($total -eq 0) {
        Aviso ("{0} ....... sin resultados" -f $nombre)
        continue
    }

    $modulosConPruebas++
    $linea = "  {0} {1} {2}/{3}" -f $(if ($mal -eq 0) { '[OK]' } else { '[XX]' }), $nombre.PadRight(36, '.'), $ok, $total
    if ($mal -eq 0) { Write-Host $linea -ForegroundColor Green } else { Write-Host $linea -ForegroundColor Red }

    foreach ($clase in $clases) {
        if (-not $porClase.ContainsKey($clase)) { continue }
        $fila = $porClase[$clase]
        $estado = if ($fila.Mal -eq 0) { '[OK]' } else { '[XX]' }
        Write-Host ("      {0} {1} {2}" -f $estado, $clase.PadRight(34, '.'), $fila.Total) -ForegroundColor DarkGray
    }
}

Linea
foreach ($nombre in $sinAutomaticas.Keys) {
    Write-Host ("  [--] {0} {1}" -f $nombre.PadRight(36, '.'), $sinAutomaticas[$nombre]) -ForegroundColor Yellow
}

# Clases que no quedaron mapeadas a ningún módulo (si mañana se agregan).
$mapeadas = @()
foreach ($clases in $modulos.Values) { $mapeadas += $clases }
$sueltas = $porClase.Keys | Where-Object { $mapeadas -notcontains $_ }
if ($sueltas) {
    Linea
    foreach ($suelta in $sueltas) {
        Aviso ("{0} {1} pruebas · sin módulo asignado en el script" -f $suelta.PadRight(34, '.'), $porClase[$suelta].Total)
    }
}

# ------------------------------------------------- plan manual por hoja
if ($hojas -and $hojas.Count -gt 0) {
    Titulo 'PLAN MANUAL POR HOJA'
    foreach ($h in $hojas) {
        $hechos = $h.P + $h.B + $h.F
        $texto = "  {0,-24} {1,3}/{2,-3} ejecutados · {3} P · {4} B · {5} pendientes" -f $h.hoja, $hechos, $h.total, $h.P, $h.B, $h.pendiente
        if ($h.F -gt 0) { Write-Host $texto -ForegroundColor Red }
        elseif ($h.pendiente -eq 0) { Write-Host $texto -ForegroundColor Green }
        else { Write-Host $texto -ForegroundColor DarkGray }
    }
}

# ------------------------------------------------- resumen
Titulo 'RESUMEN'

if ($resumen.Total -gt 0) {
    $porcentaje = [math]::Round(100 * $resumen.Ok / $resumen.Total)
    Write-Host ("  Pruebas automáticas ....... {0}/{1} ({2} %)" -f $resumen.Ok, $resumen.Total, $porcentaje)
    Write-Host ("  Módulos con pruebas ....... {0} de {1}" -f $modulosConPruebas, ($modulos.Count + $sinAutomaticas.Count))
}
if ($hojas -and $hojas.Count -gt 0) {
    $hechos = $totalP + $totalB + $totalF
    Write-Host ("  Plan manual ............... {0}/{1} casos" -f $hechos, $totalCasos)
}

Linea
if ($fallos -eq 0) {
    Write-Host '  RESULTADO: compilación y pruebas automatizadas en verde.' -ForegroundColor Green
    Write-Host ''
    exit 0
}
else {
    Write-Host ("  RESULTADO: {0} bloque(s) con problemas." -f $fallos) -ForegroundColor Red
    Write-Host ''
    exit 1
}
