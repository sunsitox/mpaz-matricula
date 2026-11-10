# Base de datos

| Carpeta | Contenido | Cuándo se usa |
|---|---|---|
| `instalacion/` | `01-estructura-bd.sql` → `02-programacion-pl.sql` → `03-datos-iniciales.sql`. Instalación nueva en ese orden, sobre una base vacía. | Crear la base desde cero |
| `pasos/` | Scripts numerados `00`–`06` (fuente de mantenimiento) + `aplicar.sh` en la raíz que los ejecuta. | Cambios paso a paso o vía ejecutor |
| `mantenimiento/` | `90-migrar-bases-existentes.sql`: pasa una base antigua al esquema `mpaz_matricula` sin perder datos. | Bases que ya existen |
| `datos-prueba/` | Sólo desarrollo: `restablecer-a-usuarios.sql` (vacía el movimiento y deja sólo usuarios) y los mocks de ejemplo. | Probar sin datos reales |

Documentación: `DESPLIEGUE.md` (cómo aplicar) y `CONEXION.md` (cómo se conecta el portal).

Reglas:

- `instalacion/` se genera desde `pasos/`: si cambia un paso, actualizar la parte consolidada correspondiente. No ejecutar ambos recorridos sobre la misma base.
- `datos-prueba/` nunca va a producción (el release la excluye y falla si encuentra mocks).
- Los tres archivos de `instalacion/` son SQL PostgreSQL sin variables ni metacomandos de `psql`.
