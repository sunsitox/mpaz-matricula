# Auditoría funcional · Portal de Matrícula MPAZ

**Fecha:** 18-11-2026
**Alcance:** todos los módulos del repositorio `mpaz-matricula/` (matrícula, auth/SSO,
asistencia, web service, base de datos, despliegue y Docker).
**Método:** lectura completa de controladores, servicios, seguridad, persistencia,
vistas, DDL, privilegios y scripts de despliegue; ejecución de la suite de pruebas;
arranque real de la aplicación en modo `Production`.
**No ejecutado:** construcción de la imagen Docker (Docker no está instalado),
pruebas contra PostgreSQL del servidor ni recorrido visual en navegador.

---

## 1. Resumen ejecutivo

El sistema tiene una base de seguridad y de integridad bien construida: antiforgery
global, CSP, limitador de intentos, bloqueo por cuenta, revalidación de permisos por
petición, auditoría de lecturas y denegaciones, ficha cerrada inmutable con snapshot y
folio, y 168 pruebas automatizadas en verde.

Sin embargo, **el paquete actual no se puede publicar**. Verifiqué ejecutando el
binario en `Production`: el proceso muere al arrancar por la configuración del emisor
SSO. Además, el flujo de validación documental de Dirección (`VALIDADA`) es
inalcanzable porque nunca se escribe `ficha_version.fecha_firma`.

| Severidad | Cantidad | Efecto |
| --- | --- | --- |
| P0 · Bloqueante | 2 | El software no arranca en producción; un acto institucional es imposible |
| P1 · Alta | 4 | Pérdida de documentos subidos, entrega Docker inoperante |
| P2 · Media | 9 | Fugas menores, historial clínico, coherencia de datos |
| P3 · Baja | 8 | Robustez, mantenimiento, experiencia |

---

## 2. P0 · Bloqueantes

### P0-1 · La aplicación no arranca en `Production` (verificado)

`EmisorSso.AddEmisorSso` lanza una excepción cuando el entorno no es `Development`,
y `Program.cs` lo invoca sin condicionar.

```
Unhandled exception. System.InvalidOperationException:
El emisor SSO aún no está configurado para producción: falta el certificado de firma.
   at EmisorSso.<AddEmisorSso>b__1(...) EmisorSso.cs:line 48
   at Program.<Main>$(...) Program.cs:line 105
```

- **Evidencia:** `MarcelaPaz.Web/Seguridad/EmisorSso.cs:44-49`, `Program.cs:105`.
  Reproducido con `ASPNETCORE_ENVIRONMENT=Production` y variables mínimas: el proceso
  termina antes de escuchar en el puerto.
- **Impacto:** ni el servidor del colegio ni `docker compose` pueden levantar el
  release actual. El release desplegado el 06-11 no incluye SSO; el paquete del 17-11
  sí, y es el que falla.
- **Recomendación:** en `Production`, no lanzar; registrar el aviso y **no** registrar
  el servidor OpenIddict (o condicionar `AddEmisorSso` a una bandera
  `Sso:Habilitado`). Cargar el certificado de firma desde configuración cuando exista.

### P0-2 · La validación documental `VALIDADA` es inalcanzable

`ficha.FechaFirma` se **lee** para autorizar `VALIDADA`, pero ninguna ruta del
aplicativo la **escribe**:

- `RegistroFicha.cs:1377` → `if (ficha.FechaFirma is null) return "No se puede validar..."`.
- `grep FechaFirma`: sólo asignaciones sobre `Autorizacion`, ninguna sobre `FichaVersion`.
- El DDL lo exige: `ck_ficha_validada` requiere `escaneo_documento_id`,
  `fecha_firma`, `validado_por` y `validado_en` (`01-ddl-institucional.sql:469-472`).
- `RegistrarFirmaAsync` cambia sólo el estado de la matrícula y comenta que la ficha es
  inmutable, pero `fn_ficha_proteger` **sí permite** actualizar `fecha_firma` mientras
  la ficha no esté `VALIDADA` (`01-ddl-institucional.sql:489-509`).

