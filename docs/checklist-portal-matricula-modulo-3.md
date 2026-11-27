# Checklist — Portal de Matrícula MPAZ Web (Módulo 3)

Escuela Escritora Marcela Paz · Basado en “Requerimientos Portal de Matrícula 2027” v1.0 (03-11-2026)  
Base de datos: `mpaz-matricula/database/modulos/matricula/pasos/01-ddl-institucional.sql` (fuente de verdad del esquema)  
**Estado revisado:** 17 de noviembre de 2026

## Criterio de esta revisión

- `[x]`: existe evidencia concreta en el repositorio y se comprobó mediante inspección, compilación o una ruta ejecutada.
- `[ ]`: pendiente o implementación parcial. Cuando existe un avance se indica como **Parcial** sin marcarlo como terminado.
- `[BLOQUEADO]`: requiere una decisión del colegio o coordinación del equipo.
- Esta revisión no implementa funcionalidades ni modifica el servidor del colegio.

### Verificaciones ejecutadas

- [x] La solución compila en `Release`: `dotnet build MarcelaPaz.Matriculas.slnx -c Release --no-restore` — 0 errores y 0 advertencias (17-11-2026).
- [x] Aplicación local accesible: `/`, `/Cuenta/Login`, `/Alertas` y `/?anio=2027` respondieron HTTP 200 en `http://localhost:5301` (17-11-2026); `/Alertas?anio=2026` también responde 200.
- [x] Se inspeccionaron el DDL, entidades, contexto EF Core, controladores, vistas, seguridad, PDF y scripts de permisos.
- [x] Existe un proyecto de pruebas automatizadas en `mpaz-matricula/tests` con 133 casos: RUN/IPE, validaciones del formulario, cierre de ficha, formatos, firma real de archivos del expediente y alertas MAT-17.
- [x] PostgreSQL 16.15 instalado y probado en el servidor (`psql`, `pg_dump` disponibles) — 06-11-2026.
- [x] Servidor auditado e intervenido en una ventana autorizada: runtime, release, base de datos y servicios temporales; ver secciones 8 y 11.

### Correcciones de seguridad y datos cercanas (17-11-2026)

- [x] Secreto SSO fuera de `appsettings.Development.json`; vive en user-secrets y `EmisorSso` actualiza el cliente en Desarrollo sin recrear la base.
- [x] Fecha de firma acotada en el servidor: no puede ser posterior a hoy ni anterior a la generación de la ficha (`RegistroFicha.RegistrarFirmaAsync`).
- [x] Emisión de certificados por POST con antiforgery token y descarga PDF; GET de emisión responde 405.
- [x] Login con rate limiter por IP (20 intentos/5 min) que vuelve al formulario con aviso claro; mensaje único para cuenta, clave y bloqueo.
- [x] Interruptor de autorización sólo en Development (`ModoRevision`): fuera de Development siempre exige sesión y permisos.
- [x] `Cache-Control: no-store, no-cache, must-revalidate` en páginas autenticadas.
- [x] Privilegios de auditoría: el script ya revocaba `UPDATE`/`DELETE` sobre `historial_cambios`, `registros_acceso` y `administracion_medicamento`; se agregó una guarda que aborta el script si el rol `mpaz_app` los conserva.

Las correcciones anteriores están verificadas en compilación Release, rutas locales y auditoría de la base de desarrollo; no se aplicaron al servidor del colegio.

---

## 0. Decisiones pendientes (sec. 16 y conversaciones)

- [ ] [Colegio] [BLOQUEADO] Textos de las autorizaciones de imagen y convivencia, aprobados por Dirección, Convivencia Escolar y el sostenedor (frase corta junto a cada checkbox)
- [ ] [Colegio] [BLOQUEADO] Protocolo escrito de medicación por TENS: custodia de medicamentos y qué hacer si el TENS no está — documento de 12 preguntas preparado para llevar impreso (`docs/protocolo-medicacion-preguntas-TENS-direccion.docx`); a la espera de las respuestas de TENS y Dirección
- [ ] [Colegio] [BLOQUEADO] Matriz final de permisos por rol (la sec. 10 es propuesta a validar) — **Confirmado por Johan 17-11-2026 (citado por José, sin aplicar todavía):** "UTP, INSPECTORIA y DIRECCIÓN son los que formalmente pueden ver todo"; "INSPECTORIA tiene la particularidad de poder editar datos del estudiante junto con el de admin"; "UTP, DIRECCIÓN, PIE Y CONVIVENCIA solo pueden ver, no editar nada, y no deberían tener acceso para generar o editar una matrícula". UTP y CONVIVENCIA no existen hoy como roles en `01-ddl-institucional.sql` ni en `03-permisos-iniciales.sql`. Comparación con lo cargado hoy en `03-permisos-iniciales.sql` (sin modificar):
  - DIRECCIÓN hoy: `GENERAL/FAMILIA/RETIRO/SALUD/PIE/JUDICIAL/AUTORIZACIONES/DOCUMENTOS:LEER` + `GENERAL/AUTORIZACIONES/DOCUMENTOS:VALIDAR` + `EXPORTAR`. Casi coincide con "ver todo, sin generar/editar", salvo que le falta `MEDICACION:LEER` para el "todo" y hay que definir si `VALIDAR` se mantiene (¿validar es editar?).
  - INSPECTORIA hoy: `GENERAL/FAMILIA/RETIRO:LEER` + `RETIRO:EDITAR`. Difiere: Johan pide ver todo y editar datos del estudiante; hoy no ve SALUD/PIE/JUDICIAL/AUTORIZACIONES/DOCUMENTOS/MEDICACION ni edita GENERAL/FAMILIA.
  - EQUIPO_PIE hoy: `GENERAL/FAMILIA/PIE/DOCUMENTOS:LEER` + `PIE:EDITAR`. Difiere en edición: Johan pide solo lectura, sin generar ni editar matrícula.
  - SECRETARIA hoy: `GENERAL/FAMILIA/RETIRO/AUTORIZACIONES/DOCUMENTOS/SALUD/JUDICIAL:LEER/CREAR/EDITAR` + `EXPORTAR`. Johan no la menciona: queda sin confirmar quién crea/mantiene.
  - TENS/DOCENTE/ADMIN_TI: Johan no los menciona; se mantienen como están hasta la matriz final.
  - "admin" que edita junto a INSPECTORIA está ambiguo: hoy `ADMIN_TI` solo tiene `ADMIN:ADMINISTRAR/LEER` + `AUDITORIA:LEER`, sin acceso al expediente. Falta definir si es ADMIN_TI o Secretaría/encargado de matrícula.
  **Sin modificar `rol_permiso` ni crear roles UTP/CONVIVENCIA hasta la confirmación final.** — **Actualizado 26-11-2026:** la matriz del área de matrícula quedó confirmada y es la ya cargada (ver "quién formaliza"); la escritura de UTP y Convivencia en notas de retiro y situación familiar pertenece a otras áreas y se mantiene. Falta sólo cargar la matriz en la base del colegio.
