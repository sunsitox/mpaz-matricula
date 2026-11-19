# Decisiones pendientes del colegio · Portal de Matrícula MPAZ

**19-11-2026 · Documento de trabajo.** Cada fila es una decisión que hoy está
detenida esperando al colegio. La columna «propuesta por defecto» es lo que el
portal asume **mientras no haya respuesta**; nada de este documento está
aplicado en la base del colegio ni cambia el comportamiento actual por sí solo.
Para aprobar basta marcar «Sí» o corregir la propuesta en la última columna.

## 1. Decisiones que bloquean el uso normal del portal

| # | Decisión | Propuesta por defecto | Qué se habilita al aprobarla |
| --- | --- | --- | --- |
| 1 | **Matriz de permisos por rol** (anexo A) | Aprobar la matriz del anexo tal como está propuesta | Que los nueve perfiles puedan trabajar. Sin matriz cargada en la base, cualquier pantalla con permiso responde 403 después del login. |
| 2 | **Quién formaliza la matrícula** (crea, confirma, anula) | Secretaría crea, edita, confirma y anula; Dirección valida; Inspectoría edita datos del estudiante; el resto solo consulta | Los botones «Iniciar matrícula», «Confirmar», «Registrar firma» y «Anular» quedan disponibles para el perfil que se elija. |
| 3 | **Protocolo de medicación por TENS** (MAT-05), incluida la custodia de medicamentos y qué hacer si el TENS no está | Mantener apagado (`Medicacion:Habilitada=false`) | Prescripción con orden médica, autorización por medicamento, registro de dosis con responsable/fecha/hora y alertas de órdenes por vencer. |
| 4 | **Textos legales de las autorizaciones de imagen y convivencia** | Aprobar los textos hoy vigentes en el portal como versión 1, con el nombre de quien aprueba | La autorización queda con plantilla aprobada y su texto impreso en la ficha. Mientras no se apruebe, se guarda como «Pendiente de aprobación (Dirección)». |
| 5 | **Paracetamol** | Mantener la pregunta reformulada («únicamente con indicación médica y protocolo TENS aprobado») como declaración informativa, sin efecto de suministro | Retirar el campo de la ficha o imprimirlo como constancia. Hoy **no** autoriza administrar nada. |

## 2. Alcance de cada perfil

| # | Decisión | Propuesta por defecto | Qué se habilita al aprobarla |
| --- | --- | --- | --- |
| 6 | **¿«Ver todo» incluye salud y antecedentes judiciales?** (UTP, Dirección, Inspectoría) | Sí: los tres ven todas las áreas, incluida salud, PIE, judicial y medicación (solo lectura) | Se ajustan las áreas de esos roles. El servidor ya recorta la ficha y el PDF por área, así que el cambio es solo de matriz. |
| 7 | **Alcance de PIE y Convivencia Escolar** | PIE: General, Familia, PIE y Documentos. Convivencia: General, Familia, Retiro, Autorizaciones y Documentos, **sin** salud, PIE ni judicial | Qué bloques de la ficha ven esos equipos y qué se imprime en su copia PDF. |
| 8 | **¿Validar es editar?** (Dirección) | No: Dirección valida ficha, autorizaciones y documentos sin modificar datos | Se mantiene `VALIDAR` separado de `EDITAR`; si el colegio considera que validar implica corregir, se le agrega edición. |
| 9 | **Quién es el «admin» que edita junto a Inspectoría** | Administración TI solo gestiona cuentas y auditoría; la edición de datos la hace Inspectoría | Si debe ser otra persona (encargado de matrícula), se crea o ajusta ese rol. |
| 10 | **Pestaña pública de registro** | No habilitar; el acceso sigue siendo solo con cuenta de funcionario | Preinscripción del apoderado o captura asistida. Requiere definir qué crea cada envío, validaciones, anti-abuso y quién revisa. |

## 3. Datos, documentos y cumplimiento

| # | Decisión | Propuesta por defecto | Qué se habilita al aprobarla |
| --- | --- | --- | --- |
| 11 | **Documentos obligatorios y formato único** de ficha para matrícula nueva y renovación | Los requisitos actuales del expediente, con estado «pendiente» que no bloquea la formalización | Marcar cuáles bloquean el cierre y unificar ficha nueva/renovación con anexos opcionales. |
| 12 | **Re-firma al corregir una ficha firmada** | Siempre: cada corrección genera una versión nueva y se vuelve a firmar | Relajar la regla a «solo cambios importantes» y definir cuáles son. |
| 13 | **Conservación de originales y datos, y eliminación segura** | No borrar nada: expediente y auditoría se conservan; solo se poda el resumen técnico de alertas a 24 meses | Política de retención por tipo de documento y procedimiento de eliminación con el sostenedor. |
| 14 | **Certificado de alumno regular** | Formato actual: folio, código de validación pública y 30 días de vigencia | Ajustar firma, texto y plazo; hoy el formato queda por validar institucionalmente. |
| 15 | **Ley 21.719** (vigente desde el 01-12-2026) | Mantener el tratamiento actual: acceso por rol, auditoría de cada lectura y sin cesión a terceros | Aviso de privacidad, derechos de acceso/rectificación y plazos formales de conservación. |

