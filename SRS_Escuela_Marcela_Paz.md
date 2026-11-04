# Documento 1 — SRS Específico: Sistema de Matrícula Escuela Marcela Paz

**Información de control del documento**

| Campo | Valor |
|---|---|
| Nombre del producto | Sistema de Matrícula — Escuela Básica Escritora Marcela Paz |
| Código del documento | SRS-EME-001 |
| Versión | 1.0 (Borrador de especificación) |
| Fecha | 15 de octubre de 2026 |
| Autor | Arquitecto de Software y Analista de Sistemas Senior |
| Descripción del alcance del documento | Especificación de requisitos del sistema web interno de matrícula y administración de estudiantes/apoderados. |
| Estado | Borrador histórico; requiere revisión con Secretaría/Dirección después de reunión 1 |

**Historial de cambios**

| Versión | Fecha | Autor | Descripción |
|---|---|---|---|
| 1.0 | 15-10-2026 | Autor | Versión inicial con módulos de apoderados, administración, reportes, seguridad y auditoría. |

---

> **Advertencia de vigencia (21-10-2026):** Este SRS fue escrito antes de la [reunión 1](scripts/docs/reunion-01-2026-10-21.md) y **no está aprobado**. Según las observaciones preliminares, el alcance inicial se enfoca en estudiantes ya inscritos por un proceso externo y sus apoderados, con ficha en etapas y PostgreSQL sobre un servidor local Ubuntu. El acceso RUN + número de documento aquí descrito está desactualizado; se conversó sobre RUT estudiante + RUT apoderado + año de nacimiento, pero **esa combinación tampoco debe tratarse como autenticación suficiente** para información sensible o edición sin revisión de seguridad. Las funciones de administración y documentos PDF se proyectaron para una etapa posterior; Blazor frente a Razor Pages está por decidir. Mantener los requisitos y criterios de aceptación que siguen como hipótesis históricas hasta revisar el acta de constitución y emitir una nueva versión de la especificación.

## 1. Objetivo del proyecto y alcance

### 1.1 Descripción general
El sistema es una aplicación web interna para la Escuela Básica Escritora Marcela Paz que permite digitalizar el proceso de matrícula y registros afines, administrar estudiantes y sus apoderados, controlar el acceso según roles institucionales y generar comprobantes y listados para la secretaría y la dirección. Se construye con C# ASP.NET Core en .NET 8 o 9, siguiendo Clean Architecture en cuatro niveles lógicos (Domain, Application, Infrastructure, Web), con PostgreSQL como sistema de persistencia y Tailwind CSS para la interfaz de usuario.

El objetivo principal es reemplazar o complementar el control actual —documentación en soporte físico y/o hojas de cálculo compartidas— por un registro digital único, trazable y con control de acceso por roles, permitiendo registrar y consultar matrículas y fichas de apoderados, administrar cupos por curso y emitir comprobantes y listados en formatos imprimibles o exportables.

### 1.2 Límites del sistema (alcance del producto)
- **Está dentro del alcance:**
  - Registro de matrículas sin clave de acceso inicial, usando RUN y número de documento como identificación institucional.
  - Gestión institucional de estudiantes y apoderados.
  - Ficha digital de apoderados con datos de contacto, y registro de salud y PIE cuando corresponda.
  - Administración y secretaría con dashboard, filtro de fichas, aprobación de solicitudes y gestión de cupos por curso.
  - Generación de comprobante PDF con código QR y listas de curso en Excel y PDF.
  - Seguridad basada en roles y registro de auditoría de acciones relevantes.
  - Respaldos de datos y política de reserva asociada al sistema.

- **Está fuera del alcance (por ahora):**
  - Gestión académica completa (calificaciones, evaluaciones, plan de aprendizaje).
  - Control de asistencia escolar diaria, si se considera módulo independiente en el futuro.
  - Integración automática con sistemas externos del Ministerio de Educación, salvo que se requiera explícitamente y se defina como parte del alcance de entrega.
  - Operaciones de pago, colegiación o gestión financiera de la escuela.
  - Correo electrónico masivo de notificaciones, si no se llega a autorizar en una entrega posterior.