- [x] [Colegio] Conservación de datos — **Confirmado 26-11-2026:** por ley los datos no se pueden borrar; se conservan. No se planifica eliminación ni plazos de descarte.
- [x] [Colegio] Documentos exigidos — **Retirado del alcance 26-11-2026:** se siguen definiendo; cada dato se formalizará como documento puntual. No hay lista que bloquee.
- [ ] [Colegio] [BLOQUEADO] Formato único de ficha para matrícula nueva y renovación, con anexos opcionales
- [x] [Colegio] Certificado de alumno regular: **formato aprobado 26-11-2026** (ya revisado). Secretaría emite y Dirección firma. Único punto abierto: el dato identificatorio del colegio en el membrete debe verse más. — **Implementado en código y validado por el colegio:** `CertificadosController` lo emite desde los datos confirmados, conserva historial y expone validación pública por folio.
- [x] [Colegio] Quién captura los datos — **Confirmado por Johan 17-11-2026:** formalmente alguien del colegio va preguntando al apoderado y registra; antes el apoderado lo escribía a mano. La captura formal no la hace el apoderado directamente en el portal. Quién formaliza quedó confirmado el 26-11-2026: **Secretaría**. Sigue abierto el registro público propuesto (ver decisiones pendientes).
- [ ] [Colegio] [BLOQUEADO] Si se actualiza un dato después de firmada la ficha: ¿se vuelve a firmar siempre o solo en cambios importantes?
- [ ] [Colegio] [BLOQUEADO] Alcance de la información visible por perfil — **Pendiente de precisar:** (1) si PIE y CONVIVENCIA ven todo el expediente o solo lo necesario para su función (salud/judicial incluidos o no); (2) si UTP/DIRECCIÓN/INSPECTORÍA ven también antecedentes médicos y judiciales al "ver todo". El punto de quién crea y formaliza quedó cerrado el 26-11-2026 (**Secretaría**); la matriz del área de matrícula ya coincide con lo confirmado.
- [x] [Colegio] Decisión: quién formaliza matrículas — **Confirmado 26-11-2026: SECRETARÍA** crea y maneja casi todo lo de la matrícula (leer, crear, editar, exportar). Dirección lee, valida y exporta; Inspectoría lee y edita; PIE lee; UTP lee y exporta; Convivencia, TENS y Docente leen. Coincide con la matriz `GENERAL` ya cargada: no hay cambio de código, sólo cargarla en la base del colegio.
- [ ] [Colegio] [BLOQUEADO] Decisión: alcance del registro público — **Pendiente y separado de la captura actual. Nuevo requisito de Johan 17-11-2026:** "debería haber una pestaña pública que sea activada o desactivada por el encargado TI para registrar a los alumnos". Sin definir: si es preinscripción del apoderado que luego un funcionario revisa y formaliza, o captura asistida; qué crea el envío (¿matrícula BORRADOR, solicitud pendiente, otro estado?); validaciones, anti-spam/abuso, auditoría y protección de datos; quién la revisa y con qué permisos. La sec. 12 mantiene "Acceso público o login de apoderados" fuera de alcance hasta aprobarlo. **No se habilita acceso público, no se cambia el login ni se toca `Program.cs`/autorización hasta aprobarlo.**
- [x] [Colegio] Autorización de paracetamol — **Confirmado 26-11-2026:** está en la ficha de matrícula; el apoderado marca ahí si autoriza paracetamol. No es un documento aparte ni falta otra versión de la ficha. Ya implementado en el paso 6 del asistente (`AutorizaParacetamol`, Sí/No) y en el bloque impreso de la ficha.

## 1. Coordinación del equipo (reunión del martes)

- [ ] [Equipo] [BLOQUEADO] ¿Qué archivo o formato pide el ministerio, y quién lo sube? (hoy el documento solo permite exportar reportes)
- [x] [Equipo] Inicio de sesión único de funcionarios (sec. 4) — **Comprobado (16-11-2026):** emisor OpenIddict propio (`Seguridad/EmisorSso.cs`, `AutorizacionController`, `/connect/authorize` y `/connect/token`, scope `mpaz_api`), migración `AgregarSso` en el esquema `mpaz_auth`, módulo `database/modulos/auth` y prototipo de prueba `scripts/probar-sso.py`. Falta acordar con el equipo qué módulos lo consumirán.
- [ ] [Equipo] [BLOQUEADO] Acordar con Samantha el identificador común del estudiante (RUN o IPE)
- [ ] [Equipo] [BLOQUEADO] Acordar qué datos mínimos recibe Asistencia (identificador, curso, condición de matrícula)
- [ ] [Equipo] [BLOQUEADO] Acordar con Sebastián el enlace desde la página del colegio hacia el portal de matrícula

## 2. Preparación del proyecto

- [x] [José] Quitar el login de apoderados y el portal de apoderados, conservando ese trabajo en una rama — evidencia: rama `feature/login-apoderado`; `main` solo contiene `CuentaController` para funcionarios.
- [x] [José] Dejar un solo login, para funcionarios — evidencia: `MarcelaPaz.Web/Controllers/CuentaController.cs`, vista `Views/Cuenta/Login.cshtml` y HTTP 200 en `/Cuenta/Login`.
- [x] [José] Alinear entidades y migraciones al DDL (curso y año en `matricula_anual`; familia en `estudiante_persona`) — **Resuelto:** `MatriculaDbContext` mapea `matricula_anual` y `estudiante_persona`; las tablas `public` dependen del DDL. La autorización de paracetamol **se conserva a propósito** en `FichaForm`, `Paso6`, `FichaController` y `FichaImpresion`: el colegio confirmó el 26-11-2026 que vive en la ficha de matrícula y la responde el apoderado (paso 6, Sí/No).
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
- [x] [José] **MAT-13** Registrar en `registros_acceso` lecturas, búsquedas, descargas, exportaciones y accesos denegados de datos sensibles — **Comprobado:** se registran `LEER` del expediente, `BUSCAR` (con hash del término), `DESCARGAR` y `DENEGADO`; las exportaciones de reportes quedan como `DESCARGAR`/`REPORTE` en `ReportesController`.
- [ ] [José] TLS en tránsito; archivos y respaldos protegidos; mínimos privilegios — configurado parcialmente para cookies/HSTS en producción, pero no desplegado ni comprobado.
- [ ] [José] [BLOQUEADO] Cargar la matriz de permisos en `rol_permiso` cuando el colegio la apruebe — existe propuesta no aprobada en `database/03-permisos-iniciales.sql`.

## 4. Etapa I — Captura y PDF

