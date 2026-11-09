<!-- Copia del texto extraído del adjunto MPAZ_Web_Requerimientos_Portal_Matricula_2027.docx. No sustituye el archivo Word original ni conserva su formato visual. -->

LEVANTAMIENTO DE REQUERIMIENTOS

Portal de Matrícula y Expediente Estudiantil

Módulo 3 de 4 · Plataforma institucional integrada MPAZ Web

| Control documental | Detalle |
| --- | --- |
| Estado | Propuesta funcional para revisión y aprobación institucional |
| Institución | Escuela Escritora Marcela Paz, San Bernardo |
| Versión / fecha | 1.0 · 03 de noviembre de 2026 |
| Fuente interna | Ficha de matrícula 2026 aportada por el establecimiento y solicitudes de gestión |
| Alcance | Matrícula digital asistida, generación de ficha para firma, archivo digital y consulta administrativa |

# 1. Resumen ejecutivo

El Portal de Matrícula será una sección específica de MPAZ Web para capturar antecedentes del estudiante y sus responsables, generar automáticamente la ficha de matrícula, formalizarla mediante firma manuscrita y resguardar tanto la versión digital firmada como el original físico. Posteriormente permitirá consultar el expediente desde distintos puestos de trabajo, con accesos diferenciados, historial y trazabilidad.

Este módulo NO sustituye el Sistema de Admisión Escolar (SAE), los sistemas oficiales del Mineduc ni los procedimientos presenciales exigidos para confirmar la matrícula. No se condicionará la matrícula a la entrega inmediata de documentos complementarios ni a consentimientos opcionales.

# 2. Contexto y problema actual

- La ficha vigente se completa y archiva principalmente en papel; su consulta requiere ubicar físicamente la carpeta.
- Varias personas no pueden disponer simultáneamente del mismo documento físico; existen riesgos de extravío, duplicidad y datos desactualizados.
- La ficha existente contiene antecedentes del estudiante, familia, apoderados, contactos, retiro, datos médicos y una autorización general de paracetamol que requiere reformulación.
- Se requiere agregar campos de PIE y apoyos, autorización de imagen, autorización para intervenciones del equipo de Convivencia Escolar y autorización sanitaria individualizada.

# 3. Objetivos

Objetivo general. Digitalizar la formalización y consulta interna de matrículas escolares dentro de una plataforma segura y unificada, reduciendo transcripción manual y preservando respaldo físico y digital.

- Capturar una sola vez datos estructurados mediante formulario con validaciones.
- Generar PDF imprimible de matrícula con datos, cláusulas y firmas requeridas.
- Digitalizar la ficha firmada y anexos, asociándolos a un expediente único del estudiante y año escolar.
- Permitir búsquedas y consultas simultáneas por funcionarios autorizados sin exponer información sensible indiscriminadamente.
- Mantener cambios y versiones, verificar documentación y facilitar continuidad anual de matrícula.

# 4. Ubicación en MPAZ Web y límites

| Sección común | Uso previsto |
| --- | --- |
| Datos de Matrícula | Identificación, apoderados, autorizaciones, respaldo documental y consulta de expedientes |
| Portal de Asistencia | Consume identificador, curso y condición de matrícula; uso controlado de autorizaciones de retiro |
| Portal de Notas | Consume identificador, curso y año; conserva calificaciones por separado |
| Apoyo Estudiantil | Consume identificador y necesidades educativas pertinentes; restringe antecedentes de salud y PIE |

Servicios transversales: inicio de sesión único, roles/permisos de acción, registro único de estudiantes, bitácora de auditoría, respaldo, cifrado y gestión de archivos. La integración no significa que todos los perfiles puedan ver todos los datos.

# 5. Proceso operativo propuesto

| Paso | Actividad | Evidencia / resultado |
| --- | --- | --- |
| 1 | Administrativo inicia matrícula nueva o renovación; valida identidad y curso. | Expediente en estado Borrador. |
| 2 | Completa formulario, consentimientos separados y enlaces de documentos. | Datos estructurados y alertas de campos faltantes. |
| 3 | Carga certificados y respaldos aplicables, o registra pendientes. | Inventario documental con fechas. |
| 4 | Revisa vista previa y genera PDF de versión cerrada para firma. | Ficha con folio, año, versión y fecha. |
| 5 | Apoderado firma presencialmente los apartados correspondientes. | Original físico firmado. |
| 6 | Funcionario escanea/sube ficha firmada y revisa legibilidad. | Copia digital autenticada administrativamente por responsable. |
| 7 | Confirma matrícula conforme procedimiento oficial y archiva original. | Expediente de consulta vigente y ubicación de archivo físico. |
| 8 | En el futuro actualiza datos con historial y emite nueva versión cuando sea necesario. | Trazabilidad de modificaciones y consentimientos. |

