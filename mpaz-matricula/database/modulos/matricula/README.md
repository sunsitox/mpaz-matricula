# Módulo Matrícula

Esquema `mpaz_matricula`: estudiantes, personas y relaciones, matrícula anual,
ficha por pasos, apoderados, emergencia, retiro, salud, PIE, autorizaciones,
documentos y auditoría. Detalle de tablas en `CONEXION.md` (raíz de `database/`).

| Carpeta | Contenido | Cuándo se usa |
|---|---|---|
| `instalacion/` | `01-estructura-bd.sql` → `02-programacion-pl.sql` → `03-datos-iniciales.sql`. Instalación nueva en ese orden, sobre una base vacía. | Crear la base desde cero |
| `pasos/` | Scripts numerados `01`, `03`–`06` (la fuente de mantenimiento; `00` y `02` viven en `comun/`). | Cambios paso a paso o vía ejecutor |
| `mantenimiento/` | `90-migrar-bases-existentes.sql`: pasa una base antigua al esquema `mpaz_matricula` sin perder datos. | Bases que ya existen |
| `datos-prueba/` | Sólo desarrollo: `restablecer-a-usuarios.sql` (vacía el movimiento y deja sólo usuarios) y los mocks de ejemplo. | Probar sin datos reales |

Documentación: `DESPLIEGUE.md` (cómo aplicar) y `CONEXION.md` (cómo se conecta
el portal), en la raíz de `database/`.
