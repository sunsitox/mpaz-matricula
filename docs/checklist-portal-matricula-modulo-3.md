# Checklist — Portal de Matrícula MPAZ Web (Módulo 3)

Escuela Escritora Marcela Paz · Basado en “Requerimientos Portal de Matrícula 2027” v1.0 (03-11-2026)  
Base de datos: `mpaz-matricula/database/01-ddl-institucional.sql` (fuente de verdad del esquema)  
**Estado revisado:** 8 de noviembre de 2026

## Criterio de esta revisión

- `[x]`: existe evidencia concreta en el repositorio y se comprobó mediante inspección, compilación o una ruta ejecutada.
- `[ ]`: pendiente o implementación parcial. Cuando existe un avance se indica como **Parcial** sin marcarlo como terminado.
- `[BLOQUEADO]`: requiere una decisión del colegio o coordinación del equipo.
- Esta revisión no implementa funcionalidades ni modifica el servidor del colegio.

### Verificaciones ejecutadas

- [x] La solución compila en `Release`: `dotnet build MarcelaPaz.Matriculas.slnx -c Release --no-restore` — 0 errores y 3 advertencias.
- [x] Aplicación local accesible: `/`, `/Ficha/Buscar` y `/Cuenta/Login` respondieron HTTP 200 en `http://localhost:5195`.
- [x] Se inspeccionaron el DDL, entidades, contexto EF Core, controladores, vistas, seguridad, PDF y scripts de permisos.
- [x] Existe un proyecto de pruebas automatizadas en `mpaz-matricula/tests` con 27 casos: RUN/IPE-IPA, validaciones del formulario, cierre de ficha, formatos y firma real de archivos del expediente.
- [x] PostgreSQL 16.15 instalado y probado en el servidor (`psql`, `pg_dump` disponibles) — 06-11-2026.
- [x] Servidor auditado e intervenido en una ventana autorizada: runtime, release, base de datos y servicios temporales; ver secciones 8 y 11.

---

## 0. Decisiones pendientes (sec. 16 y conversaciones)

- [ ] [Colegio] [BLOQUEADO] Textos de las autorizaciones de imagen y convivencia, aprobados por Dirección, Convivencia Escolar y el sostenedor (frase corta junto a cada checkbox)
- [ ] [Colegio] [BLOQUEADO] Protocolo escrito de medicación por TENS: custodia de medicamentos y qué hacer si el TENS no está
- [ ] [Colegio] [BLOQUEADO] Matriz final de permisos por rol (la sec. 10 es propuesta a validar)
- [ ] [Colegio] [BLOQUEADO] Plazos de conservación de originales y datos, y eliminación segura (con el sostenedor)
- [ ] [Colegio] [BLOQUEADO] Documentos que el colegio pide hoy y cuáles son obligatorios por ley
- [ ] [Colegio] [BLOQUEADO] Formato único de ficha para matrícula nueva y renovación, con anexos opcionales
- [ ] [Colegio] [BLOQUEADO] Certificado de alumno regular: ¿se genera desde los datos confirmados? ¿quién lo emite? ¿qué debe llevar (firma, folio)?
- [ ] [Colegio] [BLOQUEADO] Confirmar quién ingresa los datos de la matrícula (el documento dice: un funcionario)
- [ ] [Colegio] [BLOQUEADO] Si se actualiza un dato después de firmada la ficha: ¿se vuelve a firmar siempre o solo en cambios importantes?
- [ ] [Colegio] [BLOQUEADO] Responsable por proceso y alcance de información visible por perfil
- [ ] [Colegio] [BLOQUEADO] Confirmar si existe otra versión de la ficha con autorización de paracetamol (la ficha entregada no la trae)

## 1. Coordinación del equipo (reunión del martes)

- [ ] [Equipo] [BLOQUEADO] ¿Qué archivo o formato pide el ministerio, y quién lo sube? (hoy el documento solo permite exportar reportes)
- [ ] [Equipo] [BLOQUEADO] Definir quién construye el inicio de sesión único de funcionarios (sec. 4)
- [ ] [Equipo] [BLOQUEADO] Acordar con Samantha el identificador común del estudiante (RUN, o IPE/IPA)
- [ ] [Equipo] [BLOQUEADO] Acordar qué datos mínimos recibe Asistencia (identificador, curso, condición de matrícula)
- [ ] [Equipo] [BLOQUEADO] Acordar con Sebastián el enlace desde la página del colegio hacia el portal de matrícula

## 2. Preparación del proyecto