## 4. Temas menores (no bloquean)

- **Vencimiento de autorizaciones:** hoy lo persiste la revisión automática cada 15 minutos; no requiere decisión.
- **Resumen de alertas (`alerta_revision`):** crece una fila cada 15 minutos. Propuesta: conservar 24 meses y podar lo anterior.
- **Cachés de comunas y cursos:** dar de alta una comuna o un curso exige reiniciar el portal. Propuesta: aceptarlo hasta la próxima versión.
- **Configuración → Año escolar:** hoy solo Secretaría y Dirección, por nombre de rol. Propuesta: dejarlo así y pasar a permiso más adelante.
- **SSO para Asistencia:** propuesta: mantener el identificador propio del funcionario y que Asistencia resuelva al estudiante por RUT. Si el equipo prefiere el identificador de persona, hay que cambiarlo **antes** de conectar Asistencia.

---

## Anexo A · Matriz de permisos propuesta (según lo indicado por Johan el 17-11-2026)

**No aplicada.** Es la matriz que ya está escrita en
`database/modulos/matricula/pasos/03-permisos-iniciales.sql` y en los pasos 14 y
15, y que se cargaría recién con la aprobación del colegio.

Leyenda: **L** = leer · **C** = crear · **E** = editar · **V** = validar ·
**X** = exportar · **A** = administrar · **—** = sin acceso.

| Área | SECRETARÍA | DIRECCIÓN | INSPECTORÍA | PIE | UTP | CONVIVENCIA | TENS | DOCENTE | ADMIN_TI |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| General (matrícula, listado, ficha) | L C E X | L V X | L E | L | L X | L | L | L | — |
| Familia y apoderados | L C E | L | L E | L | L | L | L | — | — |
| Retiro autorizado | L C E | L | L E | — | L | L | — | — | — |
| Salud | L C E | L | L | — | L | — | L | — | — |
| PIE y apoyos | — | L | L | L | L | — | — | — | — |
| Antecedentes judiciales | L C E | L | L | — | L | — | — | — | — |
| Autorizaciones del apoderado | L C E | L V | L | — | L | L | — | — | — |
| Documentos y expediente | L C E X | L V X | L | L | L X | L | L | — | — |
| Medicación | — | L | L | — | L | — | L C | — | — |
| Administración de cuentas | — | — | — | — | — | — | — | — | L A |
| Auditoría | — | — | — | — | — | — | — | — | L |

### Interpretaciones que conviene confirmar

1. **Dirección** «ve todo» según Johan; la propuesta le suma **validar** ficha,
   autorizaciones y documentos, y **exportar**. Si validar se considera editar,
   hay que decidirlo (decisión 8).
2. **Convivencia** solo ve «lo necesario»: la propuesta excluye salud, PIE y
   judicial. Johan no precisó el detalle (decisión 7).
3. **Secretaría** no fue mencionada por Johan. La propuesta la deja como único
   perfil que crea y formaliza, pero **no** le da PIE ni medicación; si debe
   capturar la ficha completa, hay que agregarle `PIE:LEER` (decisión 6).
4. **Inspectoría** edita General, Familia y Retiro, pero no crea ni valida
   matrículas, tal como se indicó.
5. **TENS** recibe General, Familia, Salud, Medicación y Documentos en lectura,
   más registrar dosis. Aun con el permiso, la medicación sigue apagada hasta
   aprobar el protocolo (decisión 3).

## B · Cómo se aplica una vez aprobado

1. Se ajusta la matriz en `03-permisos-iniciales.sql` / pasos 14 y 15 según las
   correcciones que devuelva el colegio.
2. Se ejecuta en la base del colegio (`psql ... -f`), con respaldo previo.
3. Cada funcionario vuelve a iniciar sesión: los permisos se toman al entrar y
   la sesión se cierra si se le quitan permisos.
4. Queda registro en auditoría de cada lectura y cambio, como hoy.

Ninguno de estos pasos se ejecutó todavía: el portal sigue en trabajo local y la
base del colegio no tiene esta matriz cargada.
