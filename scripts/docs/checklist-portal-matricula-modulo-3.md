# Checklist — Portal de Matrícula MPAZ Web (Módulo 3)

Escuela Escritora Marcela Paz · Basado en “Requerimientos Portal de Matrícula 2027” v1.0 (03-11-2026)  
Base de datos: `mpaz-matricula/database/01-ddl-institucional.sql` (fuente de verdad del esquema)  
**Estado revisado:** 7 de noviembre de 2026

## Criterio de esta revisión

- `[x]`: existe evidencia concreta en el repositorio y se comprobó mediante inspección, compilación o una ruta ejecutada.
- `[ ]`: pendiente o implementación parcial. Cuando existe un avance se indica como **Parcial** sin marcarlo como terminado.
- `[BLOQUEADO]`: requiere una decisión del colegio o coordinación del equipo.
- Esta revisión no implementa funcionalidades ni modifica el servidor del colegio.

### Verificaciones ejecutadas

- [x] La solución compila en `Release`: `dotnet build MarcelaPaz.Matriculas.slnx -c Release --no-restore` — 0 errores y 3 advertencias.
- [x] Aplicación local accesible: `/`, `/Ficha/Buscar` y `/Cuenta/Login` respondieron HTTP 200 en `http://localhost:5195`.
- [x] Se inspeccionaron el DDL, entidades, contexto EF Core, controladores, vistas, seguridad, PDF y scripts de permisos.
- [ ] No existe un proyecto de pruebas automatizadas en `mpaz-matricula`; las reglas funcionales y de seguridad no tienen pruebas de regresión.
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
- [ ] [José] Alinear entidades y migraciones al DDL (curso y año en `matricula_anual`; familia en `estudiante_persona`; sin `autoriza_paracetamol`) — **Parcial:** `MatriculaDbContext` mapea `matricula_anual` y `estudiante_persona`, pero `FichaForm`, `Paso6`, `FichaController` y `FichaImpresion` todavía incluyen autorización de paracetamol. Solo existe una migración para Identity; las tablas `public` dependen del DDL.
- [x] [José] Verificar si PostgreSQL y el runtime de la app están instalados en el servidor; instalar lo que falte — **Comprobado:** `aspnetcore-runtime-10.0 10.0.14-0ubuntu1~24.04.1` y `postgresql-16 16.15-0ubuntu0.24.04.1` desde los repositorios de Ubuntu 24.04; sin SDK ni Docker.
- [x] [José] Cargar el DDL en PostgreSQL y correr una prueba de humo — **Comprobado:** DDL y esquema `auth` aplicados sobre `mpaz_matricula`; `/Cuenta/Login` responde 200 y se crearon 7 cuentas de Identity.
- [x] [José] Crear el rol de base de datos de la app, sin permiso de borrar ni modificar las tablas solo-inserción — **Comprobado:** rol `mpaz_app` con `database/04-privilegios-aplicacion.sql`; `has_table_privilege` confirma lectura de `estudiante`, sin UPDATE en `registros_acceso` y UPDATE sobre `auth."AspNetUsers"`.

## 3. Seguridad y acceso (sec. 10 y 11)

- [ ] [José] Login de funcionarios con cuentas propias (hash de contraseña, bloqueo tras intentos fallidos, sesiones seguras) — **Parcial:** Identity almacena hash, exige contraseña mínima, bloquea tras 5 intentos por 15 minutos y usa cookie `HttpOnly`/`Secure`; la configuración base y el servidor tienen `Seguridad:ExigirInicioSesion=true`, y se crearon 7 cuentas desde los funcionarios mock. Falta probar credenciales reales, cierre de sesión y bloqueo.
- [ ] [José] Pantalla de administración de cuentas y roles, visible solo para `ADMIN_TI` — **Parcial:** existen `FuncionariosController` y sus vistas con política `ADMIN:ADMINISTRAR`, pero el interruptor de seguridad desactivado vuelve abierta esa política.
- [x] [José] Roles: SECRETARIA, DIRECCION, INSPECTORIA, EQUIPO_PIE, TENS, DOCENTE, ADMIN_TI — verificados en `01-ddl-institucional.sql` y modelados en `Domain/Entidades/Seguridad.cs`.
- [ ] [José] **MAT-12** Permisos leer / crear / editar / exportar / validar / administrar, por rol y por área de datos, aplicados en el servidor — **Parcial:** existe `PermisoPolicyProvider`, claims desde `rol_permiso` y `03-permisos-iniciales.sql`; solo algunas acciones usan políticas y todas quedan abiertas mientras `ExigirInicioSesion=false`.
- [ ] [José] Denegar y registrar el intento cuando un rol no tiene permiso (también en la API) — **Parcial:** `PermisoAuthorizationHandler` y `AuditoriaAcceso` registran `DENEGADO`, pero no operan efectivamente con seguridad desactivada y no hay una API separada comprobada.
- [ ] [José] **MAT-13** Ejecutar `SET LOCAL app.funcionario_id` en cada transacción que escriba — **Parcial:** `EscrituraAuditada` usa `set_config(..., true)` en administración de funcionarios; `RegistroFicha` guarda los pasos sin ese envoltorio.
- [ ] [José] **MAT-13** Registrar en `registros_acceso` lecturas, búsquedas, descargas, exportaciones y accesos denegados de datos sensibles — **Parcial:** se registran denegaciones de login y políticas; no se encontraron llamadas para lectura, búsqueda, descarga o exportación.
- [ ] [José] TLS en tránsito; archivos y respaldos protegidos; mínimos privilegios — configurado parcialmente para cookies/HSTS en producción, pero no desplegado ni comprobado.
- [ ] [José] [BLOQUEADO] Cargar la matriz de permisos en `rol_permiso` cuando el colegio la apruebe — existe propuesta no aprobada en `database/03-permisos-iniciales.sql`.