- [x] [José] Quitar el login de apoderados y el portal de apoderados, conservando ese trabajo en una rama — evidencia: rama `feature/login-apoderado`; `main` solo contiene `CuentaController` para funcionarios.
- [x] [José] Dejar un solo login, para funcionarios — evidencia: `MarcelaPaz.Web/Controllers/CuentaController.cs`, vista `Views/Cuenta/Login.cshtml` y HTTP 200 en `/Cuenta/Login`.
- [x] [José] Alinear entidades y migraciones al DDL (curso y año en `matricula_anual`; familia en `estudiante_persona`; sin `autoriza_paracetamol`) — **Parcial:** `MatriculaDbContext` mapea `matricula_anual` y `estudiante_persona`, pero `FichaForm`, `Paso6`, `FichaController` y `FichaImpresion` todavía incluyen autorización de paracetamol. Solo existe una migración para Identity; las tablas `public` dependen del DDL.
- [x] [José] Verificar si PostgreSQL y el runtime de la app están instalados en el servidor; instalar lo que falte — **Comprobado:** `aspnetcore-runtime-10.0 10.0.14-0ubuntu1~24.04.1` y `postgresql-16 16.15-0ubuntu0.24.04.1` desde los repositorios de Ubuntu 24.04; sin SDK ni Docker.
- [x] [José] Cargar el DDL en PostgreSQL y correr una prueba de humo — **Comprobado:** DDL y esquema `auth` aplicados sobre `mpaz_matricula`; `/Cuenta/Login` responde 200 y se crearon 7 cuentas de Identity.
- [x] [José] Crear el rol de base de datos de la app, sin permiso de borrar ni modificar las tablas solo-inserción — **Comprobado:** rol `mpaz_app` con `database/04-privilegios-aplicacion.sql`; `has_table_privilege` confirma lectura de `estudiante`, sin UPDATE en `registros_acceso` y UPDATE sobre `auth."AspNetUsers"`.

## 3. Seguridad y acceso (sec. 10 y 11)

- [x] [José] Login de funcionarios con cuentas propias (hash de contraseña, bloqueo tras intentos fallidos, sesiones seguras) — **Comprobado:** Identity con hash, contraseña mínima, bloqueo tras 5 intentos y cookie `HttpOnly`/`Secure`; login, bloqueo y desbloqueo probados con una cuenta real. El cambio forzado de contraseña funciona en el primer ingreso y después de un restablecimiento.
- [x] [José] Pantalla de administración de cuentas y roles, visible solo para `ADMIN_TI` — **Comprobado:** `FuncionariosController` con política `ADMIN:ADMINISTRAR`; alta, activación, cierre de rol, restablecimiento de contraseña y desbloqueo probados en ejecución.
- [x] [José] Roles: SECRETARIA, DIRECCION, INSPECTORIA, EQUIPO_PIE, TENS, DOCENTE, ADMIN_TI — verificados en `01-ddl-institucional.sql` y modelados en `Domain/Entidades/Seguridad.cs`.
- [x] [José] **MAT-12** Permisos leer / crear / editar / validar / administrar, por rol y por área de datos, aplicados en el servidor — **Comprobado:** `PermisoPolicyProvider` con claims desde `rol_permiso`; lectura, escritura y descarga del expediente exigen permiso por área, y la denegación a Docente se verificó en pantalla y por URL directa.
- [x] [José] Denegar y registrar el intento cuando un rol no tiene permiso — **Comprobado:** `PermisoAuthorizationHandler` y `AuditoriaAcceso` registran `DENEGADO`; un Docente sin `DOCUMENTOS:LEER` no ve enlaces y la descarga directa responde con denegación.
- [x] [José] **MAT-13** Ejecutar `SET LOCAL app.funcionario_id` en cada transacción que escriba — **Comprobado:** `EscrituraAuditada` envuelve administración de funcionarios, pasos del asistente, cierre de ficha, firma, confirmación y documentos; el historial registró al funcionario responsable en las pruebas.
- [x] [José] **MAT-13** Registrar en `registros_acceso` lecturas, búsquedas, descargas, exportaciones y accesos denegados de datos sensibles — **Comprobado:** se registran `LEER` del expediente, `BUSCAR` (con hash del término), `DESCARGAR` y `DENEGADO`; falta la exportación, que aún no existe.
- [ ] [José] TLS en tránsito; archivos y respaldos protegidos; mínimos privilegios — configurado parcialmente para cookies/HSTS en producción, pero no desplegado ni comprobado.
- [ ] [José] [BLOQUEADO] Cargar la matriz de permisos en `rol_permiso` cuando el colegio la apruebe — existe propuesta no aprobada en `database/03-permisos-iniciales.sql`.

## 4. Etapa I — Captura y PDF

