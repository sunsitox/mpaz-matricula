# Roles y permisos actuales del portal · Matrícula MPAZ

**19-11-2026 (actualizado).** Describe lo que el sistema permite **hoy** con la
matriz cargada en la base de desarrollo (`mpaz_matricula`, Neon) y escrita en
el repositorio (`docker/db-init/04-datos-gestion-expediente.sql` y
`database/modulos/matricula/pasos/24-retiro-utp-convivencia.sql`).

## 1. Cómo funciona el permiso

- Cada permiso es un par **área:acción** guardado en `rol_permiso` y entregado
  al iniciar sesión como una credencial `permiso` (`GENERAL:LEER`, …).
- Las pantallas exigen el permiso en el servidor (`[Authorize(Policy="Permiso:AREA:ACCION")]`);
  los bloques sensibles de la ficha y el PDF se recortan además por área, así que
  un permiso de entrada no da acceso al expediente completo.
- Si a un funcionario se le **quitan** permisos, su sesión se invalida en la
  siguiente petición; si se le agregan, debe volver a entrar.
- Cada lectura y cada cambio queda en auditoría (`registros_acceso`,
  `historial_cambios`).

## 2. Qué abre cada permiso

| Pantalla o acción | Permiso que exige |
| --- | --- |
| Listado de alumnos | `GENERAL:LEER` |
| Ficha del alumno (consulta) | `GENERAL:LEER` + el área de cada bloque |
| Bloques de la ficha y copia PDF | `FAMILIA:LEER`, `RETIRO:LEER`, `SALUD:LEER`, `PIE:LEER`, `JUDICIAL:LEER`, `AUTORIZACIONES:LEER`, `DOCUMENTOS:LEER`, `MEDICACION:LEER`, `SITUACION_FAMILIAR:LEER` |
| Asistente de la ficha (ver y guardar pasos) | `GENERAL:LEER`; guardar exige `GENERAL:CREAR` o `GENERAL:EDITAR` |
| Iniciar matrícula, confirmar, registrar firma, anular | `GENERAL:CREAR` |
| Validar matrícula (revisión de Dirección) | `GENERAL:VALIDAR` |
| Validar documentación del expediente | `DOCUMENTOS:VALIDAR` |
| Subir documentos | `DOCUMENTOS:CREAR` |
| Marcar un requisito documental | `DOCUMENTOS:EDITAR` |
| Descargar un documento | `DOCUMENTOS:LEER` |
| Revocar una autorización | `AUTORIZACIONES:EDITAR` |
| **Registrar una nota de retiro** (ficha del alumno) | `RETIRO:EDITAR` o `RETIRO:CREAR` |
| **Ver notas de retiro** | `RETIRO:LEER` |
| **Ver/registrar observaciones de situación familiar** | `SITUACION_FAMILIAR:LEER` / `CREAR` / `EDITAR` |
| **Fichas médicas del personal** (`/personal/fichas-medicas`) | roles `UTP` o `DIRECCION` (por nombre de rol) |
| Registrar una dosis | `MEDICACION:CREAR` (además, `Medicacion:Habilitada=true`) |
| Certificado: generar | `DOCUMENTOS:CREAR` o `DOCUMENTOS:EDITAR` (Secretaría) |
| Certificado: ver historial | `DOCUMENTOS:LEER` |
| Certificado: firma impresa | no depende del permiso: firma el funcionario con rol `DIRECCION` vigente, aunque lo genere Secretaría |
| Validación pública de certificado por folio | sin sesión (página pública) |
| Reportes en pantalla | `GENERAL:LEER` |
| Reportes en CSV | `GENERAL:EXPORTAR` o `DOCUMENTOS:EXPORTAR` |
| Reporte de faltantes documentales | `DOCUMENTOS:LEER` |
| Alertas de vencimiento | `AUTORIZACIONES:LEER` y/o `MEDICACION:LEER` |
| Cuentas de funcionarios (`/admin/funcionarios`) | `ADMIN:ADMINISTRAR` |
| Actividad y auditoría (`/Home/Actividad`) | `AUDITORIA:LEER` |
| Año escolar (`/Configuracion`, activar año) | roles `SECRETARIA` o `DIRECCION` (por nombre de rol, no por permiso) |
| Inicio (`/`) | solo estar autenticado; cada indicador aparece si la sesión tiene el área |

## 3. Matriz vigente

Leyenda: **L** = leer · **C** = crear · **E** = editar · **V** = validar ·
**X** = exportar · **A** = administrar · **—** = sin acceso.

