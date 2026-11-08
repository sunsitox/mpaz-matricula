# Despliegue del esquema

Los scripts de esta carpeta se aplican **en orden numérico**. El prefijo indica
el paso; no hay que decidir cuál va primero.

| Paso | Archivo | Qué hace | Obligatorio |
|---|---|---|---|
| 00 | `00-rol-aplicacion.sql` | Crea el rol `mpaz_app` y deja el `search_path` de la base en `mpaz_matricula, public`. | Sí |
| 01 | `01-ddl-institucional.sql` | Crea el esquema `mpaz_matricula` con las 27 tablas, funciones, vistas y triggers del DDL. | Sí |
| 02 | `02-identidad.sql` | Crea las 7 tablas de ASP.NET Identity y el historial de migraciones en `mpaz_matricula`. | Sí, salvo que se use el bundle de EF Core |
| 03 | `03-permisos-iniciales.sql` | Carga la matriz propuesta de `rol_permiso`. | Sólo con aprobación del colegio |
| 04 | `04-privilegios-aplicacion.sql` | Otorga los privilegios mínimos del rol `mpaz_app`. | Sí |
| 05 | `05-anio-escolar.sql` | Crea `anio_escolar` (un solo año activo para matrícula) y activa el año más reciente con matrículas. | Sí |
| 90 | `90-migrar-bases-existentes.sql` | Pasa una base que ya existe a `mpaz_matricula` (mueve desde `public`/`auth`). | Sólo bases antiguas |

Los datos de prueba están en `datos-prueba/` y **no** se aplican en producción.

## Instalación nueva

Con el script único:

```bash
cd mpaz-matricula/database

PGPASSWORD='<clave admin>' ./aplicar.sh mpaz_matricula \
  --clave-app '<clave de mpaz_app>' \
  --con-permisos          # omitir hasta que el colegio apruebe la matriz
```

Equivale a:

```bash
psql -d mpaz_matricula -v clave_app='<clave>' -f 00-rol-aplicacion.sql
psql -d mpaz_matricula -f 01-ddl-institucional.sql
psql -d mpaz_matricula -f 02-identidad.sql
psql -d mpaz_matricula -f 03-permisos-iniciales.sql   # opcional
psql -d mpaz_matricula -f 04-privilegios-aplicacion.sql
psql -d mpaz_matricula -f 05-anio-escolar.sql
```

El año con `activo_matricula = true` es el único que usa el módulo de Matrícula.
Se cambia desde **Configuración → Año escolar** (Secretaría o Dirección), no por
fecha del sistema.

Si las tablas de Identity se aplican con el bundle de EF Core
(`efbundle --connection '...'`), usar `--sin-identidad` para no duplicar el paso.

## Base que ya existe

```bash
./aplicar.sh mpaz_matricula --migrar
```

`90-migrar-bases-existentes.sql` es idempotente, conserva los datos y mueve
tablas, secuencias, vistas, funciones y el historial de migraciones. Si la base
venía con el esquema `matricula`, lo renombra.

## Orden en el artefacto de despliegue

`deploy/build-release.sh` copia esta carpeta completa (sin `datos-prueba/`) en
`database/` del release, así que en el servidor los pasos quedan disponibles con
la misma numeración:

```bash
/opt/mpaz-matriculas/releases/<release>/database/aplicar.sh mpaz_matricula ...
```