# 6. Estructura del formulario de matrícula

| Bloque | Campos mínimos |
| --- | --- |
| A. Estudiante | Nombre y apellidos, RUN/IPE u otro identificador admitido, nacimiento, nacionalidad, dirección, curso, año y estado. |
| B. Familia / apoderados | Madre, padre, apoderado titular/suplente; identificación, vínculo, teléfonos, correo, dirección y restricciones judiciales pertinentes. |
| C. Emergencia y retiro | Contactos de emergencia, personas autorizadas a retirar, autorización de salida solo y restricciones vigentes. |
| D. Salud | Alergias y condiciones relevantes para la atención escolar, necesidades de atención, contacto sanitario y respaldos. |
| E. PIE y apoyos | Participación PIE (sí/no/pendiente), apoyos informados, existencia de diagnóstico/documentación; acceso al detalle por rol autorizado. |
| F. Imagen | Autorización independiente, canales de difusión, propósito, firma, vigencia y revocación. |
| G. Convivencia Escolar | Autorización específica para acompañamiento/intervención por profesionales de convivencia no docentes, alcance definido, firma y revocación. |
| H. Medicación TENS | Solicitud y autorización individual por medicamento prescrito, orden médica adjunta, dosis, horario, vigencia y TENS responsable. |
| I. Documentos y firmas | Identificación, certificado de nacimiento si corresponde, poder de tercero si aplica, otros respaldos; nombre y firma de apoderado, fecha y funcionario. |

# 7. Consentimientos y protocolo sanitario

## 7.1 Convivencia Escolar

Se incorporará un consentimiento separado que describa el tipo de intervención o acompañamiento, equipo responsable, finalidad, canales de comunicación con la familia y posibilidad de revocación conforme a normativa aplicable. La necesidad jurídica y el alcance exacto de este consentimiento deberán validarse con Dirección, Convivencia Escolar y el sostenedor; no se presumirá que toda actividad del equipo requiere el mismo consentimiento.

## 7.2 Uso de imagen

Separar la autorización de captación interna de la difusión externa (sitio institucional, redes sociales u otros medios). Permitir autorizar o rechazar por canales; registrar firmante, fecha, texto aceptado y revocaciones. Una negativa no afecta la matrícula.

## 7.3 Administración de medicamentos por TENS

Sustituir la autorización genérica de “paracetamol cuando se considere necesario” por un procedimiento documentado por indicación médica. Según el requisito entregado por la escuela, exclusivamente el/la TENS institucional administrará los medicamentos autorizados, siempre sujeto a prescripción, alcance de competencias y protocolo aprobado por el establecimiento.

- Registrar medicamento, prescripción/orden médica vigente, dosis, vía, frecuencia, horario, vigencia, precauciones y firma de autorización del apoderado.
- Adjuntar la orden médica y verificar su validez antes de habilitar administración.
- Guardar un registro separado de cada administración efectiva: fecha, hora, dosis, responsable TENS, incidencias y observaciones.
- Cuando el TENS no esté disponible, no se debe asumir autorización de otro funcionario: aplicar el protocolo formal de contingencia que defina la institución.
- Paracetamol también requiere la indicación correspondiente según el procedimiento definido; la ficha por sí misma no constituye orden para medicar.

# 8. Requerimientos funcionales verificables

| Código | Descripción |
| --- | --- |
| MAT-01 | Crear matrícula nueva y renovaciones por año escolar. |
| MAT-02 | Validar identificador y prevenir duplicados de estudiantes. |
| MAT-03 | Registrar datos familiares, emergencia, retiro, salud y apoyos. |
| MAT-04 | Registrar estados de autorización individual por imagen, convivencia y medicación. |
| MAT-05 | Vincular prescripción y autorización sanitaria; solo TENS puede registrar suministro efectivo. |
| MAT-06 | Acceder desde enlaces oficiales a certificados y subir archivos descargados. |
| MAT-07 | Clasificar documentación como recibida, no aplica o pendiente sin bloquear indebidamente matrícula. |
| MAT-08 | Generar PDF fiel al formulario con versión, folio, año y campos de firmas. |
| MAT-09 | Subir escaneo firmado y asociarlo a la versión generada. |
| MAT-10 | Registrar ubicación del original físico y estado de validación documental. |
| MAT-11 | Consultar simultáneamente fichas por RUN/identificador, nombre, curso, año y estado. |
| MAT-12 | Aplicar permisos de leer, crear, editar, exportar, validar y administrar por rol y campo. |
| MAT-13 | Registrar auditoría de acceso, cambios, descargas y reemplazo de documentos. |
| MAT-14 | Mantener versiones históricas de ficha, autorizaciones y cambios de apoderado. |
| MAT-15 | Compartir identificador y datos mínimos con Asistencia, Notas y Apoyo. |
| MAT-16 | Exportar reportes autorizados de estado de matrículas y faltantes documentales. |
| MAT-17 | Emitir alertas por órdenes médicas o autorizaciones vencidas. |
| MAT-18 | Permitir corrección controlada de errores sin sobreescribir documento firmado. |