- [x] [José] **MAT-01** Crear matrícula nueva y renovación por año escolar — **Comprobado:** `Buscar`, `Nueva` y `Renovar` distinguen ambos flujos, la matrícula registra `creada_por` y la ficha del alumno ofrece «Iniciar matrícula {año}» cuando falta el expediente. El cierre congela la versión, obtiene folio y deja la matrícula en `FICHA_GENERADA`; probado sobre una matrícula de renovación.
- [x] [José] Formulario en pasos, con guardado de borrador y mensajes de error claros — **Comprobado 25-11-2026:** wizard con borrador en TempData (`ClaveBorrador`), guardado progresivo por pasos en PostgreSQL vía `RegistroFicha`, validación por paso (`ValidadorFicha` + `DocumentoAdultoAttribute`/`IdentificadorAttribute`) y 213 pruebas automatizadas. Mensajes verificados en formularios RUN/IPE (estudiante) y RUN/IPA (adultos).
- [x] [José] Bloque A: estudiante (nombres, apellidos, identificador RUN/IPE, nacimiento, nacionalidad, domicilio, curso) — **Comprobado 25-11-2026:** regla explícita estudiante RUN o IPE; el IPA se rechaza porque identifica al apoderado (incluye rechazo de prefijo IPA en IPE). Pruebas: `FichaEtapa1Tests` + `DocumentoAdultoTests` (213 casos totales).
- [x] [José] **MAT-02** Validar identificador y evitar estudiantes duplicados — **Comprobado 25-11-2026:** RUN con módulo 11, IPE provisorio 3-20 sin espacios (rechaza prefijo IPA); adultos RUN o IPA con rechazo explícito de IPE (formato genérico, sin rangos inventados). Normalización diferenciada (RUN limpio, provisorios en mayúsculas) y restricción única del DDL.
- [x] [José] Bloque B: madre, padre, apoderado principal y secundario (teléfono, correo, domicilio, escolaridad, restricción judicial sí/no) — **Comprobado 25-11-2026:** pasos 2 y 3 con `DocumentoAdultoAttribute` (RUN o IPA, rechazo de IPE), restricción de madre y de padre con tipo Judicial/Familiar y detalle, validación final en `ValidadorFicha` y persistencia con `TipoIdentificador` RUN/IPA. Formularios rotulados RUN o IPA.
- [x] [José] Bloque C: contactos de emergencia, personas autorizadas a retirar, salida solo — **Comprobado 25-11-2026 en código y pruebas:** pasos 4 y 5 con retiro RUN o IPA (rechazo de IPE), salida solo obligatoria y al menos un autorizado si no sale solo; persistencia con `TipoIdentificador` RUN/IPA. Pendiente: prueba de aceptación con usuarios del colegio.
- [x] [José] Bloque D: salud (situaciones médicas, alergias, necesidades de atención, contacto sanitario) — **Comprobado:** el paso 6 registra situaciones, alergias, contacto sanitario y salida al baño, y la ficha del alumno los muestra.
- [x] [José] Bloque E: PIE y apoyos (participa sí/no/pendiente, apoyos, existencia de diagnóstico) — **Comprobado:** se captura en el paso 6 y se persiste en las tablas del DDL.
- [x] [José] Bloques F y G: autorizaciones de imagen (por canal) y de convivencia, como checkbox con su texto — **Comprobado:** se capturan en el paso 7, separadas de salud y PIE, y se guardan como autorizaciones del estudiante. Los textos legales definitivos siguen pendientes de Dirección.
- [x] [José] **MAT-03** Registrar datos familiares, emergencia, retiro, salud y apoyos — **Comprobado:** el asistente persiste los cinco bloques y la ficha del alumno muestra familia, emergencia, retiro, salud, alergias, contacto sanitario y PIE/apoyos. Los apoyos exigen `PIE:LEER`, igual que el resto del área.
- [x] [José] **MAT-04** Registrar el estado de cada autorización (otorgada, rechazada, revocada, vencida) — **Comprobado:** la ficha del alumno lista todas las autorizaciones con estado, firma, vigencia y alcances; la revocación exige motivo y registra funcionario y fecha, y las otorgadas cuyo plazo terminó pasan a `VENCIDA`. Revocación probada en ejecución con auditoría.
- [x] [José] Estados de la matrícula: BORRADOR → FICHA_GENERADA → FIRMADA → CONFIRMADA (o ANULADA) — **Comprobado:** las cuatro transiciones están implementadas y verificadas (cierre de ficha, registro de firma, confirmación con funcionario y fecha y anulación con motivo), la anulación exige motivo y permite iniciar una matrícula nueva, y la base impide saltos. Cubierto por `AnulacionMatriculaTests`.
- [x] [José] Vista previa de la ficha antes de cerrarla — el paso 8 muestra resumen ordenado (pasos 1-7) + PDF embebido + confirmación expresa; al guardar genera folio y congela la copia, y el comprobante posterior imprime desde el snapshot.
- [x] [José] **MAT-08** Generar el PDF desde la copia congelada (`datos_snapshot`), con folio, año, versión, textos aceptados y bloques de firma — **Comprobado:** el cierre crea `ficha_version` con folio, versión y snapshot; el PDF usa la versión cerrada y el borrador se descarga como «Sin folio».
- [x] [José] Botón para descargar e imprimir la ficha (registrar la descarga) — **Comprobado:** la descarga queda registrada como `DESCARGAR` en `registros_acceso`.
- [x] [José] Accesibilidad básica del formulario — **Comprobado:** auditorías con axe/Lighthouse, teclado, NVDA y revisión visual responsive documentadas en `auditorias/`. El reflow a 200 % (≈640 px), `prefers-contrast: more` y `forced-colors: active` quedaron implementados y comprobados sin desborde horizontal.

## 5. Etapa II — Expediente y consulta

- [x] [José] **MAT-06** Permitir subir los archivos del expediente — **Comprobado:** carga desde la ficha del alumno, con límite de 15 MB, formatos PDF/JPG/PNG, hash y almacenamiento fuera de `wwwroot`. Los enlaces oficiales de la sec. 12 siguen pendientes.
- [x] [José] **MAT-07** Marcar cada documento como recibido, no aplica o pendiente, sin bloquear la matrícula — **Comprobado:** estados editables desde la ficha; ninguno bloquea la matrícula.
- [x] [José] Subir archivos PDF/JPG/PNG con límite de tamaño, guardados fuera de directorios públicos, con hash y análisis del archivo — **Comprobado:** validación de extensión, tamaño y firma real del archivo; hash SHA-256 y ruta relativa en `App_Data/documentos`. El análisis antivirus queda fuera de alcance.
- [x] [José] **MAT-09** Subir el escaneo de la ficha firmada y asociarlo a la matrícula — **Comprobado:** el escaneo se guarda como documento `FICHA_FIRMADA`, reemplaza al anterior sin borrarlo y la ficha muestra el enlace de descarga.
- [x] [José] **MAT-10** Registrar la ubicación del original en papel y el estado de validación documental — **Comprobado:** `AlumnosController.ValidarDocumentacion` (permiso `DOCUMENTOS:VALIDAR`) guarda `validado_por`, `validado_en`, la observación y la ubicación del original; `RegistroFicha.ValidarDocumentacionAsync` valida PENDIENTE/VALIDADA/OBSERVADA y exige firma y escaneo para VALIDADA; la ficha del alumno muestra ubicación, estado y observación.
- [x] [José] Confirmar la matrícula según el procedimiento oficial (registrar quién y cuándo) — **Comprobado:** sólo desde ficha generada o firmada, con funcionario y fecha en la matrícula y en la auditoría.
- [x] [José] **MAT-11** Consulta administrativa: búsqueda global y filtros por RUN/identificador, nombre, curso, año, estado y pendientes — **Comprobado:** el listado busca por nombre y por RUN o IPE, y agrega el filtro y la columna «Pendientes» (sin ficha vigente o con requisitos documentales pendientes) conservando año, curso, estado y orden.
- [x] [José] Vista previa del PDF firmado; descarga solo si el rol lo permite — **Comprobado:** descarga del escaneo con `DOCUMENTOS:LEER` y denegación verificada para Docente.
- [x] [José] Pestañas con acceso condicionado: salud, PIE, documentos judiciales — la ficha tiene pestañas General/Autorizaciones/Documentos/Salud/PIE; cada panel muestra solo lo del rol y explica la ausencia (S-02).
- [x] [José] Mostrar última actualización y funcionario responsable — la ficha del alumno muestra fecha (trigger de auditoría, respaldo `updated_at`) y funcionario (historial, respaldo `creada_por`).
- [x] [José] Acceso de lectura concurrente; control de versión para evitar sobrescrituras al editar — **Comprobado:** la columna `version` está mapeada como token de concurrencia en `MatriculaDbContext` (`IsConcurrencyToken`), de modo que EF lanza `DbUpdateConcurrencyException` si otro funcionario guardó primero, en vez de sobrescribir en silencio.
- [ ] [José] [BLOQUEADO] Medicación por TENS: prescripción, autorización y registro de dosis (**MAT-05**) — lectura y registro conectados en código (`Prescripcion`, `RegistrarDosis`, trigger que exige TENS activo + orden VIGENTE validada + autorización otorgada), formulario corregido el 25-11-2026 (`679554d`: se oculta sin prescripciones VIGENTES) y flag habilitado por orden expresa (`Medicacion:Habilitada=true`); desplegado en el servidor (publicación `20261125T061732Z`, HTTP 302, Docker verificado). Falta: protocolo escrito del colegio y pantalla de ingreso/validación de prescripciones (hoy solo se crean directo en base).