- **Impacto:** Dirección/Inspectoría nunca puede dejar el expediente en `VALIDADA`.
  El acto MAT-10 queda inutilizable y el botón devuelve siempre el mismo error.
- **Recomendación:** en `RegistrarFirmaAsync`, fijar `ficha.FechaFirma`,
  `firmante_persona_id` y `escaneo_subido_por/en`; agregar prueba del flujo
  firma → escaneo → validación.

---

## 3. P1 · Alta

### P1-1 · Los documentos subidos viven dentro del release y se pierden al desplegar

`ExpedienteDocumentos` guarda en `_entorno.ContentRootPath/App_Data/documentos`
(`ExpedienteDocumentos.cs:117-123`). En el servidor el `WorkingDirectory` es
`/opt/mpaz-matriculas/current` (`deploy/mpaz-matriculas.service:8`), un enlace a
`releases/<timestamp>`. Cada release nuevo cambia la raíz: los archivos quedan en el
release anterior y `RutaFisica` devuelve `null`.

Además `scripts/publicar-paquete.sh` y `build-release.sh` **borran `App_Data`** del
paquete.

- **Impacto:** el expediente documental se pierde en cada actualización; los enlaces
  responden «El archivo no está disponible en el servidor».
- **Recomendación:** ruta configurable (`Documentos:RutaBase`) apuntando a
  `/srv/mpaz-data/matriculas/documentos`, fuera de `current`, con respaldo propio.

### P1-2 · En Docker la aplicación no puede escribir los archivos

`Dockerfile` termina con `USER $APP_UID` sobre `/app`, cuyo contenido se copia como
`root`; `App_Data` no existe ni es escribible. La subida intenta
`Directory.CreateDirectory` y falla con `UnauthorizedAccessException`, que **no** está
en los filtros del controlador (`AlumnosController.cs:1110` captura sólo
`DbUpdateException` e `IOException`) → error 500.

- **Estado:** no verificado empíricamente (la imagen nunca se construyó), pero es
  consecuencia directa de los permisos del Dockerfile.
- **Recomendación:** `RUN mkdir -p /app/App_Data && chown -R $APP_UID /app/App_Data`,
  montar volumen y añadir `UnauthorizedAccessException` al manejo de errores.

### P1-3 · `docker/.env.example` contradice las validaciones de producción

Con el ejemplo tal cual:

- `ALLOWED_HOSTS=*` → `Program.cs:44-49` lanza «Producción requiere AllowedHosts…».
- `CLAVE_INICIAL=Mpaz2027segura` → `Program.cs:196-215` la reconoce como credencial
  por defecto conocida y **detiene el arranque**.

- **Impacto:** `docker compose up` nunca levanta la aplicación con el archivo entregado.
- **Recomendación:** dejar `ALLOWED_HOSTS` vacío con instrucción explícita y una clave
  inicial aleatoria; actualizar `README-docker.md`.

### P1-4 · El rol `mpaz_app` de Docker tiene la clave fija en el SQL

`docker/db-init/00-rol-app.sql` crea el rol con `PASSWORD 'mpaz_app_2027'`. Los scripts
de `docker-entrypoint-initdb.d` corren **sólo con el volumen vacío**: si se cambia
`CLAVE_APP` en `.env`, la aplicación deja de conectar y no hay mensaje que lo explique.

- **Recomendación:** crear el rol desde el arranque de la app o documentar que cambiar
  la clave exige `ALTER ROLE` manual y recrear el volumen.

---

## 4. P2 · Media

1. **Auditoría comparte el `DbContext` de la operación.** `AuditoriaAcceso` hace
   `_db.SaveChangesAsync()` sobre el mismo contexto escalado
   (`AuditoriaAcceso.cs:70`). Hoy los llamados ocurren después de cada escritura, pero
   cualquier cambio rastreado pendiente se confirma antes de tiempo, fuera de la
   transacción de `EscrituraAuditada`.
   *Recomendación:* contexto o `IDbContextFactory` propio para auditoría.

