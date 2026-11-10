# Módulo Matrícula

Esquema `mpaz_matricula`: estudiantes, personas y relaciones, matrícula anual,
ficha por pasos, apoderados, emergencia, retiro, salud, PIE, autorizaciones,
documentos y auditoría. Detalle de tablas en `CONEXION.md` (raíz de `database/`).

| Carpeta | Contenido | Cuándo se usa |
|---|---|---|
| `instalacion/` | `01-estructura-bd.sql` → `02-programacion-pl.sql` → `03-datos-iniciales.sql`. Instalación nueva en ese orden, sobre una base vacía. | Crear la base desde cero |
| `pasos/` | Scripts numerados `01`, `03`–`07` (la fuente de mantenimiento; `00` y `02` viven en `comun/` y el ejecutor corre 02 al final). | Cambios paso a paso o vía ejecutor |
| `datos-prueba/` | Sólo desarrollo: `restablecer-a-usuarios.sql` (vacía el movimiento y deja sólo usuarios) y los mocks de ejemplo. | Probar sin datos reales |

Si cambia un script de `pasos/`, actualizar la parte consolidada
correspondiente en `instalacion/` (son dos vistas del mismo contenido).

Documentación: `DESPLIEGUE.md` (cómo aplicar) y `CONEXION.md` (cómo se conecta
el portal), en la raíz de `database/`.