# 9. Portal de consulta administrativa

La consulta será una vista distinta del formulario de captura. Mostrará identificación general, matrícula vigente, curso, apoderados, contactos autorizados, estado documental, autorizaciones vigentes e historial de versiones. Incorporará pestañas con acceso condicionado para salud, PIE, órdenes médicas y documentos judiciales.

- Búsqueda global y filtros por curso, año, estado y pendientes.
- Vista previa de PDF firmado, con descarga solo si el rol lo permite.
- Acceso concurrente a lectura; controles de versión para evitar sobrescritura en edición.
- Indicadores de última actualización y funcionario responsable.
- Acceso rápido a módulos vinculados sin transferir datos sensibles por defecto.

# 10. Modelo de permisos inicial (propuesta a validar)

| Perfil | Matrícula general | Documentos / autorizaciones sensibles |
| --- | --- | --- |
| Secretaría / matrícula | Crear, actualizar, formalizar, consultar | Solo documentos que requiera el trámite |
| Dirección | Consulta y validación institucional | Por necesidad funcional y autorización |
| Inspectoría | Consulta de contacto y retiro | Sin acceso irrestricto a diagnósticos |
| Equipo PIE | Consulta de datos mínimos y apoyos | Acceso a antecedentes PIE autorizados |
| TENS | Consulta de contactos y salud pertinente | Órdenes médicas, autorización y registro de suministro |
| Docentes | Datos mínimos según funciones | No acceso por defecto a expediente completo |
| Administración TI | Soporte, cuentas y configuración | Sin lectura rutinaria de datos clínicos o judiciales |

# 11. Requisitos no funcionales y seguridad

- Control de acceso en servidor por roles y permisos granulares, no solo ocultar botones en interfaz.
- Cifrado TLS en tránsito; protección de archivos, sesiones, contraseñas y respaldos; mínimos privilegios.
- Auditoría de consulta y descarga de datos sensibles; registros protegidos de alteraciones.
- Archivos permitidos PDF/JPG/PNG; límites de tamaño; análisis de archivos y almacenamiento fuera de directorios públicos.
- Mecanismo de respaldo y restauración probado; políticas de conservación y eliminación definidas con sostenedor.
- Accesibilidad, formulario en pasos, validación clara de errores y posibilidad de guardar borradores.
- La base de datos compartirá identificadores estables; separar tablas de consentimientos, documentos, salud y matrícula anual.
- Preparar cumplimiento de Ley 21.719 vigente desde 01-12-2026 y normativa sectorial aplicable; revisión jurídica y del sostenedor antes de operación.

# 12. Documentos oficiales y enlaces útiles

El portal presentará hipervínculos informativos que abran los sitios oficiales en otra pestaña. No almacenará contraseñas gubernamentales ni automatizará la extracción de datos personales desde esos portales sin autorización y soporte legal/técnico.

| Documento o trámite | Enlace / criterio |
| --- | --- |
| Certificado de nacimiento (tipo matrícula) | https://www.chileatiende.gob.cl/tramites/9380 |
| Matrícula y documentos admisibles SAE | https://www.sistemadeadmisionescolar.cl/matricula.html |
| Certificado de alumno regular | Lo emite el establecimiento donde está matriculado el estudiante: no ofrecer como descarga gubernamental automática. https://www.chileatiende.gob.cl/fichas/33265/1/pdf |
| Certificado anual de estudios (si se solicita en un caso aplicable) | https://www.chileatiende.gob.cl/fichas/2272 |
| Identificadores provisorios IPE/IPA (según caso) | https://serviciomigraciones.cl/guia/sistema-admision-escolar/ |