### 1.3 Definiciones clave
- **RUN:** Rol Único Nacional, identificador nacional de uso institucional en Chile.
- **N° de documento:** número del documento de identidad asociado según la norma de identificación de la institución.
- **Apoderado:** persona responsable según la organización institucional y relativa a un estudiante.
- **PIE:** Plan de Igualación Escolar o instrumento similar que la escuela utiliza según corresponda.
- **Cupos por curso:** capacidad admitida para cada curso o sección en el proceso de matrícula.
- **Comprobante:** constancia digital generada por el sistema para un registro de matrícula o acción institucional.

---

## 2. Stack tecnológico y arquitectura

### 2.1 Arquitectura propuesta
Se adopta un enfoque de Clean Architecture con separación conceptual en cuatro capas:
- **Domain:** reglas de negocio, entidades y valores del dominio escolar.
- **Application:** casos de uso, validaciones de flujo, coordinación de dominios e infraestructura.
- **Infrastructure:** persistencia en PostgreSQL, operaciones de archivo, integraciones externas si existen, y mecanismos auxiliares.
- **Web:** presentación en ASP.NET Core y UI con Tailwind CSS.

Esta separación busca que las reglas del negocio no dependan de la interfaz ni de la base de datos, facilitando pruebas, mantenimiento y futuras extensiones. La decisión arquitectónica se documenta aquí como restricción de diseño del proyecto y puede complementarse con decisiones archivadas (ADR) en un documento de diseño separado.

### 2.2 Justificación técnica del stack
- **C# ASP.NET Core (.NET 8/9):** plataforma consolidada para aplicaciones web backend fuertemente tipadas, con modelo de seguridad, control de acceso y serialización que apoya una aplicación institucional con roles y auditoría.
- **Clean Architecture:** separa políticas del negocio de mecanismos periféricos y reduce el acoplamiento con la presentación y la persistencia; lo que facilita la evolución del producto y la verificación de reglas escolares sin depender de la interfaz.
- **PostgreSQL:** base de datos relacional de uso general capaz de soportar transacciones, integridad referencial y consultas de administración y reportes. Se sugiere definir restricciones, índices y diseño de transacciones desde la SRS para los casos de cambio relacionados (por ejemplo, matrícula y vínculo con apoderado).
- **Tailwind CSS:** utilidad de interfaz para un sistema interno con enfoque de usabilidad y mantenimiento rápido. Se mantiene en la capa de presentación; no sustituye el control de acceso del servidor.

---

## 3. Requerimientos funcionales

Los requisitos funcionales se organizan por módulo. Prioridades: Alta, Media, Baja.

### 3.1 Módulo de Apoderados

| Código | Nombre | Descripción detallada | Prioridad |
|---|---|---|---|
| RF-01-01 | Registro de apoderado por RUN/Serie | El sistema deberá permitir registrar el apoderado usando RUN y el número de documento como referencia de identificación institucional, sin exigir clave inicial en el acto de registro, según lo definido por la institución. | Alta |
| RF-01-02 | Ficha digital del apoderado | El sistema deberá mantener una ficha digital del apoderado con datos institucionales de contacto y vínculo con el o los estudiantes asignados, diferenciando relaciones y roles según corresponda. | Alta |
| RF-01-03 | Registro de salud y PIE | El sistema deberá permitir registrar la información de salud y PIE del estudiante cuando la institución la considere relevante, controlando quién puede ver, ingresar o actualizar estos datos según roles. | Alta |
| RF-01-04 | Relación estudiante-apoderado | El sistema deberá permitir asociar y modificar relaciones entre estudiantes y apoderados, con registro de cambio y control de acceso sobre cada vínculo. | Media |
| RF-01-05 | Validación de duplicados | El sistema deberá avisar o manejar, según política institucional, la existencia de registros duplicados al ingresar RUN + documento. | Media |