2. **`AplicarTodoAsync` se traga el `DbUpdateException` y deja el contexto sucio.**
   Devuelve un texto, pero `CerrarFichaAsync` sigue dentro de la misma transacción
   (`FichaController.cs:432-470`); el cierre puede guardar estado parcial o lanzar un
   500 sin mensaje.

3. **`GET /Alumnos/Detalle` escribe.** `MarcarVencidasAsync` corre bajo
   `_escritura.EjecutarAsync` en una petición de lectura
   (`AlumnosController.cs:463-482`). Cualquier rol con `AUTORIZACIONES:LEER` —incluido
   sólo-lectura— provoca escrituras con sólo abrir la ficha; un prefetch o un rastreador
   también.

4. **Historial clínico destruido.** `Paso6Async` hace `RemoveRange` de situaciones
   médicas y alergias previas y las reinserta (`RegistroFicha.cs:520-560`). No cierra
   vigencia: se pierde el historial que MAT-14 pide conservar. Es la razón por la que
   `04-privilegios-aplicacion.sql` concede `DELETE` sólo en esa tabla.

5. **`antecedente_salud.necesidades_atencion` se reutiliza para la salida al baño**
   (`RegistroFicha.cs:484-486`), sobrescribiendo el sentido del campo; la ficha del
   alumno lo muestra como «Necesidades de atención» (`Detalle.cshtml:739`).

6. **`/Home/Actividad` no exige `AUDITORIA:LEER`.** Sólo `[Authorize]`: cualquier
   funcionario autenticado ve los últimos 100 accesos con nombre del responsable, área y
   recurso (`HomeController.cs:62-80`, `PanelIndicadores.cs:155-186`).

7. **CSP de producción bloquea manejadores inline.** `script-src 'self'` sin
   `'unsafe-inline'` (`Program.cs:300-310`) desactiva `onchange="this.form.submit()"`
   en `Reportes/Matriculas.cshtml:21` y `Faltantes.cshtml:22`, y `onload=` en
   `Paso8.cshtml:96`. Falla sólo en producción.

8. **Medicación implementada pese a estar bloqueada por el protocolo.** Existen
   `Prescripcion`, `AdministracionMedicamento`, `RegistrarDosis` y la lectura en la
   ficha (`AlumnosController.cs:1370-1430`). El checklist dice que MAT-05 no debe
   conectarse hasta aprobar el protocolo TENS. Además `RegistrarDosis` acepta la fecha
   y hora del formulario sin acotarla.

9. **El SSO emite `sub` con el id de Identity, no con `persona.id`.** El contrato del
   módulo `auth` dice que el `sub` es `mpaz_auth.persona.id`
   (`database/modulos/auth/README.md`) y `AutorizacionController` usa
   `funcionario.SsoSujeto` (`AutorizacionController.cs:59`). Romperá el consumo desde
   Asistencia.

---

## 5. P3 · Baja

1. `RegistrarFirma` usa `funcionarioId = 0` si falta el claim, en vez de abortar como
   el resto (`AlumnosController.cs:920-930`).
2. `/Certificados` (historial) se abre con `GENERAL:LEER`: un Docente ve folio, alumno,
   curso y propósito de todos los certificados (`CertificadosController.cs:57-100`).
3. `alertas_revision` crece sin límite: una fila cada 15 minutos (~35.000 al año).
4. `ConfiguracionController` autoriza por rol fijo `SECRETARIA,DIRECCION`, no por
   permiso; si la matriz cambia, deja de funcionar en silencio.
5. Cachés estáticas de comunas y cursos en `RegistroFicha` (`_comunasLista`,
   `_cursosCache`) nunca se invalidan: alta de curso o comuna exige reiniciar.
6. `VigilanteSesion` compara el conjunto exacto de permisos: cualquier cambio en
   `rol_permiso` deja al usuario con 403 hasta que vuelve a entrar.
7. `AnularAsync` sobrescribe `matricula_anual.observaciones` con el motivo.
8. Indentación rota en `Program.cs:255-259` (llaves del bloque de siembra de cuentas):
   compila, pero induce a error al leer y mantener.

---

## 6. Estado por módulo

