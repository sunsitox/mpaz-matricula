# Análisis, Diseño y Desarrollo: Sistema Web de Matrícula Escolar
**Institución:** Escuela Básica Escritora Marcela Paz  
**Estado:** plan preliminar anterior a la [reunión 1](reunion-01-2026-10-21.md); las casillas no son compromisos aprobados. Contrastar con el acta de constitución.  
**Stack conversado el 21-10-2026:** ASP.NET Core (Clean Architecture), interfaz Blazor o Razor Pages por decidir, PostgreSQL y servidor local Ubuntu. SQL Server, SPA y nube no forman parte de esa propuesta.

## Repriorización pendiente tras reunión 1

- **Primera entrega propuesta:** partir del padrón de estudiantes ya inscritos mediante proceso externo, verificar apoderados autorizados y digitalizar/actualizar datos en un wizard con persistencia por etapa; definir controles adicionales de identidad antes de habilitar consulta sensible o edición. No implementar el acceso basado solo en RUT + año de nacimiento como si fuera seguro.
- **Por revalidar:** altas de estudiantes, admisión, cupos, aprobación institucional, dashboard, QR, Excel y reportes; estaban en el plan previo pero no se confirmaron en la reunión.
- **Proyección futura:** gestión centralizada por Secretaría y PDFs (comprobante/certificado); priorizar únicamente si el acta confirma su inclusión.
- Mantener los ítems históricos de abajo como **backlog candidato**, no como secuencia autorizada de entrega. Los objetivos numéricos y requisitos legales deben validarse con la institución.

## Desglose detallado de tareas (backlog previo, sujeto a revisión)

### 1. Análisis de Procesos y Requisitos Institucionales
- [ ] **Levantamiento de información con la Escuela Marcela Paz**:
    - [ ] Entrevistar al personal administrativo y directivo para documentar el flujo actual de matrícula escolar (fichas en papel, libros de registro, plazos ministeriales).
    - [ ] Identificar los requisitos específicos para estudiantes prioritarios o preferentes según la normativa chilena (Ley SEP).
- [ ] **Definición de requerimientos funcionales**:
    - [ ] Registro de nuevo estudiante (antecedentes personales, médicos, de residencia y socioeconómicos).
    - [ ] Asociación de uno o más apoderados (titular y suplente) con validación de RUN chileno.
    - [ ] Consulta y filtrado de estudiantes por curso, letra, RUN o apellido.
- [ ] **Definición de requerimientos no funcionales**:
    - [ ] Cumplimiento de normativas de protección de datos personales (Ley 19.628).
    - [ ] Tiempos de respuesta menores a 2 segundos en búsquedas masivas.
- [ ] **Mapeo de roles y permisos**:
    - [ ] Administrador (Dirección / UTP): Control total del sistema, configuración de cursos y años académicos.
    - [ ] Secretaría / Administrativo: Gestión de inscripciones, edición de datos y emisión de comprobantes.
    - [ ] Apoderado (Portal de consultas/actualización): Vista de la ficha del estudiante a su cargo.

### 2. Diseño de Arquitectura en .NET (Clean Architecture / Capas)
- [ ] **Configuración inicial de la Solución (`.NET 8/9`)**:
    - [ ] Crear el proyecto base dividiendo en capas: `Core/Domain`, `Application`, `Infrastructure` y `Web/API`.
    - [ ] Configurar inyección de dependencias nativa de .NET.
- [ ] **Diseño de Endpoints y Contratos**:
    - [ ] Definir los controladores o Minimal APIs RESTful para estudiantes, apoderados, matrículas y reportes.
    - [ ] Implementar patrones DTO (Data Transfer Objects) para desacoplar las entidades de la base de datos de las peticiones HTTP.

### 3. Diseño e Implementación de Base de Datos (Entity Framework Core)
- [ ] **Modelado Entidad-Relación (DER)**:
    - [ ] Tabla `Estudiantes` (ID, RUN, Nombres, Apellidos, FechaNacimiento, Direccion, Comuna, Alergias/CondicionesMedicas).
    - [ ] Tabla `Apoderados` (ID, RUN, Nombres, Apellidos, Telefono, Correo, Parentesco).
    - [ ] Tabla intermedia `EstudianteApoderado` (Relación N:M con indicador de apoderado titular/suplente).
    - [ ] Tabla `Cursos` (ID, Nivel/Grado ej. 1° Básico, Letra ej. A, AñoLectivo, CapacidadMaxima).
    - [ ] Tabla `Matriculas` (ID, EstudianteID, CursoID, FechaMatricula, Estado: *Matriculado, Retirado, Pendiente*, Observaciones).
    - [ ] Tabla `AuditoriaLogs` (ID, UsuarioID, Accion, Detalle, FechaHora).
- [ ] **Configuración de EF Core**:
    - [ ] Escribir clases `DbContext` y configurar restricciones de unicidad (ej. RUN único).
    - [ ] Configurar índices por RUN y Apellidos para optimizar búsquedas rápidas.
    - [ ] Ejecutar migraciones iniciales (`dotnet ef migrations add InitialCreate`) y aplicar a la base de datos (PostgreSQL/SQL Server).