## 6. Etapa III — Continuidad

- [x] [José] **MAT-01** Renovación anual: precargar datos vigentes, exigir confirmación y generar la ficha del año nuevo — **Comprobado:** `Renovar` crea el expediente del año como `RENOVACION` y precarga estudiante, familia, apoderados, contactos, retiro, salud, PIE y autorizaciones; el paso 7 exige la confirmación y el cierre genera la ficha con folio. El flujo de cierre se probó sobre una matrícula de renovación.
- [x] [José] Egreso de 8º básico: al renovar a quien terminó el último nivel del colegio se pide confirmar — **Comprobado:** `Renovar` abre una pantalla de confirmación con dos caminos: «Egresado, sin renovar» (no crea expediente) o «Repite 8º básico» (la ficha queda marcada como repetición). La ficha del alumno muestra la insignia «Egresado de 8º básico», derivada de la última matrícula no anulada, sin columna nueva. Cubierto por `NivelesCursoTests.EsUltimo`.
- [x] [José] **MAT-14** Versiones históricas de la ficha, autorizaciones y cambios de apoderado — **Comprobado:** la ficha del alumno lista todas las versiones con folio, estado, fecha, responsable y motivo, y cada una se descarga desde su copia congelada; las autorizaciones conservan su historial y los vínculos familiares cerrados se muestran con su fecha de término.
- [x] [José] **MAT-18** Corrección controlada: crear una versión nueva con motivo, sin tocar el documento firmado — **Comprobado:** «Corregir ficha» parte de la copia congelada, exige motivo en el paso 7 y genera la versión siguiente; la anterior queda `REEMPLAZADA`, la matrícula conserva su estado y la ficha original no se edita (trigger del DDL). Probado: versión 2 con motivo sobre una ficha vigente.
- [x] [José] Revocación de autorizaciones (imagen, convivencia): conservar firma y fecha, bloquear el uso posterior — **Comprobado:** la acción de revocación conserva `fecha_firma` y firmante, guarda motivo, funcionario y fecha de revocación, y deja la autorización fuera de las vigentes.
- [x] [José] **MAT-16** Exportar reportes autorizados: estado de matrículas y faltantes documentales — `ReportesController` con vista y CSV (descarga auditada, permiso EXPORTAR distinto de LEER).
- [x] [José] **MAT-17** Alertas de órdenes médicas y autorizaciones vencidas o por vencer — **Implementado el 16-11-2026.** Pantalla `/Alertas` con listado por alumno, curso, tipo, detalle, vencimiento y estado, enlace a la ficha y permiso por área; la vista incluye también las `VENCIDA` y las que vencen en 30 días. El requisito de origen sólo pide «emitir alertas por órdenes médicas o autorizaciones vencidas»; no fija canal ni antelación.
    - **Existe:** la vista `v_alertas_vencimiento` del DDL (unión de `prescripcion` con estado `VIGENTE`/`PENDIENTE_VALIDACION` y de `autorizacion` `OTORGADA` o `VENCIDA` no revocada, con `vigente_hasta <= hoy + 30` y bandera `vencida`); el panel de inicio cuenta las vencidas en `PanelIndicadores.ContarPanelAsync` y las muestra como «Documentos y autorizaciones vencidas»; `Autorizaciones.EstadoEfectivo` y `RegistroFicha.MarcarVencidasAsync` pasan a `VENCIDA` las autorizaciones cuyo plazo terminó.
    - **Listado — resuelto:** `AlertasController` y `/Alertas` muestran alumno, identificador, curso, tipo, detalle, vencimiento y estado, con enlace a la ficha; el bloque de autorizaciones exige `AUTORIZACIONES:LEER` y el de órdenes médicas `MEDICACION:LEER`, y la lectura queda en `registros_acceso` (MAT-13).
    - **Aviso previo — resuelto en pantalla:** `/Alertas` muestra también las que vencen en los próximos 30 días, separadas como «por vencer». El contador del panel sigue mostrando sólo las vencidas.
    - **Rama de órdenes médicas — conectada en lectura (25-11-2026):** existe la entidad `Prescripcion` (`Domain/Entidades/Medicacion.cs`, `DbSet` en `MatriculaDbContext`) y `AlertasController` consulta la rama `PRESCRIPCION` de la vista con permiso `MEDICACION:LEER`. Falta la pantalla de ingreso/validación de prescripciones y el proceso programado; el panel se calcula al abrir la página (caché de 15 s).
    - **Falta proceso programado:** el panel se calcula al abrir la página (caché de 15 s). No hay tarea, timer de systemd ni correo o reporte periódico; `deploy/mpaz-matriculas.service` no programa nada.
    - **Alcance por rol — resuelto (17-11-2026):** el panel sólo cuenta vencidas de las áreas que la sesión puede leer, por el año consultado; si no tiene ninguna de las dos áreas, no muestra el indicador. La pantalla de alertas ya filtraba por área y ahora también por año escolar.
    - **Pruebas:** `AlertasPaginaTests` cubre `Vencidas`/`PorVencer` y el estado relativo de `Plazo`; no hay cobertura automatizada de las consultas SQL contra PostgreSQL.
    - **Riesgo corregido y acotado (16-11-2026):** la vista acepta `estado IN ('OTORGADA', 'VENCIDA')`, excluye `revocada_en`, no lista una autorización reemplazada (comparación por fecha con respaldo en `id`, así una fila sin `vigente_desde` ni `fecha_firma` también se excluye) y deja de listar vencimientos de más de 90 días. La regla de reemplazo se limita a imagen y convivencia: la medicación admite una autorización por medicamento. Cambio en `pasos/01-ddl-institucional.sql`, `instalacion/01-estructura-bd.sql`, `docker/db-init/01-ddl-institucional.sql` y el paso incremental `pasos/12-alertas-vencimiento.sql`; **aplicado en la base de desarrollo el 16-11-2026, falta la base del servidor**.
    - **Detalle menor corregido (17-11-2026):** la etiqueta del panel ahora dice «Autorizaciones y órdenes médicas vencidas», que es lo que cuenta la vista; los faltantes documentales son otro indicador.
- [x] [José] Auditoría de cuentas: agregar `funcionario` a los triggers de auditoría — **Comprobado:** `pasos/09-auditar-funcionario.sql` agrega el trigger de auditoría a `funcionario` (nombre, correo y estado de la cuenta dejan rastro en `historial_cambios`); es aditivo e idempotente sobre una base instalada.

## 7. Pruebas de aceptación (sec. 13)