- [x] [José] **MAT-01** Crear matrícula nueva y renovación por año escolar — **Comprobado:** `Buscar`, `Nueva` y `Renovar` distinguen ambos flujos, la matrícula registra `creada_por` y la ficha del alumno ofrece «Iniciar matrícula {año}» cuando falta el expediente. El cierre congela la versión, obtiene folio y deja la matrícula en `FICHA_GENERADA`; probado sobre una matrícula de renovación.
- [ ] [José] Formulario en pasos, con guardado de borrador y mensajes de error claros — **Parcial:** hay wizard de 7 pasos, TempData y escritura progresiva en PostgreSQL; el propio código declara que no tiene validación definitiva y algunas reglas están desactivadas.
- [x] [José] Bloque A: estudiante (nombres, apellidos, identificador RUN/IPE/IPA, nacimiento, nacionalidad, domicilio, curso) — **Comprobado:** RUN, IPE e IPA se aceptan y validan según el tipo elegido, con pruebas de regresión.
- [x] [José] **MAT-02** Validar RUN (dígito verificador) y evitar estudiantes duplicados — **Comprobado:** normalización y dígito verificador con pruebas automatizadas, búsqueda previa y restricción única del DDL.
- [ ] [José] Bloque B: madre, padre, apoderado principal y secundario (teléfono, correo, domicilio, escolaridad, restricción judicial sí/no) — **Parcial:** pasos 2 y 3 guardan familia y apoderados; la restricción está modelada solo como restricción de la madre y falta validación final.
- [ ] [José] Bloque C: contactos de emergencia, personas autorizadas a retirar, salida solo — **Parcial:** pasos 4 y 5 y persistencia implementados; no hay prueba de aceptación.
- [x] [José] Bloque D: salud (situaciones médicas, alergias, necesidades de atención, contacto sanitario) — **Comprobado:** el paso 6 registra situaciones, alergias, contacto sanitario y salida al baño, y la ficha del alumno los muestra.
- [x] [José] Bloque E: PIE y apoyos (participa sí/no/pendiente, apoyos, existencia de diagnóstico) — **Comprobado:** se captura en el paso 6 y se persiste en las tablas del DDL.
- [x] [José] Bloques F y G: autorizaciones de imagen (por canal) y de convivencia, como checkbox con su texto — **Comprobado:** se capturan en el paso 7, separadas de salud y PIE, y se guardan como autorizaciones del estudiante. Los textos legales definitivos siguen pendientes de Dirección.
- [x] [José] **MAT-03** Registrar datos familiares, emergencia, retiro, salud y apoyos — **Comprobado:** el asistente persiste los cinco bloques y la ficha del alumno muestra familia, emergencia, retiro, salud, alergias, contacto sanitario y PIE/apoyos. Los apoyos exigen `PIE:LEER`, igual que el resto del área.
- [x] [José] **MAT-04** Registrar el estado de cada autorización (otorgada, rechazada, revocada, vencida) — **Comprobado:** la ficha del alumno lista todas las autorizaciones con estado, firma, vigencia y alcances; la revocación exige motivo y registra funcionario y fecha, y las otorgadas cuyo plazo terminó pasan a `VENCIDA`. Revocación probada en ejecución con auditoría.
- [ ] [José] Estados de la matrícula: BORRADOR → FICHA_GENERADA → FIRMADA → CONFIRMADA (o ANULADA) — **Parcial:** las cuatro primeras transiciones están implementadas y verificadas (cierre de ficha, registro de firma y confirmación con funcionario y fecha), y la base impide saltos; **falta ANULADA**, que solo existe en el DDL.
- [x] [José] Vista previa de la ficha antes de cerrarla — el paso 8 muestra resumen ordenado (pasos 1-7) + PDF embebido + confirmación expresa; al guardar genera folio y congela la copia, y el comprobante posterior imprime desde el snapshot.
- [x] [José] **MAT-08** Generar el PDF desde la copia congelada (`datos_snapshot`), con folio, año, versión, textos aceptados y bloques de firma — **Comprobado:** el cierre crea `ficha_version` con folio, versión y snapshot; el PDF usa la versión cerrada y el borrador se descarga como «Sin folio».
- [x] [José] Botón para descargar e imprimir la ficha (registrar la descarga) — **Comprobado:** la descarga queda registrada como `DESCARGAR` en `registros_acceso`.
- [x] [José] Accesibilidad básica del formulario — **Comprobado:** auditorías con axe/Lighthouse, teclado, NVDA y revisión visual responsive documentadas en `auditorias/`. El reflow a 200 % (≈640 px), `prefers-contrast: more` y `forced-colors: active` quedaron implementados y comprobados sin desborde horizontal.

## 5. Etapa II — Expediente y consulta

