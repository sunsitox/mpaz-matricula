# Entregables — Portal de Matrícula

Lista de entregables acordados. **Estado revisado el 16-11-2026.**

- [x] **Software (portal funcionando)**
    - [x] Portal compilando sin errores y pruebas pasando — solución `mpaz-matricula/MarcelaPaz.Matriculas.slnx` con batería xUnit en `tests/`; la compilación en `Release` se verificó el 08-11-2026 y no se re-ejecutó en esta revisión.
    - [x] Despliegue o instancia disponible para revisión del colegio — release en el servidor y `https://matricula.empaz.cl` viva (verificado 23-11-2026, responde login); falta app de Access (entra directo al login).
    - [ ] Datos iniciales cargados (año escolar, funcionarios, cursos) — **Parcial:** año escolar, funcionarios y los 20 cursos están cargados; falta la carga de alumnos reales (`limpiar-para-carga-manual.sql` está listo, sin ejecutar).
- [x] **Video demostrativo con recorrido automatizado**
    - [x] Guion del recorrido — casos 01 a 12 en `pruebas-visuales/README.md`.
    - [x] Datos de prueba ficticios, sin estudiantes reales.
    - [x] Recorrido ejecutado de corrido con cursor visible como un usuario — `pruebas-visuales/grabar-flujo.mjs`.
    - [x] Video final en WebM — `pruebas-visuales/evidencias/grabar-flujo/flujo-mpaz.webm`; queda la revisión final antes de entregar.
- [ ] **Manual de uso**
    - [ ] Alcance: Secretaría y Dirección (ingreso, matrícula por pasos, correcciones, documentos, autorizaciones, firma y confirmación).
    - [ ] Paso a paso con capturas de las pantallas reales.
    - [ ] Sección de errores frecuentes y cómo resolverlos.
- [ ] **Documentación técnica**
    - [x] Arquitectura de la solución (capas, proyectos, responsabilidades) — `documentacion/documentacion-tecnica/documentacion-tecnica.md`: vistas 4+1 en Mermaid (escenarios, lógica, procesos, desarrollo y física) más modelo de datos, estados, seguridad y operación, con los diagramas renderizados en `.svg` y `.png` (27-11-2026).
    - [ ] Base de datos: esquema, restricciones e índices relevantes — **Parcial:** `docs/diagrama-relacional.md` y el DDL comentado.
    - [ ] Permisos por rol y controles de seguridad aplicados — **Parcial:** propuesta en `database/modulos/matricula/pasos/03-permisos-iniciales.sql` y `docs/auditorias/01-auditoria-seguridad.md`.
    - [ ] Despliegue y configuración (entornos, respaldo, restauración) — **Parcial:** `docs/deployment.md` y `docs/migracion-servidor-runbook.md`, sin restauración probada.