Conforme a SAE, la identidad del estudiante y apoderado es el núcleo de la documentación exigible; los documentos complementarios pueden solicitarse para registro, pero su falta no debe impedir la matrícula. El certificado de alumno regular es emitido por el establecimiento; no debe confundirse con el certificado anual de estudios.

# 13. Criterios de aceptación y pruebas esenciales

| Escenario | Resultado esperado |
| --- | --- |
| Ficha completa | Genera PDF imprimible con todos los datos y bloques de firma sin transcribir manualmente. |
| Falta un documento complementario | Marca pendiente, no bloquea la formalización si no es legalmente exigible. |
| Dos funcionarios consultan el mismo estudiante | Ambos acceden a la misma versión sin esperar carpeta física. |
| Apoderado revoca imagen | Estado actualizado, versión histórica y fecha visibles; difusión posterior bloqueada según alcance. |
| Docente sin permiso solicita diagnóstico | Acceso denegado en interfaz y API; evento auditable. |
| TENS registra una dosis | Exige autorización y orden vigentes; guarda responsable, fecha, hora y dosis. |
| Se corrige matrícula firmada | Preserva original y genera nueva versión para revisión y firma si corresponde. |
| Estudiante continúa el próximo año | Precarga datos vigentes, exige confirmación y genera ficha del nuevo año. |

# 14. Vinculación de datos y diseño preliminar

Entidades recomendadas: estudiantes; personas/contactos; relaciones estudiante-persona; matrículas_anuales; cursos; autorizaciones (tipo, alcance, versión, fecha y estado); documentos (tipo, ruta protegida, hash, estado); antecedentes_salud (acceso restringido); prescripciones; administraciones_medicamento; antecedentes_apoyo; historial_cambios; registros_acceso. Esta propuesta deberá adaptarse al esquema de la BD actual y a la migración planificada a PostgreSQL.

Integración por identificador interno del estudiante. Asistencia recibirá información imprescindible para asociar estudiantes y sus cursos, y eventualmente las restricciones de salida autorizadas; Notas tendrá matrícula y curso vigente; Apoyo recibirá solo datos y orientaciones que cada rol pueda conocer.

# 15. Alcance por etapas y exclusiones

| Etapa | Entregables |
| --- | --- |
| I · Captura y PDF | Formulario digital, validación, ficha automática, impresión y firma manuscrita. |
| II · Expediente | Adjuntos, escaneo firmado, archivo físico referenciado, permisos y consulta administrativa. |
| III · Continuidad | Renovación anual, versiones, auditoría, reportes y alertas. |
| IV · Integración | Intercambio controlado con Asistencia RFID, Portal de Notas y Apoyo Estudiantil. |

Fuera de alcance inicial: sustituir SAE o SIGE, firma electrónica avanzada, acceso público de apoderados, emisión por Mineduc de certificados, automatización de sitios estatales, prescripción médica, diagnóstico clínico y administración de medicamentos por la plataforma.

# 16. Dependencias y decisiones pendientes

- Validación final de los textos de autorizaciones y firmas por Dirección, Convivencia Escolar, TENS y sostenedor.
- Definir protocolo escrito de medicación, custodia de medicamentos y actuaciones ante ausencia del TENS.
- Confirmar tipos de documentos que la escuela solicita actualmente y su obligatoriedad legal.
- Aprobar perfiles, alcance de información visible y responsables por proceso.
- Definir conservación de originales físicos, digitalización, plazos de retención y eliminación segura.
- Determinar formato único de ficha para matrícula nueva y renovación, con anexos opcionales.
- Decidir si el certificado de alumno regular se generará después desde los datos confirmados del colegio.

# 17. Trazabilidad de origen

Fuente institucional: FICHA (1).docx, “Ficha de Matrícula 2026” de tres páginas. Contiene apartados de estudiante, madre/padre, apoderados, contactos de emergencia, retiro, datos médicos y autorización genérica de paracetamol. Requisitos añadidos por solicitud de gestión: PIE y diagnóstico, imagen, Convivencia Escolar, orden médica/TENS, portales oficiales, ficha generada y expediente consultable.

Referencias públicas consultadas: Sistema de Admisión Escolar, matrícula y preguntas frecuentes; ChileAtiende, certificado de nacimiento y alumno regular; Biblioteca del Congreso Nacional, Ley 21.719. Consultadas el 03-11-2026. Las fuentes públicas informan criterios generales y no sustituyen validación normativa del establecimiento.

Estado del documento: BASE DE PROPUESTA. No acredita aprobación institucional ni implementación técnica. Antes de su despliegue deberán revisarse los consentimientos, las políticas de tratamiento de datos y los protocolos de salud.
