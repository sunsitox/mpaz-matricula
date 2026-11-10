# Base de datos

Una carpeta por módulo. Lo compartido vive en `comun/`; cada módulo tiene su
propia carpeta en `modulos/` con la misma estructura interna.

```text
database/
  aplicar.sh            Ejecutor: aplica comun/ + los módulos indicados
                        (--modulo repetible, --todos para todos, --neon
                        para Neon). Identity (02) corre al final.
  CONEXION.md           Cómo se conecta el portal.
  DESPLIEGUE.md         Cómo aplicar cada recorrido.
  comun/                Rol de aplicación e Identity (compartidos).
    pasos/              00-rol-aplicacion.sql, 02-identidad.sql
  modulos/
    matricula/          Esquema de matrícula (implementado).
      instalacion/      01-estructura, 02-PL, 03-datos. Base nueva, en orden.
      pasos/            01, 03, 04, 05, 06, 07. Fuente de mantenimiento.
      datos-prueba/     Sólo desarrollo: mocks y restablecer-a-usuarios.sql.
    asistencia/         Esquema de asistencia (semilla DDL, paso 10).
    _plantilla-modulo/  Esqueleto para copiar cuando se defina un módulo nuevo.
```

Reglas:

- `instalacion/` se genera desde `pasos/`: si cambia un paso, actualizar la
  parte consolidada correspondiente. No ejecutar ambos recorridos sobre la
  misma base.
- `datos-prueba/` nunca va a producción (el release la excluye y falla si
  encuentra mocks).
- Los archivos de `instalacion/` son SQL PostgreSQL sin variables ni
  metacomandos de `psql`.
- Los pasos de cada módulo se numeran desde `10` para no chocar con los de
  `comun/` (`00`, `02`). Matrícula es la excepción histórica (`01`–`07`).