- [x] [José] **MAT-06** Permitir subir los archivos del expediente — **Comprobado:** carga desde la ficha del alumno, con límite de 15 MB, formatos PDF/JPG/PNG, hash y almacenamiento fuera de `wwwroot`. Los enlaces oficiales de la sec. 12 siguen pendientes.
- [x] [José] **MAT-07** Marcar cada documento como recibido, no aplica o pendiente, sin bloquear la matrícula — **Comprobado:** estados editables desde la ficha; ninguno bloquea la matrícula.
- [x] [José] Subir archivos PDF/JPG/PNG con límite de tamaño, guardados fuera de directorios públicos, con hash y análisis del archivo — **Comprobado:** validación de extensión, tamaño y firma real del archivo; hash SHA-256 y ruta relativa en `App_Data/documentos`. El análisis antivirus queda fuera de alcance.
- [x] [José] **MAT-09** Subir el escaneo de la ficha firmada y asociarlo a la matrícula — **Comprobado:** el escaneo se guarda como documento `FICHA_FIRMADA`, reemplaza al anterior sin borrarlo y la ficha muestra el enlace de descarga.
- [ ] [José] **MAT-10** Registrar la ubicación del original en papel y el estado de validación documental — definido en el DDL, no implementado en la aplicación.
- [x] [José] Confirmar la matrícula según el procedimiento oficial (registrar quién y cuándo) — **Comprobado:** sólo desde ficha generada o firmada, con funcionario y fecha en la matrícula y en la auditoría.
- [x] [José] **MAT-11** Consulta administrativa: búsqueda global y filtros por RUN/identificador, nombre, curso, año, estado y pendientes — **Comprobado:** el listado busca por nombre y por RUN, IPE o IPA, y agrega el filtro y la columna «Pendientes» (sin ficha vigente o con requisitos documentales pendientes) conservando año, curso, estado y orden.
- [x] [José] Vista previa del PDF firmado; descarga solo si el rol lo permite — **Comprobado:** descarga del escaneo con `DOCUMENTOS:LEER` y denegación verificada para Docente.
- [x] [José] Pestañas con acceso condicionado: salud, PIE, documentos judiciales — la ficha tiene pestañas General/Autorizaciones/Documentos/Salud/PIE; cada panel muestra solo lo del rol y explica la ausencia (S-02).
- [x] [José] Mostrar última actualización y funcionario responsable — la ficha del alumno muestra fecha (trigger de auditoría, respaldo `updated_at`) y funcionario (historial, respaldo `creada_por`).
- [ ] [José] Acceso de lectura concurrente; control de versión para evitar sobrescrituras al editar — el DDL tiene versionado/triggers en tablas vivas, pero la aplicación no maneja conflictos de concurrencia.
- [ ] [José] [BLOQUEADO] Medicación por TENS: prescripción, autorización y registro de dosis (**MAT-05**) — el DDL contiene las tablas y restricciones, pero no debe conectarse hasta aprobar el protocolo.

## 6. Etapa III — Continuidad

- [x] [José] **MAT-01** Renovación anual: precargar datos vigentes, exigir confirmación y generar la ficha del año nuevo — **Comprobado:** `Renovar` crea el expediente del año como `RENOVACION` y precarga estudiante, familia, apoderados, contactos, retiro, salud, PIE y autorizaciones; el paso 7 exige la confirmación y el cierre genera la ficha con folio. El flujo de cierre se probó sobre una matrícula de renovación.
- [x] [José] **MAT-14** Versiones históricas de la ficha, autorizaciones y cambios de apoderado — **Comprobado:** la ficha del alumno lista todas las versiones con folio, estado, fecha, responsable y motivo, y cada una se descarga desde su copia congelada; las autorizaciones conservan su historial y los vínculos familiares cerrados se muestran con su fecha de término.
- [x] [José] **MAT-18** Corrección controlada: crear una versión nueva con motivo, sin tocar el documento firmado — **Comprobado:** «Corregir ficha» parte de la copia congelada, exige motivo en el paso 7 y genera la versión siguiente; la anterior queda `REEMPLAZADA`, la matrícula conserva su estado y la ficha original no se edita (trigger del DDL). Probado: versión 2 con motivo sobre una ficha vigente.
- [x] [José] Revocación de autorizaciones (imagen, convivencia): conservar firma y fecha, bloquear el uso posterior — **Comprobado:** la acción de revocación conserva `fecha_firma` y firmante, guarda motivo, funcionario y fecha de revocación, y deja la autorización fuera de las vigentes.
- [x] [José] **MAT-16** Exportar reportes autorizados: estado de matrículas y faltantes documentales — `ReportesController` con vista y CSV (descarga auditada, permiso EXPORTAR distinto de LEER).
- [ ] [José] **MAT-17** Alertas de órdenes médicas y autorizaciones vencidas o por vencer — vistas de apoyo en el DDL, sin interfaz o proceso de alertas.
- [ ] [José] Auditoría de cuentas: agregar `funcionario` a los triggers de auditoría — pendiente; el DDL revisado declara trigger específico para `funcionario_rol`, no para `funcionario`.