### Matrícula (núcleo) — funcional, con los P0/P1/P2 anteriores

Asistente de 8 pasos con borrador en `TempData`, validación por paso
(`ValidadorFicha`, recién completada para los pasos 2-5), cierre con snapshot y folio,
corrección con motivo, anulación, renovación con precarga, egreso de 8º, concurrencia
optimista por `version`, autorizaciones con revocación y vencimiento, expediente
documental, reportes CSV y alertas. Bien resuelto: la ficha cerrada es inmutable por
trigger y el PDF se arma desde `datos_snapshot`.

### Auth / SSO — prototipo, bloquea producción

Emisor OpenIddict con authorization-code y refresh, migración `AgregarSso` en
`mpaz_auth`, siembra de cliente de prueba sólo en desarrollo. Correcto como prototipo;
inutilizable fuera de `Development` (P0-1) y con el `sub` desalineado del contrato
(P2-9).

### Asistencia — sólo esquema

`10-ddl-asistencia.sql` (11 tablas, 9 enums) sin flujo de datos. Sin hallazgos de
código; pendiente de integración (MAT-15).

### Web service de registro — correcto con reservas

Timeouts de 8 s, respaldo en Neon y registro de resultado. Envía la contraseña en claro
al servicio externo (`RegistroWebService.cs:56-70`); el colegio debe confirmar que ese
endpoint es propio y confiable.

### Base de datos — sólida

38-41 tablas, triggers de auditoría, inmutabilidad de la ficha, bloqueo de borrado,
vista de alertas y funciones con `search_path` fijo. Los privilegios del rol están
correctamente limitados (sin `UPDATE/DELETE` sobre auditoría y medicación, con guarda
que aborta si se concedieran).

### Despliegue — preparado, no publicable

Servicio systemd endurecido, claves de Data Protection persistentes, `AllowedHosts`
obligatorio, guardas de credenciales conocidas. Todo eso queda tapado por P0-1.

### Docker — revisado, nunca ejecutado

Multi-stage correcto y compose con healthchecks, pero P1-1 a P1-4 lo dejan inoperante
tal como está.

---

## 7. Pruebas

- **168 pruebas en verde** (137 previas + 31 nuevas de validación de los pasos 2-5).
- Cobertura buena de: RUN/IPE/IPA, permisos, transiciones, anulación, autorizaciones,
  PDF/snapshot, reportes, conexión, niveles, web service, validación de bloques.
- **Huecos que habrían detectado esta auditoría:**
  - no hay prueba de `RegistrarFirmaAsync` → `ValidarDocumentacionAsync` (P0-2);
  - no hay prueba de arranque en `Production` (P0-1);
  - no hay pruebas de integración contra PostgreSQL para la vista de alertas, los
    triggers y `ck_ficha_validada`;
  - no hay prueba de persistencia de archivos fuera del release (P1-1).

---

## 8. Orden recomendado

1. P0-1: permitir el arranque en `Production` sin certificado SSO.
2. P0-2: escribir `fecha_firma` al registrar la firma y probar el flujo completo.
3. P1-1 y P1-2: ruta persistente de documentos y permisos en la imagen.
4. P1-3 y P1-4: corregir `.env.example` y el alta del rol en Docker.
5. P2-1 a P2-4: contexto de auditoría aislado, error de cierre, escritura fuera del GET
   y vigencia en lugar de borrado clínico.
6. P2-6 y P2-7: permisos de Actividad y CSP con los manejadores de las vistas.
7. P2-9 antes de que Asistencia consuma el SSO.

---

## 9. Correcciones aplicadas (18-11-2026)

Trabajo local, sin despliegue: no se publicó nada ni se tocó el servidor.