- [ ] Ficha completa: genera el PDF imprimible con todos los datos y las firmas, sin transcribir a mano — existe generador parcial, pero no se ejecutó la aceptación completa ni usa snapshot cerrado.
- [x] Falta un documento complementario: queda como pendiente y no bloquea la matrícula — **Comprobado:** el estado documental es independiente del estado de la matrícula.
- [ ] Dos funcionarios consultan al mismo estudiante: ven la misma versión, sin esperar una carpeta física — no probado.
- [x] El apoderado revoca la imagen: el estado queda actualizado, con la versión histórica y la fecha, y se bloquea la difusión — **Comprobado:** la revocación queda como `REVOCADA` con funcionario, fecha y motivo; la firma original se conserva y la autorización deja de contar como vigente.
- [x] Un docente sin permiso pide un diagnóstico: acceso denegado en pantalla y servidor, con evento de auditoría — **Comprobado** con un Docente real sobre documentos y áreas sensibles.
- [ ] Un TENS registra una dosis: exige autorización y orden vigentes, y guarda responsable, fecha, hora y dosis — código y trigger verificados y flag habilitado el 25-11-2026; pendiente el protocolo del colegio, la pantalla de prescripciones y la prueba con una orden válida.
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
- [x] [José] Túnel y hostname `matricula.empaz.cl` vivos y sirviendo el portal — **Verificado 23-11-2026:** DNS a Cloudflare, `https://matricula.empaz.cl/` 200 → `/Cuenta/Login`, login con antiforgery, `Cache-Control: no-cache, no-store`, CSP/HSTS. Falta: app de Access (hoy entra directo al login) y confirmar `Seguridad__ExigirInicioSesion=true` en el servicio. Detalle en `docs/respuesta-ti-cloudflare.md`.
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

> Este resumen corresponde al 06-11-2026. El estado vigente está en las secciones 11.1, 13 y 14.

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

1. ~~Activar y probar la seguridad obligatoria~~ — cerrado: `ExigirInicioSesion`, cookie `Secure`, antiforgery global y cambio forzado de contraseña.
2. Paracetamol: confirmar con Dirección si se mantiene la pregunta con indicación médica o se retira del formulario y del PDF.
3. ~~Envolver todas las escrituras de matrícula con `app.funcionario_id`~~ — cerrado con `EscrituraAuditada`.
4. ~~Crear pruebas para RUN, duplicados, permisos, auditoría, transiciones y concurrencia~~ — batería en `mpaz-matricula/tests` (no re-ejecutada en esta revisión).
5. ~~Implementar el cierre de ficha mediante `ficha_version.datos_snapshot`~~ — cerrado: folio, versión congelada y PDF desde la copia.
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
| MAT-11 y asistente de 8 pasos | Búsqueda por nombre y por RUN/IPE, filtro y columna de pendientes; salud/PIE y autorizaciones separados en pasos 6 y 7, con revisión en el paso 8 |
| Pruebas | 39 casos, incluidos formatos y firma real de archivos, RUN/IPE, cierre de ficha y estados de autorización |
| Prueba de humo | Sobre la matrícula 4: corrección generó la versión 2 con motivo y dejó la 1 como `REEMPLAZADA`; la revocación de la autorización 9 guardó funcionario, fecha y motivo. Datos de prueba y permisos temporales revertidos |
| Commits | `dcb30fe`, `6d1a347` y `99013b8` en `main`; estos avances quedan sin commit |

### Pendiente que sigue abierto

- Paracetamol: no se retiró; la ficha lo pregunta con el texto «únicamente con indicación médica y protocolo TENS aprobado» (`Paso6.cshtml`, `FichaImpresion`, `FichaEtapa1Tests`). Confirmar con Dirección si ésa es la decisión definitiva.
- Anulación de matrícula — **cerrado:** `AlumnosController.Anular` y `RegistroFicha.AnularAsync` (exige motivo y permite iniciar una matrícula nueva), cubierto por `AnulacionMatriculaTests`.
- Reportes y exportación (MAT-16) — **cerrado** (`ReportesController` y `ReportesCsvTests`); control de concurrencia al editar — **cerrado** (`version` como token de concurrencia en `MatriculaDbContext`). Sigue abierto MAT-17 (alertas), con el detalle de la sección 6.
- Construir y probar la imagen Docker, probar respaldo/restauración y rotar las credenciales del servidor.
- Publicar con dominio propio y Cloudflare Access.
- Textos legales, matriz de permisos aprobada, retención de datos y revisión de la Ley 21.719 (bloqueados por el colegio).
- Revisión visual en un equipo con Windows en alto contraste y a 200 % de zoom, más allá de la comprobación automatizada de desborde.

---

## 14. Avances del 09 al 16-11-2026

### Cerrado y verificado en el repositorio

| Cambio | Evidencia |
| --- | --- |
| Anulación de matrícula (antes sin acción) | `AlumnosController.Anular` y `RegistroFicha.AnularAsync`, con motivo obligatorio y `AnulacionMatriculaTests` |
| MAT-16 reportes y exportación | `ReportesController` con vista y CSV de matrículas y faltantes, permisos `GENERAL:EXPORTAR` y `DOCUMENTOS:EXPORTAR`, descarga auditada; `ReportesCsvTests` |
| Control de concurrencia al editar | columna `version` como token de concurrencia en `MatriculaDbContext` |
| Inicio de sesión único de funcionarios | emisor OpenIddict (`Seguridad/EmisorSso.cs`, `AutorizacionController`), migración `AgregarSso` en `mpaz_auth`, módulo `database/modulos/auth` y `scripts/probar-sso.py` |
| Certificado de alumno regular | `CertificadosController` con emisión, historial de emisiones y validación pública por folio; licencia QuestPDF configurada |
| Egreso de 8º básico y niveles completos | confirmación egreso/repite en `Renovar`, insignia de egresado y cursos de pre-kínder a 8º básico con letras A/B |
| Base por módulos en Neon | esquemas `mpaz_matricula`, `mpaz_auth` y `mpaz_asistencia`, conexión con respaldo y `database/CONEXION.md` |
| Video demostrativo | `pruebas-visuales/evidencias/grabar-flujo/flujo-mpaz.webm` y 15 capturas con CSV de evidencia |

### Pendiente que sigue abierto

- MAT-17: aviso automático/proceso programado y pruebas contra la base real de la vista y el contador; el listado, el año escolar y las «por vencer» ya están en `/Alertas`, con el panel acotado por área y año.
- Base de desarrollo: migración `AgregarSso` de OpenIddict aplicada el 16-11-2026; el emisor arranca sin error y `/connect/authorize` responde. La base del servidor sigue sin esas tablas.
- MAT-15: integración con Asistencia; el esquema `mpaz_asistencia` existe, sin flujo de datos.
- Matriz de permisos aprobada, protocolo de medicación, textos legales y Ley 21.719 (vigente desde el 01-12-2026): bloqueados por el colegio.
- Respaldo y restauración probados, rotación de credenciales, dominio propio con Cloudflare Access e imagen Docker sin construir.

## 15. Avances del 17-11-2026

### Cerrado y verificado en el repositorio

| Cambio | Evidencia |
| --- | --- |
| MAT-17: filtro y alcance por año escolar | `AlertasController` resuelve el año activo o seleccionado y filtra la matrícula de ese año; `PanelIndicadores` agrupa el conteo por año y área. |
| MAT-17: panel por permisos | `HomeController` pasa `AUTORIZACIONES:LEER` y `MEDICACION:LEER`; el contador queda en 0 y oculto sin alguna de las áreas. |
| MAT-17: etiqueta del panel corregida | «Autorizaciones y órdenes médicas vencidas», con enlace a `/Alertas`. |
| Pruebas MAT-17 | `AlertasPaginaTests`: conteos de vencidas/por vencer y texto relativo de `Plazo`. |
| Compilación Release y pruebas | 0 errores y 0 advertencias; 133 pruebas superadas, 0 fallos. |

### Pendiente que sigue abierto

- MAT-17: proceso programado/aviso automático, pruebas contra la base real de la vista y el contador, y aplicación en la base del servidor.
- Respaldo y restauración probados, rotación de credenciales, dominio propio con Cloudflare Access e imagen Docker sin construir.

## 16. Revisión actualizada y lista vigente (17-11-2026)