### 3.2 Módulo de Administración / Secretaría

| Código | Nombre | Descripción detallada | Prioridad |
|---|---|---|---|
| RF-02-01 | Dashboard institucional | El sistema deberá mostrar un panel de administración o secretaría con resumen de matrículas, estados activos, pendientes y observables de operación, según perfil de usuario. | Alta |
| RF-02-02 | Filtro de fichas | El sistema deberá permitir filtrar fichas de estudiantes y apoderados por criterios institucionales autorizados, sin mostrar campos reservados a roles no habilitados. | Alta |
| RF-02-03 | Aprobación de solicitudes | El sistema deberá permitir que la secretaría o el rol autorizado apruebe o gestione solicitudes de matrícula o vínculos según flujo institucional. | Alta |
| RF-02-04 | Gestión de cupos por curso | El sistema deberá permitir configurar y controlar cupos por curso o sección, registrar ocupación y rechazar o gestionar inscripciones según capacidad definida. | Alta |
| RF-02-05 | Búsqueda autorizada | El sistema deberá permitir buscar estudiantes y apoderados por RUN/documento o otros identificadores institucionales dentro del alcance del usuario autenticado. | Media |
| RF-02-06 | Control de estados institucionales | El sistema deberá permitir definir estados relevantes del proceso de matrícula y del estudiante (por ejemplo, activo, pendiente, aprobado, retirado) según política de la escuela. | Media |

### 3.3 Módulo de Reportes e Impresión

| Código | Nombre | Descripción detallada | Prioridad |
|---|---|---|---|
| RF-03-01 | Comprobante PDF con QR | El sistema deberá generar un comprobante PDF que contenga información institucional requerida y un código QR cuyo contenido sea controlado institucionalmente y no incluya datos personales sensibles sin justificación y control. | Alta |
| RF-03-02 | Listas de curso en Excel | El sistema deberá permitir exportar listas de curso en Excel (.xlsx) con columnas definidas para el rol autorizado, respetando campos reservados y rastreando la exportación. | Alta |
| RF-03-03 | Listas de curso en PDF | El sistema deberá permitir generar listas de curso en PDF según el rol y el alcance del usuario, con los datos y formato aprobado por la institución. | Media |
| RF-03-04 | Filtros de reporte | El sistema deberá permitir definir filtros de reporte por curso, estado o periodo, según lo que la institución autorice, y solo entregar los resultados dentro del alcance del usuario. | Media |
| RF-03-05 | Registro de generación de reportes | El sistema deberá registrar la generación de reportes y exportaciones relevantes para fines de auditoría y trazabilidad institucional. | Media |

### 3.4 Módulo de Seguridad y Auditoría

| Código | Nombre | Descripción detallada | Prioridad |
|---|---|---|---|
| RF-04-01 | Roles institucionales | El sistema deberá implementar roles aplicables a la operación escolar, como administrador, secretaría, autoridad institucional y técnico/auditor según alcance, y asignar permisos de forma controlada. | Alta |
| RF-04-02 | Control de acceso por roles | El sistema deberá proteger las funciones e información según rol y alcance, no solo en la interfaz sino sobre todo en el servidor para cada acción de consulta, creación, edición, eliminación, descarga y exportación. | Alta |
| RF-04-03 | Registro de cambios | El sistema deberá registrar acciones institucionalmente relevantes sobre estudiantes, apoderados, matrículas, roles y exportaciones, con actor, instante y resultado, respetando lo que no debe quedar en logs. | Alta |
| RF-04-04 | Respaldo y recuperación | El sistema deberá basarse en un proceso de respaldo de datos y políticas de retención, con comprobación periódica de la capacidad de recuperación según lo acordado con la institución. | Alta |
| RF-04-05 | Gestión de cuentas y autenticación | El sistema deberá permitir gestión de cuentas de usuarios internos y control de accesos, con recursos adecuados para el nivel de sensibilidad de la información escolar. | Alta |
| RF-04-06 | Privacidad de datos personales | El sistema deberá tratar RUN y números de documento como datos de identificación institucional de control, aplicando mínimo necesario, enmascaramiento en listas ordinarias y acceso restringido a roles autorizados. | Alta |

