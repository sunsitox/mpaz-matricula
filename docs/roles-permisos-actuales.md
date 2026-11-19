# Roles y permisos actuales del portal · Matrícula MPAZ

**19-11-2026.** Describe lo que el sistema permite **hoy** con la matriz escrita
en el repositorio (`database/modulos/matricula/pasos/03-permisos-iniciales.sql`
y los pasos 14 y 15). Esa matriz **no está cargada en la base del colegio**: en
una base sin `rol_permiso`, un funcionario puede entrar pero las pantallas con
permiso responden 403. En desarrollo se carga con el instalador
(`--con-permisos`); en el colegio queda a la espera de aprobación (ver
`docs/decisiones-pendientes-colegio.md`).

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
| Bloques de la ficha y copia PDF | `FAMILIA:LEER`, `RETIRO:LEER`, `SALUD:LEER`, `PIE:LEER`, `JUDICIAL:LEER`, `AUTORIZACIONES:LEER`, `DOCUMENTOS:LEER`, `MEDICACION:LEER` |
| Asistente de la ficha (ver y guardar pasos) | `GENERAL:LEER`; guardar exige `GENERAL:CREAR` o `GENERAL:EDITAR` |
| Iniciar matrícula, confirmar, registrar firma, anular | `GENERAL:CREAR` |
| Validar matrícula (revisión de Dirección) | `GENERAL:VALIDAR` |
| Validar documentación del expediente | `DOCUMENTOS:VALIDAR` |
| Subir documentos | `DOCUMENTOS:CREAR` |
| Marcar un requisito documental | `DOCUMENTOS:EDITAR` |
| Descargar un documento | `DOCUMENTOS:LEER` |
| Revocar una autorización | `AUTORIZACIONES:EDITAR` |
| Registrar una dosis | `MEDICACION:CREAR` (además, `Medicacion:Habilitada=true`) |
| Certificado: emitir y ver historial | `GENERAL:LEER` + crear, editar o validar |
| Validación pública de certificado por folio | sin sesión (página pública) |
| Reportes en pantalla | `GENERAL:LEER` |
| Reportes en CSV | `GENERAL:EXPORTAR` o `DOCUMENTOS:EXPORTAR` |
| Reporte de faltantes documentales | `DOCUMENTOS:LEER` |
| Alertas de vencimiento | `AUTORIZACIONES:LEER` y/o `MEDICACION:LEER` |
| Cuentas de funcionarios (`/admin/funcionarios`) | `ADMIN:ADMINISTRAR` |
| Actividad y auditoría (`/Home/Actividad`) | `AUDITORIA:LEER` |
| Año escolar (`/Configuracion`, activar año) | roles `SECRETARIA` o `DIRECCION` (por nombre de rol, no por permiso) |
| Inicio (`/`) | solo estar autenticado; cada indicador aparece si la sesión tiene el área |

## 3. Matriz vigente en el repositorio

Leyenda: **L** = leer · **C** = crear · **E** = editar · **V** = validar ·
**X** = exportar · **A** = administrar · **—** = sin acceso.

| Área | SECRETARÍA | DIRECCIÓN | INSPECTORÍA | PIE | UTP | CONVIVENCIA | TENS | DOCENTE | ADMIN_TI |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| General | L C E X | L V X | L E | L | L X | L | L | L | — |
| Familia | L C E | L | L E | L | L | L | L | — | — |
| Retiro | L C E | L | L E | — | L | L | — | — | — |
| Salud | L C E | L | L | — | L | — | L | — | — |
| PIE | — | L | L | L | L | — | — | — | — |
| Judicial | L C E | L | L | — | L | — | — | — | — |
| Autorizaciones | L C E | L V | L | — | L | L | — | — | — |
| Documentos | L C E X | L V X | L | L | L X | L | L | — | — |
| Medicación | — | L | L | — | L | — | L C | — | — |
| Administración | — | — | — | — | — | — | — | — | L A |
| Auditoría | — | — | — | — | — | — | — | — | L |

## 4. Qué hace cada rol, en la práctica

| Rol | Puede | No puede |
| --- | --- | --- |
| **Secretaría / matrícula** | Crear alumnos y matrículas, capturar y editar los pasos de la ficha, subir documentos, revocar autorizaciones, confirmar y anular, exportar matrículas y CSV, ver salud y antecedentes judiciales | Validar institucionalmente (`GENERAL:VALIDAR`), validar documentación, ver PIE y medicación, administrar cuentas |
| **Dirección** | Ver todo el expediente (incluida salud, PIE, judicial y medicación), validar matrícula, autorizaciones y documentación, exportar | Crear o editar matrículas y datos; registrar firma; revocar autorizaciones |
| **Inspectoría** | Ver todo el expediente y editar datos del estudiante (General, Familia, Retiro) | Crear o confirmar matrículas, validar, exportar, revocar autorizaciones |
| **Equipo PIE** | Ver datos generales, familia, PIE y documentos | Ver salud, judicial, autorizaciones o medicación; cualquier escritura |
| **UTP** | Ver todo el expediente y exportar | Cualquier escritura: crear, editar, validar o revocar |
| **Convivencia Escolar** | Ver datos generales, familia, retiro, autorizaciones y documentos | Ver salud, PIE, judicial o medicación; cualquier escritura |
| **TENS** | Ver datos generales, familia, salud, medicación y documentos; registrar dosis **si** la medicación está habilitada | Ver PIE, judicial o autorizaciones; editar, validar o exportar; crear/confirmar matrículas |
| **Docente** | Ver solo el listado y los datos mínimos del alumno (identificación, curso, estado) | Ver familia, salud, retiro, PIE, judicial, autorizaciones, documentos o medicación |
| **Administración TI** | Gestionar cuentas de funcionarios, roles y consultar auditoría | Entrar al listado o a la ficha de alumnos; ver datos clínicos |

## 5. Observaciones sobre la matriz actual

1. **Sin la matriz cargada no hay operación.** El DDL deja `rol_permiso` vacío a
   propósito y solo la carga el instalador con `--con-permisos`; en la base del
   colegio todavía no se ejecutó.
2. **Secretaría no ve PIE ni medicación**, aunque es el perfil que captura la
   ficha. Si debe completar esos bloques, hay que agregarle `PIE:LEER` (y
   decidir si también medicación). Es la inconsistencia más visible de la matriz.
3. **`GENERAL:EDITAR` habilita editar la matrícula**, pero no crearla, confirmarla
   ni anularla: eso sigue exigiendo `GENERAL:CREAR` (hoy, solo Secretaría).
4. **Dirección valida, no edita.** Si «validar» debe permitir corregir datos, hoy
   no lo permite.
5. **La medicación está apagada por configuración** (`Medicacion:Habilitada=false`),
   así que ni siquiera TENS ve o registra dosis hasta aprobar el protocolo, aunque
   la matriz le dé el permiso.
6. **Configuración → Año escolar usa nombres de rol**, no permisos: si el colegio
   cambia quién administra el año escolar, hay que tocar código (queda como tema
   menor en el documento de decisiones).
7. **Administración TI no puede ver alumnos**: no tiene `GENERAL:LEER`, por
   diseño, para que gestionar cuentas no dé acceso al expediente.
