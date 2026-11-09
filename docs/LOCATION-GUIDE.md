# Guía de ubicación del proyecto

Esta guía explica qué va en cada carpeta del repo para mantener el proyecto ordenado y separar lo que es el sistema de lo que es documentación o scripts de ingeniería.

## Estructura general

```
<raíz del repo>/
├── mpaz-matricula/   # Sistema completo (solución + proyectos .NET)
├── docs/             # Documentación general del proyecto
└── scripts/          # Scripts de ingeniería / setup / automatización
```

---

## mpaz-matricula/

**Propósito:** Contiene todo el sistema de MarcelaPaz.Matriculas (solución y 4 proyectos .NET).

**Qué ahí va:**
- `MarcelaPaz.Matriculas.slnx` (o `.sln`) — archivo de solución de Visual Studio / .NET CLI
- `MarcelaPaz.Domain/` — Capa de dominio (reglas de negocio, entidades, interfaces del dominio, value objects, excepciones del dominio)
- `MarcelaPaz.Application/` — Capa de aplicación (casos de uso, comandos/queries, DTOs, validaciones, mappings, interfaces de servicios de aplicación)
- `MarcelaPaz.Infrastructure/` — Capa de infraestructura (DbContext, repositorios, migrations, Identity, servicios externos, DI desde esta capa, configuración)
- `MarcelaPaz.Web/` — Capa de presentación / API (controllers/endpoints, middleware, filters, hosting, configuración de entrada, DI final, wwwroot, properties/launchSettings)

**Por qué:** Separar el proyecto de la documentación y los scripts hace que la raíz no se “ensucie” con binarios, configuraciones o documentos operadales; y permite que el sistema sea fácil de abrir desde Visual Studio o cualquier IDE sin ruido extra.

**Qué NO va aquí:**
- Documentación operacional (manuales, decisiones, fe de erratas) → va en `docs/`
- Scripts de setup, generación o CI → van en `scripts/`
- Archivos temporales de la IDE (`.vs/`, `.idea/`, etc.) → deben quedar fuera o ignorarse en `.gitignore` según convención

---

## docs/

**Propósito:** Documentación no código del proyecto: decisiones, manuales, análisis, planes, notas de arquitectura, guías de uso interno.

**Qué ahí va:**
- `systema-matricula-tasks.md` — desglose de tareas del sistema de matrícula escolar
- Otros documentos: decisiones de diseño (ADR), planes de despliegue, notas de auditoría, manual de operación, diseño de BD, etc.

**Por qué:** La documentación suele ser de lectura más que de ejecución; separarla del código ayuda a mantener el repo enfocado y la documentación accesible sin mezclar con rutas que el sistema espera en tiempo de ejecución.

**Qué NO va aquí:**
- Código fuente, scripts ejecutables, configuraciones de tiempo de ejecución, binarios, paquetes → esos van en `mpaz-matricula/` o `scripts/` según corresponda

---

## scripts/

**Propósito:** Scripts de ingeniería, setup, generación de solución, automatización local, utilidades de desarrollo.

**Qué ahí va:**
- `setup-marcela-paz-matriculas.bat` — script para crear la solución y los 4 proyectos .NET, referencias y carpetas sugeridas
- Otros posibles: scripts de generación de migraciones, scripts de seed de datos, scripts de respaldo, etc., si se necesitan localmente

**Por qué:** Separar scripts del proyecto evita que archivos de utilidad se mezclen con la lógica del sistema y facilita identificar qué es “generación/setup” vs. “aplicación”.

---

## Convención adoptada

- Separación simple en la raíz: `mpaz-matricula/`, `docs/`, `scripts/`
- El sistema completo vive en `mpaz-matricula/`
- Todo lo que no sea el sistema (documentación, scripts, notas) vive fuera de `mpaz-matricula/` según su naturaleza
- Si en el futuro se agrega una carpeta `solution/` o `src/`, esta guía se actualiza para reflejar la nueva convención

---

## Ejemplos de uso común

- Para abrir el sistema en Visual Studio / VS Code: abrir `mpaz-matricula/MarcelaPaz.Matriculas.slnx`
- Para ejecutar la API desde CLI: `cd mpaz-matricula/MarcelaPaz.Web && dotnet run`
- Para regenerar la solución desde cero (Windows): `scripts/setup-marcela-paz-matriculas.bat`
- Para leer la planificación del sistema: leer `docs/systema-matricula-tasks.md`