| Hallazgo | Corrección | Verificación |
| --- | --- | --- |
| P0-1 | `Sso:Habilitado` (por omisión sólo en Desarrollo). Sin emisor no se registra OpenIddict; en producción, habilitarlo sin certificado falla con un mensaje explícito. Se admite certificado PKCS#12 o PEM. La pantalla de autorización responde 404 si el emisor está apagado. | Arranque real en `Production`: escucha y `/Cuenta/Login` = 200, sin excepción de SSO. 2 pruebas nuevas. |
| P0-2 | `RegistrarFirmaAsync` escribe `fecha_firma` y `firmante_persona_id` en la ficha vigente. | Prueba firma → escaneo → `VALIDADA` en verde. |
| P1-1 | `Documentos:RutaBase` con valor por omisión `App_Data/documentos`; resolución con protección contra rutas fuera de la raíz y compatibilidad con el prefijo antiguo. | 3 pruebas nuevas; `Documentos__RutaBase` documentado en el `.env` de despliegue. |
| P1-2 | `Dockerfile` crea `/app/App_Data` con dueño `$APP_UID`; el compose monta el volumen `documentos`; la subida captura `UnauthorizedAccessException`. | Revisión de Dockerfile y compose (la imagen no se construyó: Docker no está instalado). |
| P1-3 | `.env.example` con dominio real y claves aleatorias; ya no usa `*` ni las claves rechazadas. | Inspección contra las guardas de `Program.cs`. |
| P1-4 | `00-rol-app.sql` reemplazado por `00-rol-app.sh`, que lee `CLAVE_APP` del entorno. | Revisión; requiere base nueva para ejecutarse. |
| P2-1 | `AuditoriaAcceso` escribe con un contexto propio en su propio alcance. | Compila; sin prueba de integración con PostgreSQL. |
| P2-2 | `AplicarTodoAsync` devuelve `ResultadoGuardado` y limpia el seguimiento al fallar; el cierre no se ejecuta si el guardado falló. | Compila; el flujo con base real queda por probar. |
| P2-3 | Se quitó la escritura del `GET /Alumnos/Detalle`; el estado efectivo se calcula al leer y la revisión programada (`AlertasProgramadas`, al arrancar y cada 15 min) persiste `OTORGADA → VENCIDA`, que antes quedaba sin escribir. | Prueba del barrido de vencidas en verde. |
| P2-4 | Las condiciones médicas y alergias se cierran con `vigente_hasta` en vez de borrarse; se revocó `DELETE` sobre `situacion_medica`. | Prueba: guardar la ficha sin condición médica cierra la anterior sin borrarla. Inspección del SQL. |
| P2-5 | Columna `sale_al_bano` (paso 16) con rescate del dato antiguo; la ficha muestra ambos campos por separado. | DDL actualizado en los tres scripts e instalación. |
| P2-6 | `/Home/Actividad` exige `AUDITORIA:LEER`. | Inspección. |
| P2-7 | Los filtros de Reportes usan `data-ux-enviar-al-cambiar` y el aviso del PDF ya lo resuelve `ux.js`; sin manejadores inline. | Inspección. |
| P2-8 | `Medicacion:Habilitada` (por omisión `false`) bloquea lectura y registro de dosis hasta aprobar el protocolo. | Inspección. |
| P3-1 | `RegistrarFirma` exige funcionario en sesión. | Inspección. |
| P3-2 | El historial de certificados exige permiso de emisión. | Inspección. |
| P3-6 | La sesión se invalida si se **quitan** permisos, no si se agregan. | Inspección. |
| P3-7 | La anulación agrega el motivo a las observaciones. | Suite existente en verde. |
| P3-8 | Indentación del bloque de arranque corregida. | Compila. |

**Pendiente por decisión del colegio:** P2-9 (`sub` del SSO frente a `persona.id`, antes de que Asistencia lo consuma) y P3-3 a P3-5 (poda de `alertas_revision`, cachés de comunas y autorización por rol en Configuración).

### Verificación de esta ronda

- `dotnet build -c Release`: 0 errores, 0 advertencias.
- `dotnet test -c Release`: **175 pruebas superadas, 0 fallos** (168 previas + 7 nuevas).
- Arranque en `Production` con variables de despliegue y sin base accesible: el portal inicia, `/Cuenta/Login` responde 200 y no hay excepción de SSO.
- **No verificado:** ejecución de la imagen Docker, conexión al PostgreSQL real, migración 16 sobre datos existentes ni recorrido visual de la interfaz.