> **Actualización 26-11-2026:** esta sección conserva el estado al 17-11-2026 como registro histórico. Lo que el colegio confirmó después (quién formaliza la matrícula, paracetamol, formato del certificado, documentos obligatorios y conservación de datos) está aplicado en §0, en `docs/decisiones-pendientes-colegio.md` y en `docs/ficha-pendientes-portal-mpaz.md`, y ya no se repite como pendiente aquí.

### Estado real hoy

El portal local compila en Release sin errores ni advertencias y las 137 pruebas pasan (al 26-11-2026 la suite es de 213 pruebas, todas en verde). `/Alertas` filtra por año (activo o seleccionado) y por área (autorizaciones/medicación); el panel de inicio sólo cuenta alertas de las áreas permitidas, por año, y oculta el indicador si no tiene ninguna. Quedan cerrados los cinco hallazgos de auditoría y los dos mínimos de datos, más la guarda de privilegios de auditoría.

> **Superadmin solo en prueba (17-11-2026):** rol `SUPERADMIN` (66 permisos: todas las áreas × todas las acciones) y funcionario `superadmin.test@mpaz.cl` existen **únicamente en la base local de prueba** (`mpaz_restore_local`), nunca en Neon ni en producción. No están en los pasos del DDL: es siembra puntual de desarrollo. Si la base de prueba se re-restaura desde Neon, el superadmin desaparece y hay que volver a sembrarlos con el procedimiento de esta revisión.

### Lo que falta y puede hacerse sin autorización externa

- [x] MAT-17: aviso automático/proceso programado y pruebas reales (17-11-2026). Canal in-app en `/Alertas`; frecuencia cada 15 min + primera revisión al arrancar; responsable: proceso programado, sin funcionario asignado. Vista y contador probados contra la base real de desarrollo: años 2026 (9 matrículas) y 2027 (27); prueba positiva con una autorización temporal vencida (vista = 1, contador del panel 2027 = 1, revisión programada con “1 autorizaciones”, `/Alertas` en HTTP con el resumen), datos de prueba limpiados y contador de vuelta a 0. Permisos: `mpaz_app` recibe solo `SELECT, INSERT` sobre `alerta_revision` en `pasos/13` e `instalacion/02`, verificado con rol simulado (lee e inserta, no actualiza ni borra).
- [x] Aceptación funcional parcial (17-11-2026): PDF desde `datos_snapshot` descargado por HTTP (`/Ficha/Version/43` y `/44`, 200 `application/pdf`, `%PDF`); concurrencia con bloqueo optimista verificada en base real (guardado con versión actual sí, con versión antigua 0 filas); renovación al año siguiente probada por HTTP con 2028 activado temporalmente (`POST /Ficha/Renovar` → paso 1 con año 2028). Validación local bloques B/C 23-11-2026: unit 38/38 + HTTP en local (`dotnet run` Desarrollo + docker db, usuario `prueba.wizard@mpaz.cl` SECRETARIA): GET pasos 2-5 200, POST válido 302 al siguiente (2→3→4→5→6), POST inválido 200 con mensaje (madre/padre, teléfono repetido, falta autorizado). Nota: la terminal local deforma tildes por encoding — en HTTP se usó `Postgrado`; los acentos están cubiertos por los unit tests. Falta: validación final contra el papel del colegio.
- [x] Docker verificado en este equipo (23-11-2026): Engine 29.9.3 + compose v5.7.0; base `docker-db-1` levantada con seed mock (7 funcionarios) para las pruebas del wizard. `.env` local con claves generadas (gitignored).
- [ ] Construir y probar la imagen Docker y el arranque completo en una máquina local/API. No se hizo: el compose `app` no se construyó en esta pasada (se probó con `dotnet run` + db en docker).
- [x] Ficha de salud del funcionario (23-11-2026; UI y PDF 24-11-2026): entidad `FichaSaludFuncionario`, `DbSet` + mapeo, `pasos/25-ficha-salud-funcionario.sql` (tabla + índice + permisos SALUD UTP/Dirección) aplicado al docker local y a Neon, `pasos/26-auditar-ficha-salud-funcionario.sql` (trigger de `historial_cambios`) aplicado a Neon, `ValidadorFichaSalud` y 11 tests de la ficha. UI en `personal/fichas-medicas` (UTP/Dirección): formulario completo, listado, vista imprimible y PDF generado con QuestPDF (`FichaSaludFuncionarioPdf`). Ya no se suben archivos: la ficha se completa en el portal. Verificado por HTTP contra Neon con `prueba.utp@mpaz.cl` (crear, editar, ver, PDF 200 `application/pdf`, rechazo por campos incompletos) y el trigger auditó INSERT/UPDATE con el funcionario responsable; ficha de prueba eliminada. Falta: validar los campos contra el papel definitivo del colegio.
- [x] Prueba de respaldo y restauración en una copia local (17-11-2026). Respaldo `artifacts/respaldo/mpaz-20261117T023610Z.dump` (354 KB, 629 objetos, incluye `alerta_revision`). Restauración completa en PostgreSQL 18 local (`mpaz_restore_local`): 41 tablas en `mpaz_matricula`, 4 en `mpaz_auth`, 36 matrículas, 44 fichas y 1 revisión; para llegar, `pg_restore` directo no bastó por sentencias `pg_attver` que el PostgreSQL local no reconoce, y se restauró vía SQL plano sin esas líneas.
- [x] Paquete de despliegue limpio (17-11-2026): `scripts/publicar-paquete.sh` pública en Release y genera `artifacts/deploy/mpaz-matriculas-20261117T025827Z-linux-x64.tar.gz` (46 MB) con SHA-256; verificado sin `appsettings.Development.json`, sin PDB y sin `App_Data`.

### Lo que falta y sí requiere autorización o decisión externa

- [ ] Aplicar en el servidor: `12-alertas-vencimiento.sql`, `13-alertas-revision.sql`, migración `AgregarSso`, guarda de `04-privilegios-aplicacion.sql`, fuentes PDF (`fontconfig`/`fonts-dejavu-core`) y el release actual.
- [ ] Respaldos programados y copia externa; rotación de credenciales visibles; dominio propio con Cloudflare Access.
- [ ] Decisiones del colegio que siguen abiertas (**actualizado 26-11-2026**): textos legales de las autorizaciones de imagen y convivencia, protocolo escrito de medicación TENS (MAT-05), situación familiar con o sin adjuntos, campos de las 7 secciones de la ficha médica del personal, Ley 21.719 (aviso de privacidad y derechos) y el dato identificatorio del colegio en el membrete del certificado. **Ya cerrados el 26-11-2026 y por eso fuera de esta lista:** quién formaliza la matrícula (Secretaría), autorización de paracetamol (va en la ficha, la responde el apoderado), formato del certificado de alumno regular (aprobado), documentos obligatorios (se definen caso a caso) y conservación de datos (por ley se conservan). **La matriz de permisos ya está confirmada**; falta sólo cargarla en la base del colegio.
- [ ] Coordinación del equipo: formato ministerial y quién lo sube, identificador común con Asistencia/Notas/Apoyo (MAT-15), enlace desde la página del colegio y consumo del SSO por otros módulos.

### Orden sugerido

1. Construir y probar la imagen Docker del servicio `app` (Docker ya está instalado y verificado desde el 23-11-2026).
2. Aceptación funcional final: pasos 2-5 del wizard (bloques B y C) y validación final del formulario.
3. Con autorización: despliegue al servidor, credenciales y Cloudflare Access.

---

## 17. Importación de datos de alumnos (ordenamiento del subrogante) — 23-11-2026