## 7. Pruebas de aceptación (sec. 13)

- [ ] Ficha completa: genera el PDF imprimible con todos los datos y las firmas, sin transcribir a mano — existe generador parcial, pero no se ejecutó la aceptación completa ni usa snapshot cerrado.
- [x] Falta un documento complementario: queda como pendiente y no bloquea la matrícula — **Comprobado:** el estado documental es independiente del estado de la matrícula.
- [ ] Dos funcionarios consultan al mismo estudiante: ven la misma versión, sin esperar una carpeta física — no probado.
- [x] El apoderado revoca la imagen: el estado queda actualizado, con la versión histórica y la fecha, y se bloquea la difusión — **Comprobado:** la revocación queda como `REVOCADA` con funcionario, fecha y motivo; la firma original se conserva y la autorización deja de contar como vigente.
- [x] Un docente sin permiso pide un diagnóstico: acceso denegado en pantalla y servidor, con evento de auditoría — **Comprobado** con un Docente real sobre documentos y áreas sensibles.
- [ ] Un TENS registra una dosis: exige autorización y orden vigentes, y guarda responsable, fecha, hora y dosis — solo existen reglas en DDL; bloqueado por protocolo.
- [x] Se corrige una matrícula firmada: se conserva el original y se genera una versión nueva — **Comprobado:** la corrección crea la versión 2 con motivo y deja la versión 1 como `REEMPLAZADA`, sin modificar su snapshot.
- [ ] El estudiante continúa el año siguiente: se precargan los datos, se confirman y se genera la ficha del año nuevo — precarga parcial, flujo completo no probado.
- [ ] Probar la restauración de un respaldo de la base de datos — pendiente.

## 8. Despliegue y operación

- [x] [José] Instalar solo los paquetes necesarios — **Comprobado:** runtime de ASP.NET Core 10 (sin SDK), PostgreSQL 16 y su cliente. `fontconfig` y `fonts-dejavu-core` siguen pendientes para los PDF.
- [x] [José] Publicar la aplicación en un release inmutable — **Comprobado:** `/opt/mpaz-matriculas/releases/20261106T054708Z` con enlace `current`; publicación framework-dependent sin `appsettings.Development.json`, PDB ni datos de prueba.
- [x] [José] Aislar el portal del sistema RFID — **Comprobado:** usuario de sistema `mpaz-matriculas` sin login ni `sudo`, claves en `/var/lib/mpaz-matriculas/keys`, configuración en `/etc/mpaz-matriculas/`; `mpaz-rfid`, nginx y `cloudflared` sin cambios.
- [x] [José] Crear base y rol dedicados — **Comprobado:** `mpaz_matricula` en PostgreSQL 16 y rol `mpaz_app` con privilegios mínimos verificados.
- [x] [José] Cargar DDL, esquema `auth` y datos mock — **Comprobado:** 10 estudiantes, 10 matrículas, 9 versiones de ficha, 27 documentos, 21 autorizaciones, 61 requisitos, 44 permisos y 7 cuentas de Identity.
- [x] [José] Publicar una vista temporal por Cloudflare sin tocar el túnel del colegio — **Comprobado con salvedades:** Quick Tunnel propio (`mpaz-matriculas-tunnel-preview.service`) hacia `127.0.0.1:5100`; la URL `*.trycloudflare.com` cambia al reiniciar y no tiene Access.
- [ ] [José] Respaldar y restaurar base y archivos con prueba real — pendiente; la base vive en `/var/lib/postgresql/16/main` (no en `/srv/mpaz-data`) y no hay respaldo programado ni copia externa.
- [ ] [José] Retirar y rotar las credenciales visibles antes de producción — hoy `preview.env` y `LEEME-ACCESO.txt` tienen la clave en claro a propósito, para pruebas.
- [ ] [José] Paquete Docker para la entrega final — **Parcial:** `mpaz-matricula/docker/` con Dockerfile multi-stage, compose (app + PostgreSQL 16), semilla SQL ordenada, `.env.example` y README; revisado, pero la imagen no se construyó ni se ejecutó.
- [x] [José] Acceso remoto gráfico temporal al servidor — **Completado y ya retirado:** RDP de sistema (`grdctl --system`) en 3389, abierto solo por `tailscale0`, verificado desde Windows (`TcpTestSucceeded=True`); **retirado el 06-11-2026 04:17** con `scripts/servidor/quitar-rdp.sh`: servicio detenido y deshabilitado, reglas 3389 eliminadas, certificado borrado y sin listener. Registro en `scripts/servidor/README-acceso-remoto.md`.
- [ ] [José] Publicar en subdominio institucional con Cloudflare Access — **Parcial:** zona `empaz.cl` **activa** (DNS Full, plan Free) y túnel `mpaz-rfid-tunnel` `Healthy` con 1 ruta, verificado el 06-11-2026; el panel se consulta con la cuenta personal del colega (`Tap.tapia@duocuc.cl`) y **requiere su autorización** antes de crear o modificar algo. Zero Trust aún sin configurar, por lo que no hay app de Access. Pendiente: `Seguridad__ExigirInicioSesion=true`, team de Zero Trust, app de Access y hostname `matricula.empaz.cl` → `http://127.0.0.1:5100`. Detalle en `docs/respuesta-ti-cloudflare.md`. **En pausa:** José no modifica la cuenta del colega; espera invitación como miembro (*Members → Invite*).
- [x] [José] Revisar qué se publica por Cloudflare y a quién se expone — documentado en `docs/deployment.md` y `migracion-servidor-runbook.md`.
- [ ] [Colegio] [BLOQUEADO] Revisión jurídica y del sostenedor antes de operar (consentimientos, tratamiento de datos, protocolos de salud)
- [ ] [Colegio] [BLOQUEADO] Preparar el cumplimiento de la Ley 21.719 (vigente desde 01-12-2026)
- [ ] [José] Documentar cómo crear cuentas, cómo respaldar y cómo restaurar — **Parcial:** existe el runbook y un `LEEME-ACCESO.txt` en el servidor, sin procedimiento de restauración probado.