### 4. Desarrollo del Backend en ASP.NET Core (C#)
- [ ] **Implementación de Servicios de Negocio (`Application Layer`)**:
    - [ ] `EstudianteService`: Lógica para validar edad según el curso, evitar duplicados por RUN y registrar altas.
    - [ ] `MatriculaService`: Validar disponibilidad de cupos en el curso antes de confirmar la matrícula.
    - [ ] `ReporteService`: Lógica para consolidar estadísticas de matrícula por género, curso y situación socioeconómica.
- [ ] **Validaciones y Manejo de Errores**:
    - [ ] Implementar validaciones de formato de RUN chileno (dígito verificador) mediante atributos personalizados o FluentValidation.
    - [ ] Configurar un middleware global de manejo de excepciones para capturar errores y retornar códigos HTTP adecuados (`400`, `404`, `500`).

### 5. Seguridad, Autenticación y Control de Acceso (ASP.NET Core Identity)
- [ ] **Configuración de Identidad**:
    - [ ] Integrar `ASP.NET Core Identity` para la gestión de usuarios administrativos.
    - [ ] Configurar políticas de contraseñas robustas.
- [ ] **Autorización basada en Roles (RBAC)**:
    - [ ] Crear los roles de sistema (`Admin`, `Secretaria`, `Apoderado`).
    - [ ] Proteger controladores y endpoints mediante atributos `[Authorize(Roles = "Admin,Secretaria")]`.
- [ ] **Seguridad Web**:
    - [ ] Configurar protección contra ataques CSRF, XSS e Inyección SQL (mitigado nativamente por el uso de consultas parametrizadas en EF Core).
    - [ ] Configuración segura de Cookies de sesión o tokens JWT según el tipo de cliente frontend elegido.

### 6. Respaldos, Logs y Auditoría
- [ ] **Sistema de Auditoría**:
    - [ ] Crear un interceptor en EF Core o un servicio dedicado para registrar automáticamente qué usuario modificó o creó un registro crítico.
- [ ] **Políticas de Respaldo**:
    - [ ] Diseñar script automatizado (ej. `pg_dump` o respaldos de SQL Server) programado mediante tareas del sistema operativo para respaldar la base de datos diariamente.
    - [ ] Documentar el procedimiento de restauración ante fallos (Disaster Recovery).

### 7. Pruebas y Control de Calidad (QA)
- [ ] **Pruebas Unitarias (`xUnit` / `NUnit`)**:
    - [ ] Escribir pruebas unitarias para validar la lógica de negocio crítica (ej. validación de cupos y cálculo de edades).
- [ ] **Pruebas de Integración**:
    - [ ] Probar el flujo completo de una petición HTTP contra los endpoints usando `WebApplicationFactory`.
- [ ] **Pruebas de Usuario (UAT)**:
    - [ ] Realizar una marcha blanca interna simulando el proceso de matrícula real de la Escuela Básica Escritora Marcela Paz con datos de prueba.

### 8. Optimización de Procesos Administrativos y Reportes
- [ ] **Módulo de Generación de Reportes**:
    - [ ] Implementar exportación de listas de curso a formatos estructurados (Excel usando librerías como ClosedXML o EPPlus).
    - [ ] Generar el comprobante oficial de matrícula en formato descargable PDF para los apoderados.
- [ ] **Optimización de Consultas**:
    - [ ] Aplicar `.AsNoTracking()` en consultas de sólo lectura en EF Core.
    - [ ] Asegurar el uso correcto de `.Include()` para evitar problemas de consultas N+1 al traer relaciones de estudiantes y apoderados.

### 9. Interfaz de Usuario / Vistas (Frontend)
- [ ] **Desarrollo de Vistas (Razor Pages / Blazor o SPA)**:
    - [ ] Pantalla de Login institucional con branding de la Escuela Básica Escritora Marcela Paz.
    - [ ] Dashboard administrativo con contadores rápidos (total matriculados, cupos disponibles por curso).
    - [ ] Formulario dinámico de inscripción de matrícula con validaciones en tiempo real de campos obligatorios.
    - [ ] Módulo de búsqueda avanzada con filtros por RUN, nombres y niveles.

### 10. Despliegue, Documentación y Entrega
- [ ] **Preparación para Producción**:
    - [ ] Configurar perfiles de entorno (`appsettings.Production.json`) para cadenas de conexión y secretos.
    - [ ] Publicar la aplicación en un servidor interno o nube utilizando Kestrel detrás de un proxy inverso (como Nginx o IIS).
- [ ] **Documentación Técnica y de Usuario**:
    - [ ] Redactar el manual de usuario paso a paso dirigido al personal administrativo de la escuela.
    - [ ] Documentar la arquitectura de la API y el esquema de la base de datos para futuros mantenimientos.

## Prioridad y orden de ejecución (histórico, pendiente de replanificación)

1. **Fase 1 (Fundación)**: Análisis de procesos, levantamiento en la Escuela Marcela Paz y diseño del modelo relacional.
2. **Fase 2 (Core)**: Configuración de la solución en capas con ASP.NET Core y Entity Framework Core.
3. **Fase 3 (Lógica y Seguridad)**: Desarrollo de servicios de negocio, validaciones de RUN y autenticación basada en Identity.
4. **Fase 4 (UI)**: Construcción de vistas e interfaces administrativas y de registro.
5. **Fase 5 (Optimización)**: Generación de reportes (PDF/Excel), auditoría y respaldos automáticos.
6. **Fase 6 (Entrega)**: Pruebas finales, documentación de usuario y despliegue en servidor.