- Contexto: el subrogante actual del colegio intenta ordenar parte de los datos de los alumnos (no todos). Hay dos fuentes: una base Access desestructurada y una planilla Excel mejor estructurada (posible fuente de verdad, por confirmar).
- Todo el trabajo con datos reales se hace únicamente en `datos-alumnos/` (raíz del proyecto; carpeta completa ignorada por git, nada de lo que haya en ella se sube al repositorio). Los archivos fuente aún no están en el proyecto. Procedimiento y reglas de limpieza en `mpaz-matricula/database/importacion/README.md`.
- Defectos conocidos (sin revisar fila por fila todavía):
  - RUTs incompletos: falta el dígito verificador.
  - Extranjeros: traen número de matrícula en vez de RUN.
  - Nacionalidades mal escritas.
  - Apoderados: datos no revisados.

- [ ] [José] Recibir el Excel y el Access, dejarlos en `datos-alumnos/fuentes/` y anotar fecha y versión recibida en el README de importación.
- [ ] [Colegio] [BLOQUEADO] Confirmar cuál es la fuente de verdad (¿el Excel manda y el Access solo se consulta?).
- [ ] [José] Perfilado de la fuente elegida: columnas, nulos, duplicados de identificador y formatos de fecha/teléfono; informe en `datos-alumnos/informes/`.
- [ ] [José] Normalización de RUT: a los sin DV se les calcula el dígito (módulo 11, igual que `rut_valido()`); los que sigan inválidos van a lista de cuarentena para que el colegio los corrija (el DDL rechaza el `RUN` inválido: `ck_estudiante_run` / `ck_persona_run`).
- [ ] [José] Extranjeros: regla explícita estudiante RUN o IPE y apoderado RUN o IPA. Mapear el número de matrícula del estudiante a IPE y el del apoderado extranjero a IPA; `OTRO` solo si el colegio lo admite formalmente. El DDL conserva RUN/IPE/IPA/OTRO; el asistente admite RUN/IPE en el estudiante y RUN/IPA en madre, padre, apoderados y retiros (IPE rechazado en adultos, IPA rechazado en estudiantes).
- [ ] [José] Normalizar nacionalidad contra lista controlada; registrar el mapeo de variantes mal escritas en el README de importación; lo no mapeable va a cuarentena.
- [ ] [José] Validar apoderados sin revisar (RUN o IPA con rechazo de IPE, teléfono, formato de correo que exige el DDL) y vínculos madre/padre/principal/secundario para `estudiante_persona` (unicidades de vigente); duplicados y vínculos ambiguos a cuarentena.
- [ ] [Colegio] [BLOQUEADO] Quién corrige la lista de cuarentena y en qué plazo; si se importan solo los registros limpios o se espera la corrección.
- [ ] [Colegio] [BLOQUEADO] Alcance de la carga inicial: ¿solo estudiante + familia/apoderados, o también curso y matrícula anual? (si la fuente no trae curso/año, la matrícula queda para el wizard).
- [ ] [José] Carga primero en base de desarrollo, idempotente (re-ejecutable sin duplicar, apoyada en `uk_estudiante_identificador` / `uk_persona_identificador`), con respaldo previo; los 10 estudiantes mock conviven o se retiran según decida el colegio.
- [ ] [José] Protección de datos: todo el trabajo solo en `datos-alumnos/` (ignorado por git completo), borrar o archivar fuera del equipo tras la carga; revisión de la Ley 21.719 antes de operar con datos reales (sec. 0).

---

## 18. Revisión de módulos y simulación de matrículas — 25-11-2026

### Módulos revisados con sesión real (HTTP, base de desarrollo Neon)

| Módulo | Ruta | Secretaría | UTP | Dirección |
| --- | --- | --- | --- | --- |
| Panel | `/` | 200 | 200 | 200 |
| Estudiantes y ficha | `/Alumnos`, `/Alumnos/Detalle/{id}` | 200 | 200 | 200 |
| Buscador de matrícula | `/Ficha/Buscar` | 200 | 200 | 200 |
| Alertas | `/Alertas` | 200 | 200 | 200 |
| Reportes y CSV | `/Reportes/Matriculas`, `/Reportes/Faltantes` | 200 | 200 | 200 |
| Certificados | `/Certificados` | 302 | 302 | 200 |
| Fichas médicas del personal | `/personal/fichas-medicas` | 302 | 200 | 200 |
| Año escolar | `/Configuracion/AnioEscolar` | 200 | 302 | 200 |
| Actividad | `/Home/Actividad` | 200 | 302 | 200 |
| Funcionarios | `/admin/funcionarios` | 302 | 302 | 302 |

Los 302 corresponden a la política de permisos (redirección a Sin permiso); en `admin/funcionarios` es lo esperado porque el único rol con esa área sería un administrador y no existe cuenta con ese rol en la base de desarrollo.

### Simulación del asistente (3 alumnos de cursos distintos)

- Recorrido completo de los 8 pasos con el borrador precargado desde la base: pasos 1 a 5 con datos existentes, paso 6 con «sin situación médica» y PIE «Pendiente», paso 7 con ambas autorizaciones afirmativas.
- Resultado: matrículas en `FICHA_GENERADA`, folios `MP-2027-00001` a `MP-2027-00003`, versión 1 `VIGENTE`, PDF de la ficha (112 KB, `application/pdf`), reporte de matrículas con las 3 filas y CSV con los mismos datos.
- Al terminar se restauró el respaldo previo (`artifacts/respaldo/mpaz-antes-simulacion-*.dump`) y se recargó `import_carga_2026` (255 filas: 92 APTO y 163 OBSERVADO). La base quedó en: 251 estudiantes, 251 matrículas 2027 en BORRADOR, 394 vínculos vigentes, 0 fichas, 0 documentos, 0 autorizaciones y folio 2027 en 0.

### Hallazgos de integración

- **Pasos 2 y 3 no se pueden precargar para todos:** de 251 estudiantes, 226 tienen algún vínculo vigente, 196 madre, 128 padre, 191 apoderado principal y 25 no tienen ningún vínculo. Sin vínculo, el asistente abre los pasos 2 y 3 en blanco.
- **Retiro autorizado:** 186 estudiantes tienen personas autorizadas; los demás abren el paso 5 vacío y no se puede cerrar sin registrar al menos una.
- **Curso:** 2 matrículas 2027 no tienen curso asignado, así que el paso 1 obliga a elegirlo a mano.
- **Salud y PIE:** 0 situaciones médicas y 0 antecedentes de apoyo en la base; el paso 6 siempre pide el contacto sanitario y las respuestas de PIE. Los datos clínicos del Excel (`SMED_SITU`, `SMED1_NOM`, etc.) están en `import_carga_2026` pero no se imputaron a las tablas del expediente.
- **Fichas médicas con datos de mockup:** en la base de desarrollo no hay ninguna: `ficha_salud_funcionario` = 0 y `ficha_medica_funcionario` = 0. Los datos ficticios (asma, ansiedad) viven solo en `docker/db-init/04-datos-gestion-expediente.sql` y en `database/modulos/matricula/datos-prueba/`, que no se cargan en Neon.
- **Cuentas de prueba:** los 9 funcionarios de la base son cuentas de prueba (`prueba.*` y nombres de ejemplo). Antes de entregar hay que reemplazarlas por las cuentas reales del colegio.



### Certificado de alumno regular: quién genera y quién firma (25-11-2026)