---

## 4. Requerimientos no funcionales

| Código | Nombre | Descripción | Prioridad |
|---|---|---|---|
| RNF-01-01 | Rendimiento operativo | El sistema deberá responder de forma usable en un entorno institucional esperado; los tiempos de respuesta y volúmenes admitidos se definirán con valores medibles durante la fase de validación. | Media |
| RNF-01-02 | Privacidad de datos | El sistema deberá manejar RUN, número de documento y datos de estudiantes y apoderados con mínimo necesario, enmascaramiento en vistas y listados no autorizados, y acceso controlado por servidor a cada acción relevante. | Alta |
| RNF-01-03 | Usabilidad móvil-first | La interfaz deberá ser funcional y legible en navegadores modernos con enfoque de usabilidad para dispositivos comúnmente usados por la institución, manteniendo claridad en secciones críticas. | Media |
| RNF-01-04 | Disponibilidad | El sistema deberá operar con una disponibilidad adecuada para una herramienta interna escolar; los objetivos concretos de disponibilidad, tiempo de recuperación y pérdida de datos admisible se definirán con la institución. | Media |
| RNF-01-05 | Seguridad y configuración | El sistema deberá usar medidas de transmisión y almacenamiento adecuadas para la sensibilidad de la información, con gestión de claves, acceso limitado y monitoreo de eventos relevantes. | Alta |
| RNF-01-06 | Integridad y transaccionalidad | El sistema deberá preservar la coherencia de operaciones vinculadas, como asociaciones de matrícula con apoderado y cambios de estado, mediante transacciones cuando corresponda. | Alta |
| RNF-01-07 | Trazabilidad y auditoría | El sistema deberá permitir rastrear acciones relevantes y evidenciar quién realizó qué, cuándo y con qué resultado, sin registrar información innecesaria de usuarios o documentos. | Alta |
| RNF-01-08 | Mantenibilidad | El sistema deberá organizarse para que reglas, validaciones y repositorios sean mantenibles y probables con separación actual que favorezca pruebas sobre las reglas del negocio. | Media |

---

## 5. Historias de usuario y casos de uso principales

### 5.1 Historias de usuario

**HU-01** — Como apoderado, quiero completar o consultar mi información en el sistema con RUN y documento, para que la secretaría pueda gestionar la matrícula del estudiante sin necesidad de un proceso de clave inicial en el acto.

Criterios de aceptación:
- Dado que el apoderado presenta RUN y número de documento, el sistema permite registrar o consultar su ficha según la política definida.
- La ficha digital refleja los datos básicos de contacto y vínculo con el estudiante asignado.
- La salud y PIE queda registrada y protegida según roles habilitados.

**HU-02** — Como secretario/a o administrador/a, quiero ver un dashboard y filtrar fichas, para gestionar aprobaciones y controlar cupos por curso sin revisar registros manuales dispersos.

Criterios de aceptación:
- Dado que el usuario está autenticado en rol institucional, el sistema muestra un resumen de matrículas y estados relevantes.
- Dado que el usuario aplica filtros, el sistema devuelve solo los resultados dentro de su alcance.
- Dado que la institución define cupos por curso, el sistema permite gestionar inscripciones de acuerdo a la capacidad.

**HU-03** — Como autoridad institucional, quiero generar un comprobante PDF con QR y exportar listas de curso en Excel o PDF, para dar constancia a estudiantes/apoderados y para operaciones de secretaría.

Criterios de aceptación:
- Dado que el requerimiento es de un rol autorizado, el sistema genera el comprobante con QR y el contenido controlado por la institución.
- El listado de curso exportado contiene las columnas autorizadas y se registra la generación.
- El QR no expone datos personales sensibles sin control institucional.