## 4. Etapa I — Captura y PDF

- [ ] [José] **MAT-01** Crear matrícula nueva y renovación por año escolar — **Parcial:** `FichaController.Nueva`, `Buscar` y `Renovar` distinguen ambos flujos y `RegistroFicha` crea `matricula_anual` en BORRADOR; falta cerrar y comprobar el proceso completo.
- [ ] [José] Formulario en pasos, con guardado de borrador y mensajes de error claros — **Parcial:** hay wizard de 7 pasos, TempData y escritura progresiva en PostgreSQL; el propio código declara que no tiene validación definitiva y algunas reglas están desactivadas.
- [ ] [José] Bloque A: estudiante (nombres, apellidos, identificador RUN/IPE/IPA, nacimiento, nacionalidad, domicilio, curso) — **Parcial:** interfaz y persistencia para RUN; no hay soporte completo comprobado para IPE/IPA.
- [ ] [José] **MAT-02** Validar RUN (dígito verificador) y evitar estudiantes duplicados — **Parcial:** `RegistroFicha.RutValido`, normalización, búsqueda previa y restricción única del DDL; falta prueba automatizada/consulta de aceptación.
- [ ] [José] Bloque B: madre, padre, apoderado principal y secundario (teléfono, correo, domicilio, escolaridad, restricción judicial sí/no) — **Parcial:** pasos 2 y 3 guardan familia y apoderados; la restricción está modelada solo como restricción de la madre y falta validación final.
- [ ] [José] Bloque C: contactos de emergencia, personas autorizadas a retirar, salida solo — **Parcial:** pasos 4 y 5 y persistencia implementados; no hay prueba de aceptación.
- [ ] [José] Bloque D: salud (situaciones médicas, alergias, necesidades de atención, contacto sanitario) — **Parcial:** paso 6 registra situaciones y salida al baño; faltan campos específicos de alergias y contacto sanitario.
- [ ] [José] Bloque E: PIE y apoyos (participa sí/no/pendiente, apoyos, existencia de diagnóstico) — no implementado en entidades, controlador ni vistas.
- [ ] [José] Bloques F y G: autorizaciones de imagen (por canal) y de convivencia, como checkbox con su texto — las tablas existen en el DDL, pero no hay interfaz ni persistencia en la aplicación.
- [ ] [José] **MAT-03** Registrar datos familiares, emergencia, retiro, salud y apoyos — **Parcial:** familia, emergencia, retiro y parte de salud están conectados; PIE/apoyos no.
- [ ] [José] **MAT-04** Registrar el estado de cada autorización (otorgada, rechazada, revocada, vencida) — disponible en el DDL, no conectado a la aplicación.
- [ ] [José] Estados de la matrícula: BORRADOR → FICHA_GENERADA → FIRMADA → CONFIRMADA (o ANULADA) — el DDL define estados y la aplicación crea BORRADOR; no implementa las transiciones completas.
- [ ] [José] Vista previa de la ficha antes de cerrarla — **Parcial:** `/Ficha/Comprobante` genera PDF desde el borrador, pero no cierra ni registra una versión.
- [ ] [José] **MAT-08** Generar el PDF desde la copia congelada (`datos_snapshot`), con folio, año, versión, textos aceptados y bloques de firma — **Parcial:** QuestPDF genera folio provisional, año, “borrador” y firmas; usa TempData, no `ficha_version.datos_snapshot`, y no registra la versión.
- [ ] [José] Botón para descargar e imprimir la ficha (registrar la descarga) — existe descarga/visualización de PDF, pero no auditoría de la descarga.
- [ ] [José] Accesibilidad básica del formulario — no existe auditoría WCAG ni prueba de teclado/lector de pantalla registrada.

## 5. Etapa II — Expediente y consulta