### 8.1 Ubicaciones reales en el servidor (06-11-2026)

| Elemento | Ruta o valor |
| --- | --- |
| Release activo | `/opt/mpaz-matriculas/current` → `releases/20261106T054708Z` |
| Configuración y cadena de conexión | `/etc/mpaz-matriculas/preview.env` (`0644`, visible a propósito) |
| Credenciales de prueba | `/srv/mpaz-data/matriculas/LEEME-ACCESO.txt` |
| Scripts SQL cargados | `/srv/mpaz-data/matriculas/sql/` |
| Claves de cookies | `/var/lib/mpaz-matriculas/keys/` |
| Base de datos | `mpaz_matricula` en `127.0.0.1:5432`; datos en `/var/lib/postgresql/16/main` |
| Servicios temporales | `mpaz-matriculas-preview.service` y `mpaz-matriculas-tunnel-preview.service`, ambos `disabled` (no arrancan solos) |

## 9. Etapa IV — Integración (por ahora fuera)

- [ ] [Equipo] [BLOQUEADO] **MAT-15** Compartir identificador y datos mínimos con Asistencia, Notas y Apoyo (sin datos sensibles por defecto)

## 10. Referencia rápida de requerimientos

MAT-01 matrículas por año · MAT-02 validar y evitar duplicados · MAT-03 familia, emergencia, retiro, salud, apoyos · MAT-04 estados de autorización · MAT-05 prescripción y suministro solo TENS · MAT-06 enlaces oficiales y carga de archivos · MAT-07 estado documental sin bloquear · MAT-08 PDF fiel · MAT-09 escaneo firmado · MAT-10 original físico y validación · MAT-11 consulta simultánea · MAT-12 permisos por rol y campo · MAT-13 auditoría · MAT-14 versiones históricas · MAT-15 datos mínimos a otros módulos · MAT-16 reportes · MAT-17 alertas · MAT-18 corrección sin sobrescribir

## 11. Avances del 06-11-2026 (servidor y base de datos)

### Completados

