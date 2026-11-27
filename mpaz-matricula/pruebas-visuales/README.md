# Simulación visual de usuario · Portal de Matrícula MPAZ

Recorridos automatizados del portal con navegador real (Playwright). Hay dos:

| Script | Qué hace |
|---|---|
| `grabar-flujo.mjs` | Recorre **todos los módulos** y graba el video del flujo completo. |
| `simulacion-secretaria.mjs` | Corrida corta con capturas PNG por caso (verificación rápida). |

El recorrido grabado usa la cuenta con más funciones para la mayoría de los
casos y cambia de rol cuando el módulo lo exige:

| Rol | Cuenta | Qué cubre |
|---|---|---|
| SECRETARIA | `prueba.secretaria@mpaz.cl` | acceso, panel, estudiantes, ficha, documentos, matrícula nueva, reportes, certificados, configuración |
| DIRECCION | `prueba.direccion@mpaz.cl` | validación institucional y documental, fichas médicas del personal |
| ADMIN_TI | `admin.palma@mpaz.cl` | administración de funcionarios |
| TENS | `prueba.tens@mpaz.cl` | panel de medicación y protocolo pendiente |
| DOCENTE | `prueba.docente@mpaz.cl` | vista limitada: demuestra la ausencia de paneles sensibles |

Las claves no viven en el repositorio: cada rol se toma de `CLAVE_SECRETARIA`,
`CLAVE_DIRECCION`, `CLAVE_ADMIN`, `CLAVE_TENS` y `CLAVE_DOCENTE` (con
`CLAVE` como respaldo para Secretaría).

## Requisitos

1. PostgreSQL con la base del portal y los datos mock.
2. Node 18+ y Playwright con Chromium:
   ```bash
   cd mpaz-matricula/pruebas-visuales
   npm install
   npx playwright install chromium
   ```
3. El portal puede estar ya corriendo en `http://localhost:5199`, o el script lo
   levanta con `--levantar-servidor` (publica en una carpeta temporal: nunca
   toca el portal abierto en Visual Studio).

## Uso

```bash
npm run grabar              # navegador visible, espera ENTER para comenzar
npm run grabar:headless     # sin ventana (verificación)
node grabar-flujo.mjs --levantar-servidor
```

Variables opcionales: `BASE_URL`, `USUARIO`/`USUARIO_DIRECCION`/`USUARIO_ADMIN`/
`USUARIO_TENS`/`USUARIO_DOCENTE` (correos), `CLAVE_SECRETARIA`, `CLAVE_DIRECCION`,
`CLAVE_ADMIN`, `CLAVE_TENS`, `CLAVE_DOCENTE`, `SLOWMO`, `MIN_VIDEO_MS` (duración
mínima, por defecto 100 s), `SOLO` (prefijos de casos separados por coma, para
ensayar uno solo) y `CERT_CODIGO` (código de un certificado para validar en la
página pública).

Resultado en `evidencias/grabar-flujo/`:

- `flujo-mpaz.webm` — el video del recorrido.
- `resultados` por caso en la consola y checklist por módulo al final.
- `certificado-alumno-regular.pdf`, `reporte-matriculas.csv`, capturas de los
  archivos que se suben y `servidor.log`.

## Módulos del recorrido

El recorrido está partido en un archivo por módulo dentro de `flujos/`; cada
uno se puede leer y ajustar sin tocar los demás. `grabar-flujo.mjs` solo
orquesta servidor, navegador, video y resumen.

| Módulo | Archivo | Qué muestra |
|---|---|---|
| 01 Acceso y sesión | `flujos/01-acceso.mjs` | login, panel con KPIs, actividad auditada y denegación a Funcionarios |
| 02 Ficha de matrícula | `flujos/02-ficha.mjs` | asistente completo de 8 pasos con revisión y confirmación |
| 03 Estudiantes y expediente | `flujos/03-estudiantes.mjs` | búsqueda y filtros, ficha por pestañas, revocación, documentos, firma, confirmación, corrección, validación y anulación |
| 04 Medicación (TENS) | `flujos/04-medicacion.mjs` | panel de medicación, protocolo pendiente (MAT-05) y vista limitada del docente |
| 05 Alertas y reportes | `flujos/05-alertas-reportes.mjs` | alertas de vencimiento, reporte de matrículas y faltantes con CSV |
| 06 Fichas médicas del personal | `flujos/06-fichas-medicas.mjs` | listado, ficha, PDF y denegación a Secretaría |
| 07 Funcionarios y roles | `flujos/07-funcionarios.mjs` | alta, clave temporal, desactivar/reactivar y cierre de rol |
| 08 Certificados | `flujos/08-certificados.mjs` | emisión con folio y QR, historial y verificación pública |
| 09 Configuración | `flujos/09-configuracion.mjs` | año escolar activo y formulario de alta |
| 10 Cierre | `flujos/10-cierre.mjs` | cierre de sesión, página pública y plano final |

`flujos/comun.mjs` concentra configuración, sesiones, utilidades de navegador y
el registro de casos (`caso(nombre, fn, { critico })`). Un caso no crítico que
no aplique queda como **OMITIDO** y la corrida sigue; uno crítico detiene el
módulo.

## Casos de la corrida corta

`npm run simular` recorre con capturas: login con cambio de clave, listado y
búsqueda, ficha por pestañas, reportes y CSV, denegación de funcionarios,
wizard paso 1, comprobante y salida. Cada caso guarda su PNG en `evidencias/`.

> Ambas corridas dejan datos de prueba en la base de desarrollo,
> identificables por `SIMULACIÓN` / `demo.video@mpaz.cl`. El módulo 09 no
> guarda cambios de configuración a propósito.