- [ ] [José] **MAT-06** Mostrar los enlaces oficiales de la sec. 12 y permitir subir los archivos descargados — no implementado.
- [ ] [José] **MAT-07** Marcar cada documento como recibido, no aplica o pendiente, sin bloquear la matrícula — definido en el DDL, no conectado a la aplicación.
- [ ] [José] Subir archivos PDF/JPG/PNG con límite de tamaño, guardados fuera de directorios públicos, con hash y análisis del archivo — no implementado.
- [ ] [José] **MAT-09** Subir el escaneo de la ficha firmada y asociarlo a la versión generada — definido en el DDL, no implementado en la aplicación.
- [ ] [José] **MAT-10** Registrar la ubicación del original en papel y el estado de validación documental — definido en el DDL, no implementado en la aplicación.
- [ ] [José] Confirmar la matrícula según el procedimiento oficial (registrar quién y cuándo) — no implementado.
- [ ] [José] **MAT-11** Consulta administrativa: búsqueda global y filtros por RUN/identificador, nombre, curso, año, estado y pendientes — **Parcial:** solo existe búsqueda por RUN para iniciar nueva matrícula o renovación.
- [ ] [José] Vista previa del PDF firmado; descarga solo si el rol lo permite — no implementado.
- [ ] [José] Pestañas con acceso condicionado: salud, PIE, documentos judiciales — no implementado.
- [ ] [José] Mostrar última actualización y funcionario responsable — no implementado en las vistas revisadas.
- [ ] [José] Acceso de lectura concurrente; control de versión para evitar sobrescrituras al editar — el DDL tiene versionado/triggers en tablas vivas, pero la aplicación no maneja conflictos de concurrencia.
- [ ] [José] [BLOQUEADO] Medicación por TENS: prescripción, autorización y registro de dosis (**MAT-05**) — el DDL contiene las tablas y restricciones, pero no debe conectarse hasta aprobar el protocolo.

## 6. Etapa III — Continuidad

- [ ] [José] **MAT-01** Renovación anual: precargar datos vigentes, exigir confirmación y generar la ficha del año nuevo — **Parcial:** `Renovar` y `CompletarDesdeBaseAsync` precargan estudiante, familia, contactos, retiro y salud; no hay confirmación/cierre de ficha anual.
- [ ] [José] **MAT-14** Versiones históricas de la ficha, autorizaciones y cambios de apoderado — soportado parcialmente por el DDL; la aplicación no crea ni consulta `ficha_version` o autorizaciones.
- [ ] [José] **MAT-18** Corrección controlada: crear una versión nueva con motivo, sin tocar el documento firmado — protegido/modelado en el DDL, no implementado en la aplicación.
- [ ] [José] Revocación de autorizaciones (imagen, convivencia): conservar firma y fecha, bloquear el uso posterior — modelado en el DDL, no implementado en la aplicación.
- [ ] [José] **MAT-16** Exportar reportes autorizados: estado de matrículas y faltantes documentales — no implementado.
- [ ] [José] **MAT-17** Alertas de órdenes médicas y autorizaciones vencidas o por vencer — vistas de apoyo en el DDL, sin interfaz o proceso de alertas.
- [ ] [José] Auditoría de cuentas: agregar `funcionario` a los triggers de auditoría — pendiente; el DDL revisado declara trigger específico para `funcionario_rol`, no para `funcionario`.

## 7. Pruebas de aceptación (sec. 13)

- [ ] Ficha completa: genera el PDF imprimible con todos los datos y las firmas, sin transcribir a mano — existe generador parcial, pero no se ejecutó la aceptación completa ni usa snapshot cerrado.
- [ ] Falta un documento complementario: queda como pendiente y no bloquea la matrícula — no implementado.
- [ ] Dos funcionarios consultan al mismo estudiante: ven la misma versión, sin esperar una carpeta física — no probado.
- [ ] El apoderado revoca la imagen: el estado queda actualizado, con la versión histórica y la fecha, y se bloquea la difusión — no implementado.
- [ ] Un docente sin permiso pide un diagnóstico: acceso denegado en pantalla y API, con evento de auditoría — no probado; seguridad global desactivada.
- [ ] Un TENS registra una dosis: exige autorización y orden vigentes, y guarda responsable, fecha, hora y dosis — solo existen reglas en DDL; bloqueado por protocolo.
- [ ] Se corrige una matrícula firmada: se conserva el original y se genera una versión nueva — no implementado en aplicación.
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
- [ ] [José] Publicar en subdominio institucional con Cloudflare Access — **Parcial:** zona `empaz.cl` **activa** (DNS Full, plan Free) y túnel `mpaz-rfid-tunnel` `Healthy` con 1 ruta, verificado el 06-11-2026; el panel se consulta con la cuenta personal del colega (`Tap.tapia@duocuc.cl`) y **requiere su autorización** antes de crear o modificar algo. Zero Trust aún sin configurar, por lo que no hay app de Access. Pendiente: `Seguridad__ExigirInicioSesion=true`, team de Zero Trust, app de Access y hostname `matricula.empaz.cl` → `http://127.0.0.1:5100`. Detalle en `scripts/docs/respuesta-ti-cloudflare.md`. **En pausa:** José no modifica la cuenta del colega; espera invitación como miembro (*Members → Invite*).
- [x] [José] Revisar qué se publica por Cloudflare y a quién se expone — documentado en `scripts/docs/deployment.md` y `migracion-servidor-runbook.md`.
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