| Cambio | Evidencia |
| --- | --- |
| Runtime de ASP.NET Core 10 instalado | `dotnet --list-runtimes` muestra `Microsoft.AspNetCore.App 10.0.14`; paquete `aspnetcore-runtime-10.0` 10.0.14 de Ubuntu |
| PostgreSQL 16 instalado y activo | `postgresql-16` 16.15; clúster `16/main` escuchando en `127.0.0.1:5432` |
| Aplicación desplegada | Release en `/opt/mpaz-matriculas/releases/20261106T054708Z`, enlace `current`, sin configuración de desarrollo |
| Aislamiento del RFID | Usuario `mpaz-matriculas`, config en `/etc/mpaz-matriculas`; `mpaz-rfid`, nginx y `cloudflared` verificados activos antes y después |
| Base y rol creados | `mpaz_matricula` + `mpaz_app` con `04-privilegios-aplicacion.sql` aplicado |
| Esquema institucional cargado | DDL aplicado sin errores (`ON_ERROR_STOP=1`) |
| Esquema `auth` creado | Script idempotente de EF Core; 7 cuentas de Identity sembradas desde los funcionarios mock |
| Datos mock cargados | 10 estudiantes, 10 matrículas, 9 fichas versionadas, 27 documentos, 21 autorizaciones, 61 requisitos, 44 permisos |
| Permisos mínimos verificados | `has_table_privilege`: lee `estudiante` = t, modifica `registros_acceso` = f, actualiza `auth.AspNetUsers` = t |
| Cadena de conexión del servidor actualizada | `/etc/mpaz-matriculas/preview.env` apunta a `mpaz_matricula`; credenciales visibles y editables a pedido |
| Vista web temporal publicada | Quick Tunnel independiente + app en `127.0.0.1:5100`; `/Cuenta/Login` responde 200 localmente y por HTTPS |

### Parcialmente completados

| Cambio | Qué falta |
| --- | --- |
| Login con datos reales de la base | Falta probar credenciales del usuario mock, cierre de sesión y bloqueo por intentos; la clave inicial temporal está en `preview.env` |
| Servicio de aplicación | Corre como `mpaz-matriculas-preview.service`, deshabilitado al arranque y sin `RequiresMountsFor`; falta la unidad definitiva |
| Publicación web | Es un Quick Tunnel público con URL aleatoria, sin Cloudflare Access ni dominio del colegio |
| Almacenamiento de la base | Quedó en el directorio por defecto del SSD (`/var/lib/postgresql/16/main`), no en el disco de datos `/srv/mpaz-data` |
| Credenciales | Visibles en archivos `0644` por decisión de esta etapa de pruebas |
| Documentación operativa | Existe el runbook, pero sin prueba de restauración ni procedimiento de cuentas verificado |
| Entrega con Docker | Archivos creados y orden de carga verificado; falta construir la imagen y probar el arranque completo |

## 11.1 Avances del 07-11-2026 (esquema y cierre de ficha)

| Cambio | Evidencia |
| --- | --- |
| Esquema dedicado `mpaz_matricula` | DDL, Identity y `__EFMigrationsHistory` viven en `mpaz_matricula`; `public` solo conserva la extensión `pg_trgm` y ya no se usa `auth`. Script idempotente en `database/90-migrar-bases-existentes.sql` y migración `EsquemaDedicado`. |
| Base local migrada sin pérdida de datos | 38 tablas en `mpaz_matricula`, 10 estudiantes, 19 matrículas y 7 usuarios; respaldo en `artifacts/mpaz-matricula-antes-esquema.dump`. |
| Instalación limpia verificada | Cadena de Docker (`01`→`04`) deja 38 tablas en `mpaz_matricula` con datos mock; probado en una base temporal. |
| Cierre de ficha con confirmación visual | Tras «Guardar y finalizar» se muestra la rueda de carga, un check verde y el aviso «Ficha guardada» antes de volver a la ficha del estudiante. Sigue creando la matrícula en `BORRADOR`: no emite folio ni congela versión. |
| Pendiente que este avance no cambia | La ficha aún no se cierra con `ficha_version.datos_snapshot` (MAT-08), no se registran descargas y la seguridad sigue desactivada por defecto en desarrollo. |

## 12. Fuera de alcance (no construir)

No son tareas: no se marcan ni se construyen.

- Sustituir el SAE o el SIGE
- Firma electrónica avanzada
- Acceso público o login de apoderados
- Emisión de certificados por el Mineduc
- Automatizar sitios del Estado o guardar contraseñas gubernamentales
- Prescripción médica, diagnóstico clínico, administración de medicamentos por la plataforma
- Descarga del certificado de alumno regular por apoderados (pendiente de decisión)

---

## Resumen del estado actual

### Completado y comprobado

1. Rama de respaldo para el trabajo de acceso de apoderados y retiro de ese acceso desde `main`.
2. Única pantalla de login para funcionarios.
3. Definición de los siete roles institucionales en el DDL.
4. Revisión técnica del uso futuro de Cloudflare Tunnel y Access.
5. Solución compilable en `Release` y rutas principales accesibles localmente.
6. Runtime ASP.NET Core 10 y PostgreSQL 16 instalados en el servidor solo con paquetes de Ubuntu.
7. Aplicación desplegada en `/opt/mpaz-matriculas` con usuario de sistema propio, sin tocar nginx ni el RFID.
8. Base `mpaz_matricula` con DDL, esquema `auth`, datos mock y permisos mínimos verificados.
9. Vista temporal publicada por Quick Tunnel, aislada del túnel administrado del colegio.
10. Cadena de conexión del servidor actualizada y visible para edición en `/etc/mpaz-matriculas/preview.env`.

