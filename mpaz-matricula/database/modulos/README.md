# Módulos

Cada módulo tiene su carpeta con la misma estructura:

```text
<modulo>/
  instalacion/    Recorrido consolidado para bases nuevas (en orden).
  pasos/          Fuente de mantenimiento, numerada desde 10.
  mantenimiento/  Migraciones de bases antiguas (90+).
  datos-prueba/   Sólo desarrollo. Nunca en producción.
```

| Módulo | Estado |
|---|---|
| `matricula/` | Implementado (esquema `mpaz_matricula`). |
| `asistencia/` | Semilla DDL (esquema `mpaz_asistencia`, paso 10): 11 tablas + 9 enums traídos de la base `mpaz_asistencia` de Neon. Sin datos de prueba aún. |
| `_plantilla-modulo/` | Esqueleto para copiar. No es un módulo real. |

Módulos futuros (notas, apoyo, …) aún por definir con el equipo:
cuando se definan, copiar `_plantilla-modulo/` con el nombre del módulo y
documentar aquí su esquema y su estado.

El ejecutor raíz aplica `comun/` + un módulo:

```bash
./aplicar.sh <base> --modulo matricula --clave-app '<clave>'
```