| Área | SECRETARÍA | DIRECCIÓN | INSPECTORÍA | PIE | UTP | CONVIVENCIA | TENS | DOCENTE | ADMIN_TI |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| General | L C E X | L V X | L E | L | L X | L | L | L | — |
| Familia | L C E | L | L E | L | L | L | L | — | — |
| **Retiro** | **L** | **L** | **L** | — | **L C E** | **L C E** | — | — | — |
| Salud | L C E | L | L | — | L | — | L | — | — |
| PIE | — | L | L | L | L | — | — | — | — |
| Judicial | L C E | L | L | — | L | — | — | — | — |
| Autorizaciones | L C E | L V | L | — | L | L | — | — | — |
| **Situación familiar** | — | **L** | — | — | **L C E** | **L C E** | — | — | — |
| Documentos | L C E X | L V X | L | L | L X | L | L | — | — |
| Medicación | — | L | L | — | L | — | L C | — | — |
| Administración | — | — | — | — | — | — | — | — | L A |
| Auditoría | — | — | — | — | — | — | — | — | L |
| **Fichas médicas del personal** | — | **Sí** | — | — | **Sí** | — | — | — | — |

Notas: el área `RETIRO` cubre notas de retiro (orden de alejamiento, retiro
autorizado/no autorizado) y retiro autorizado. La columna *Fichas médicas del
personal* es por nombre de rol (UTP y Dirección), no por permiso de área.

## 4. Qué hace cada rol, en la práctica

| Rol | Puede | No puede |
| --- | --- | --- |
| **Secretaría / matrícula** | Crear alumnos y matrículas, capturar y editar los pasos de la ficha, subir documentos, revocar autorizaciones, confirmar y anular, exportar matrículas y CSV, ver salud, antecedentes judiciales y **notas de retiro (solo lectura)** | Escribir notas de retiro, validar, validar documentación, ver PIE, situación familiar o medicación, administrar cuentas, ver fichas médicas del personal |
| **Dirección** | Ver todo el expediente (salud, PIE, judicial, medicación, situación familiar), validar matrícula/autorizaciones/documentación, exportar, ver fichas médicas del personal y **notas de retiro (solo lectura)** | Crear o editar matrículas y notas de retiro; registrar firma; revocar autorizaciones |
| **Inspectoría** | Ver el expediente y editar datos del estudiante (General, Familia), ver notas de retiro (solo lectura) | Crear/confirmar matrículas, escribir notas de retiro, validar, exportar, revocar autorizaciones |
| **Equipo PIE** | Ver datos generales, familia, PIE y documentos | Ver salud, judicial, autorizaciones, retiro, situación familiar o medicación; cualquier escritura |
| **UTP** | Ver todo el expediente y exportar, **registrar y editar notas de retiro**, **ver/crear/editar observaciones de situación familiar**, ver fichas médicas del personal | Crear/eliminar matrículas, validar institucionalmente, revocar autorizaciones |
| **Convivencia Escolar** | Ver datos generales, familia, retiro, autorizaciones, documentos y situación familiar; **registrar y editar notas de retiro y observaciones de situación familiar** | Ver salud, PIE, judicial o medicación; crear matrículas o revocar autorizaciones |
| **TENS** | Ver datos generales, familia, salud, medicación y documentos; registrar dosis **si** la medicación está habilitada | Ver PIE, judicial, autorizaciones o retiro; editar, validar o exportar; crear/confirmar matrículas |
| **Docente** | Ver solo el listado y los datos mínimos del alumno (identificación, curso, estado) | Ver familia, salud, retiro, PIE, judicial, autorizaciones, documentos, situación familiar o medicación |
| **Administración TI** | Gestionar cuentas de funcionarios, roles y consultar auditoría | Entrar al listado o a la ficha de alumnos; ver datos clínicos |

## 5. Observaciones sobre la matriz actual

1. **Notas de retiro**: escriben **UTP y Convivencia**; **Secretaría,
   Inspectoría y Dirección** solo leen (pasos 17 y 24). `VALIDAR` no aplica al
   área.
2. **Situación familiar** es un área propia (`SITUACION_FAMILIAR`), distinta de
   Salud y Judicial: UTP y Convivencia leen/crean/editan, Dirección lee.
3. **Fichas médicas del personal** viven en el apartado *Personal*, fuera de
   UTP, con acceso por rol a **UTP y Dirección**.
4. **La medicación está apagada por configuración** (`Medicacion:Habilitada=false`),
   así que ni siquiera TENS ve o registra dosis hasta aprobar el protocolo.
5. **Configuración → Año escolar usa nombres de rol**, no permisos.
6. **Administración TI no puede ver alumnos**, por diseño.
7. En la base del **colegio** la matriz aún no está cargada: queda pendiente
   según `docs/decisiones-pendientes-colegio.md`.