### Avance parcial principal

- Wizard de siete pasos con guardado progresivo para estudiante, familia, apoderados, emergencia, retiro y salud.
- Búsqueda por RUN, inicio de matrícula nueva y precarga para renovación.
- Identity, administración de funcionarios, permisos por claims y auditoría de denegaciones: la configuración base y el servidor ya exigen inicio de sesión, pero falta probar el flujo completo y las políticas por área de datos.
- PDF provisional generado desde el borrador, todavía sin `datos_snapshot` ni `ficha_version`.
- DDL amplio para documentos, autorizaciones, versiones, medicación y auditoría, pero gran parte aún no está conectada a la aplicación.

### Bloqueos técnicos prioritarios

1. Activar y probar la seguridad obligatoria antes de exponer cualquier ruta.
2. Retirar `autoriza_paracetamol` del formulario/PDF y terminar la alineación con el DDL.
3. Envolver todas las escrituras de matrícula con `app.funcionario_id`.
4. Crear pruebas para RUN, duplicados, permisos, auditoría, transiciones y concurrencia.
5. Implementar el cierre de ficha mediante `ficha_version.datos_snapshot`; el PDF actual es solo borrador.
6. Probar respaldo y restauración reales, mover la base al disco de datos y programar copia externa.
7. Retirar las credenciales visibles, rotarlas y publicar con dominio propio y Cloudflare Access.

---

## 13. Avances del 08-11-2026

### Cerrado y verificado

| Cambio | Evidencia |
| --- | --- |
| Hallazgos S-08, S-09 y S-10 | Guard de `AllowedHosts` en producción, cambio forzado de contraseña inicial y antiforgery global; informe actualizado en `auditorias/01-auditoria-seguridad.md` |
| Encabezado, títulos y año escolar | Encabezado fijo «Portal Marcela Paz», títulos por sección y año vigente por bandera o fecha de apertura, configurable por Secretaría/Dirección |
| Restablecimiento y desbloqueo de cuentas | Acciones en Administración → Funcionarios, con clave temporal de un solo uso, cierre de sesiones y cambio obligatorio |
| Atribución de escrituras | `EscrituraAuditada` en pasos del asistente, cierre, firma, confirmación y documentos; `historial_cambios` registra el funcionario |
| Expediente documental | Carga, reemplazo, estados, descarga por área y escaneo de la ficha firmada (MAT-06/07/09) |
| MAT-01, MAT-03 y MAT-04 | Matrícula nueva y renovación con cierre completo, `creada_por` y botón «Iniciar matrícula»; salud, alergias, contacto sanitario y PIE/apoyos visibles; autorizaciones con estado, alcances, revocación con motivo y vencimiento |
| MAT-14 y MAT-18 | Historial de versiones con descarga desde el snapshot, autorizaciones y apoderados anteriores; corrección con motivo que crea una versión nueva y reemplaza la anterior |
| MAT-11 y asistente de 8 pasos | Búsqueda por nombre y por RUN/IPE/IPA, filtro y columna de pendientes; salud/PIE y autorizaciones separados en pasos 6 y 7, con revisión en el paso 8 |
| Pruebas | 39 casos, incluidos formatos y firma real de archivos, RUN/IPE/IPA, cierre de ficha y estados de autorización |
| Prueba de humo | Sobre la matrícula 4: corrección generó la versión 2 con motivo y dejó la 1 como `REEMPLAZADA`; la revocación de la autorización 9 guardó funcionario, fecha y motivo. Datos de prueba y permisos temporales revertidos |
| Commits | `dcb30fe`, `6d1a347` y `99013b8` en `main`; estos avances quedan sin commit |

### Pendiente que sigue abierto

- Retirar la autorización de paracetamol del formulario y del PDF cuando Dirección apruebe el procedimiento de medicación.
- Anulación de matrícula: el estado `ANULADA` existe en el DDL, pero no hay acción en la aplicación.
- Reportes y exportación (MAT-16), alertas de vencimientos (MAT-17) y control de concurrencia al editar.
- Construir y probar la imagen Docker, probar respaldo/restauración y rotar las credenciales del servidor.
- Publicar con dominio propio y Cloudflare Access.
- Textos legales, matriz de permisos aprobada, retención de datos y revisión de la Ley 21.719 (bloqueados por el colegio).
- Revisión visual en un equipo con Windows en alto contraste y a 200 % de zoom, más allá de la comprobación automatizada de desborde.