- Regla del colegio: **Secretaría genera** el certificado; **Dirección solo firma**. El código tenía al revés la restricción: `PuedeEmitir()` exigía el rol `DIRECCION` y dejaba fuera a Secretaría.
- `CertificadosController` ahora genera con `DOCUMENTOS:CREAR` o `DOCUMENTOS:EDITAR` y el historial se abre con `DOCUMENTOS:LEER`, que es lo que dice la matriz de la hoja de permisos (Inspectoría, PIE, Convivencia y TENS solo leen; Docentes y Administración TI no tienen acceso).
- La firma impresa ya no es la de quien genera: el controlador busca al funcionario activo con rol `DIRECCION` vigente y guarda su nombre como `FirmanteNombre` (cargo «Director»), mientras `EmitidoPor` queda con quien emitió. Si no hay ninguna cuenta con ese rol, usa `Institucion:FirmanteNombre` y, en último caso, «La Dirección».
- El botón «Certificado alumno regular» de la ficha del alumno aparece a quien puede generar y la matrícula está apta (firmada, confirmada o validada).
- Verificado por HTTP: Secretaría ve el botón y obtiene el PDF (`application/pdf`, 103 KB); Dirección y UTP ven el historial pero no el botón y su POST de emisión no genera documento. El certificado de prueba con folio `CAR-2027-000001` se eliminó después de la prueba.

### Matrículas de demostración dejadas en la base (25-11-2026)

- Se registraron **5 matrículas completas** con alumnos que ya tenían familia, apoderado principal, dos contactos de emergencia y personas de retiro: folios `MP-2027-00001` a `MP-2027-00005` (una ficha `VIGENTE` por alumno, PDF de 112 a 114 KB, visibles en la ficha del estudiante). Quedan a propósito para la demostración y **se reemplazarán cuando el colegio entregue los datos definitivos**.
- El paso 6 se completó con «sin situación médica», PIE «Pendiente» y el contacto sanitario copiado del contacto de emergencia; el paso 7 con ambas autorizaciones afirmativas. Son respuestas de prueba: hay que corregirlas con la información real antes de usar estas fichas.
- Respaldo previo a estas matrículas: `artifacts/respaldo/mpaz-antes-matriculas-demo-*.dump`, por si hay que volver al estado en blanco.

### Tareas nuevas

- [ ] [José] Completar los vínculos familiares de los 25 estudiantes sin vínculo y el apoderado principal de los 60 que no lo tienen, para que los pasos 2 y 3 del asistente se abran precargados.
- [ ] [José] Imputar desde `import_carga_2026` las situaciones médicas y los antecedentes PIE de los alumnos que sí los traen (hoy 0 en el expediente), revisando antes las filas OBSERVADO.
- [ ] [José] Asignar curso a las 2 matrículas 2027 que quedaron sin curso.
- [ ] [José] Registrar personas autorizadas de retiro para los 65 estudiantes que no tienen ninguna.
- [ ] [Colegio] Reemplazar las 9 cuentas de prueba por las cuentas reales antes de la entrega y definir quién conserva el rol de administración.
- [ ] [José] Excluir del release el seed mock de Docker y los `datos-prueba/` (ya fuera del paquete por `scripts/publicar-paquete.sh`, falta verificarlo en la imagen Docker).
- [ ] [José] Revisar el RUN de las personas del grupo familiar y de retiro: en la selección de alumnos para la demostración, 26 de 121 traían dígito verificador inválido y el paso 5 no deja cerrar la ficha.
- [ ] [José] Reemplazar o anular las 5 matrículas de demostración cuando estén los datos definitivos del colegio.
- [ ] [José] Dejar automatizado el recorrido completo del asistente (hoy se validó con un script externo sobre el portal en ejecución) para repetirlo antes de cada entrega.

---

## 19. Medicación TENS: corrección, habilitación y despliegue — 25-11-2026

- Formulario de dosis corregido (`MarcelaPaz.Web/Views/Alumnos/Detalle.cshtml`, commit `679554d`): solo se muestra cuando hay al menos una prescripción `VIGENTE`; antes bastaba con que existiera cualquiera y el selector quedaba vacío.
- Flag de medicación puesto en `true` por orden expresa (`Medicacion:Habilitada=true` en `appsettings.json`, `docker-compose.yml` y `deploy/mpaz-matriculas.env.example`; en el servidor, `_servidor/privado/entorno.json`, que es lo que manda por sobre el `appsettings`). Desde ahora TENS ve el panel de medicación y puede registrar dosis, siempre con orden VIGENTE validada + autorización otorgada (lo exige el trigger, no solo la pantalla).
- Desplegado en el servidor con `_servidor/actualizar.sh`: publicación `20261125T061732Z`, portal en línea (HTTP 302) con el commit `679554d`, Docker verificado.
- Revisión funcional TENS: build Release 0 errores, 213/213 pruebas.
- Pendiente del colegio (sigue bloqueado): protocolo escrito (las 12 preguntas están en `docs/protocolo-medicacion-preguntas-TENS-direccion.docx`), pantalla de ingreso/validación de prescripciones y prueba de aceptación con una orden válida.

---

## 20. Tablas con filtro sin recarga — 25-11-2026

- Nuevo `MarcelaPaz.Web/wwwroot/js/tablas.js` (cargado en `_AdminLayout`): al filtrar, ordenar o paginar solo se reemplaza la región de resultados, sin recargar la página; actualiza URL (`replaceState`), título y anuncia "tabla actualizada" al lector de pantalla. Ante cualquier error navega como antes y sin JS todo sigue igual.
- Tablas cubiertas: Estudiantes (filtros + orden + paginación + tamaño + limpiar), Alertas (año), Reporte de matrículas y Faltantes (año) y Fichas del personal (búsqueda + funcionario + limpiar). Los enlaces que salen de la tabla (ficha, CSV, otros reportes) y los formularios POST no se tocan.
- Ajuste en `ux.js`: el envío automático al cambiar un filtro (`data-ux-enviar-al-cambiar`) pasó a delegado en `document`, para que siga funcionando en los filtros reemplazados.
- Commit `4bf86eb` (9 archivos), pusheado a `origin/main`; desplegado en el servidor con `_servidor/actualizar.sh`: publicación `20261125T063936Z`, portal en línea (HTTP 302), Docker verificado.
- Verificación: `node --check` en ambos JS, build Release 0 errores, 213/213 pruebas. Falta probarlo a mano en el portal (filtrar en Estudiantes, paginar, cambiar de año en Reportes).


---

## 21. Auditoría de la emisión de certificados — 26-11-2026

- **Hallazgo (hoja 08 del plan, caso CER-07):** la emisión de un certificado no dejaba rastro en
  `registros_acceso`. `CertificadosController` auditaba con la acción `EMITIR`, pero
  el `CHECK` de `registros_acceso.accion` solo admitía
  `LEER/BUSCAR/DESCARGAR/EXPORTAR/REEMPLAZAR/DENEGADO`. El `INSERT` fallaba,
  `AuditoriaAcceso` capturaba la excepción y la dejaba solo en el log, así que la
  pantalla funcionaba y el certificado se guardaba, pero sin registro de acceso.
- **Corrección:** `EMITIR` se agregó al `CHECK` en el DDL (`pasos/01-ddl-institucional.sql`,
  `docker/db-init/01-ddl-institucional.sql` e `instalacion/01-estructura-bd.sql`) y se
  creó el paso `28-accion-emitir-en-registros-acceso.sql`, re-ejecutable, para las bases
  ya instaladas.
- **Verificado** en instancia local aislada: emitir el certificado de un alumno con
  matrícula firmada deja una fila
  `accion=EMITIR, area=DOCUMENTOS, recurso=certificado_emitido, recurso_id=1,
  estudiante_id=5, funcionario_id=1` con el folio en `detalle`, y el log no registra
  errores de auditoría.
- **Pendiente de despliegue:** aplicar el paso 28 en la base del colegio y en Neon
  (la tabla existe y la restricción hay que reemplazarla). No se ejecutó en esos
  entornos.