**HU-04** — Como operador/a de seguridad o administrador/a del sistema, quiero que los roles controlen el acceso por servidor y que los cambios importantes queden registrados, para mantener seguridad y trazabilidad institucional.

Criterios de aceptación:
- Dado que un usuario intenta acceder a una función protegida, el sistema verifica rol y alcance en servidor.
- Dado que ocurre una acción relevante, el sistema registra un evento de auditoría con suficiente contexto institucional.
- Dado que se producen cambios en registros vinculados, el sistema conserva la coherencia de la operación.

### 5.2 Flujo principal — Registro de matrícula con RUN y documento (pseudocódigo)

```
INICIO
  SYSTEM recibe credenciales de sesión del usuario institucional
  SYSTEM verifica rol y alcance del usuario
  USER ingresa RUN y número de documento del apoderado/estudiante
  SYSTEM valida formato si aplica según reglas institucionales
  IF RUN + documento ya existe Y la política lo exige
    SYSTEM indica registro existente o aviso de duplicado
    USER decide continuar o corregir
  ELSE
    SYSTEM crea o recupera registro según estado institucional
    USER completa datos necesarios de ficha y vínculo
    IF la institución requiere salud o PIE
      USER registra información autorizada
    SYSTEM guarda como transacción coherente
    SYSTEM registra acción en auditoría
    SYSTEM actualiza resumen del dashboard si aplica
    IF el flujo exige aprobación
      SYSTEM asigna estado pendiente/aprobado según rol aprobador
    SYSTEM genera comprobante si corresponde
  FIN
```

---

## 6. Cuestionario de levantamiento de requisitos

Este cuestionario es de uso interno para la escuela y la secretaría; sus respuestas definen reglas de negocio pendientes y se convierten en requisitos con trazabilidad.

1. ¿Qué campos se consideran obligatorios en el registro de un apoderado y en la ficha del estudiante, y qué campos son opcionales o internos?
2. ¿Cómo se debe manejar un RUN o número de documento que aparezca más de una vez en el registro, y quién decide si es el mismo o no?
3. ¿Qué roles reales existen en la escuela para ingresar, aprobar o consultar matrículas y fichas, y qué puede hacer cada uno exactamente?
4. ¿Cuál es el flujo institucional de aprobación: quién aprueba, en qué orden y qué estado sigue a cada aprobación o rechazo?
5. ¿Qué información de salud o PIE debe registrarse, quién puede verla y quién puede modificarla?
6. ¿Cuántos cupos o vacantes tiene cada curso o sección, y cómo se comporta el sistema cuando se llena o cuando se retira un estudiante?
7. ¿Qué debe aparecer obligatoriamente en el comprobante PDF y qué contenido debe llevar el código QR según la institución?
8. ¿Qué columnas y filtros debe tener la lista de curso en Excel y en PDF, y qué datos no deben exportarse?
9. ¿Cómo se establece legal/institucionalmente la relación de apoderado con el estudiante, y qué se debe registrar como evidencia de esa relación?
10. ¿Cuál es la política de retención, respaldo y recuperación de registros, y durante cuánto tiempo deben conservarse matrículas, fichas y exportaciones?

### 7. Preguntas pendientes por definir con la escuela y la secretaría

#### Proceso actual
- ¿Cómo se matricula hoy un alumno, paso a paso, y cuánto demora?
- ¿Dónde se guarda la información (papel, Excel) y quién la mantiene?
- ¿Qué errores o demoras son los más frecuentes?

#### Volumen
- ¿Cuántos alumnos y cursos hay?
- ¿Cuántas matrículas se procesan en el peak de matrícula y cuántos usuarios usarían el sistema a la vez?

#### Reportes
- ¿Qué reportes arma hoy la secretaría o dirección y con qué frecuencia?
- ¿Alguno es obligatorio para el Ministerio?

#### Pruebas
- ¿Quién va a validar el sistema por parte de la escuela?
- ¿Hay datos de ejemplo reales o anonimizados para probar?

---
