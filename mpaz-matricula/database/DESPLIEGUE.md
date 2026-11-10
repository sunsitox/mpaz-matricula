# Despliegue del esquema

`aplicar.sh` aplica `comun/pasos/00` (rol), los pasos de cada módulo indicado
(`--modulo`, repetible; `--todos` para todos; por defecto `matricula`) en
orden numérico, y al final `comun/pasos/02` (Identity). El prefijo indica el
paso; no hay que decidir cuál va primero. El orden importa: 02 corre último
porque otros pasos pre-sellan el historial de migraciones de EF.

Con `--neon` se adapta a Neon (sin `CREATE ROLE`: omite 00 y 04 y deja el
`search_path` por defecto en la base).

| Paso | Archivo | Qué hace | Obligatorio |
|---|---|---|---|
| 00 | `comun/pasos/00-rol-aplicacion.sql` | Crea el rol `mpaz_app` y deja el `search_path` de la base en `mpaz_matricula, public`. | Sí (se omite con `--neon`) |
| 01 | `modulos/matricula/pasos/01-ddl-institucional.sql` | Crea el esquema `mpaz_matricula` con las 27 tablas, funciones, vistas y triggers del DDL. | Sí |
| 03 | `modulos/matricula/pasos/03-permisos-iniciales.sql` | Carga la matriz propuesta de `rol_permiso`. | Sólo con `--con-permisos` y aprobación del colegio |
| 04 | `modulos/matricula/pasos/04-privilegios-aplicacion.sql` | Otorga los privilegios mínimos del rol `mpaz_app`. | Sí (se omite con `--neon`) |
| 05 | `modulos/matricula/pasos/05-anio-escolar.sql` | Crea `anio_escolar` (un solo año activo para matrícula) y activa el año más reciente con matrículas. | Sí |
| 06 | `modulos/matricula/pasos/06-validacion-matricula.sql` | Agrega fecha y responsable de validación institucional. Trae su propio `SET search_path`. | Sí |
| 07 | `modulos/matricula/pasos/07-search-path-funciones.sql` | Fija el `search_path` dentro de cada función, para que los triggers no dependan de la sesión que los dispara. Re-ejecutable. | Sí |
| 08 | `modulos/matricula/pasos/08-certificados-emitidos.sql` | Crea `certificado_emitido`: folio único y hash del código por cada certificado de alumno regular emitido. Tabla nueva, re-ejecutable. | Sí |
| 10 | `modulos/asistencia/pasos/10-ddl-asistencia.sql` | Crea el esquema `mpaz_asistencia` (11 tablas + 9 enums del equipo de asistencia). | Sí, si se usa ese módulo |
| 02 | `comun/pasos/02-identidad.sql` | Crea las 7 tablas de ASP.NET Identity y el historial de migraciones en `mpaz_matricula`. Corre ÚLTIMO. | Sí, salvo que se use el bundle de EF Core |

Los datos de prueba están en `modulos/matricula/datos-prueba/` y **no** se
aplican en producción. `restablecer-a-usuarios.sql` vacía todo el movimiento
y deja sólo usuarios (funcionarios, sus roles y cuentas): sin fichas ni nada
más. Después hay que reconfigurar año escolar, cursos y plantillas desde el
portal.

## Instalación nueva: tres partes separadas

Crear una base vacía desde el administrador de PostgreSQL y conectarse a ella.
Ejecutar completos, en este orden, usando pgAdmin, otro cliente PostgreSQL o psql:

| Orden | Archivo | Contenido |
|---|---|---|
| 1 · BD | `modulos/matricula/instalacion/01-estructura-bd.sql` | Esquema, extensión pg_trgm, tablas institucionales e Identity, claves, índices, vistas y comentarios. Sin carga de datos. |
| 2 · PL | `modulos/matricula/instalacion/02-programacion-pl.sql` | Funciones PL/pgSQL, triggers, validaciones RUN dependientes de funciones y seguridad del rol de aplicación. |
| 3 · Datos | `modulos/matricula/instalacion/03-datos-iniciales.sql` | Perfiles institucionales, historial técnico EF Core y recuperación de años existentes. Sin estudiantes ni cuentas. |

```bash
psql -X -v ON_ERROR_STOP=1 -U postgres -d mpaz_matricula -f modulos/matricula/instalacion/01-estructura-bd.sql
psql -X -v ON_ERROR_STOP=1 -U postgres -d mpaz_matricula -f modulos/matricula/instalacion/02-programacion-pl.sql
psql -X -v ON_ERROR_STOP=1 -U postgres -d mpaz_matricula -f modulos/matricula/instalacion/03-datos-iniciales.sql
```

Los tres archivos son SQL PostgreSQL sin variables ni metacomandos de psql.
No crean ni cambian automáticamente la conexión de base. La estructura no es
una migración: usar sólo sobre una base vacía. No ejecutar la app hasta terminar
las tres partes, ya que PL agrega restricciones que dependen de funciones.
Cada parte usa su propia transacción; si una falla, detenerse y revisar el error.

PL requiere permisos administrativos. Crea `mpaz_app` sin contraseña si no existe;
configurar su credencial por un canal seguro antes de iniciar el portal. Si existe,
conserva su contraseña. No se guardan claves en los archivos.

En Datos, la matriz de permisos está desactivada con `IF FALSE THEN`: cambiar
únicamente ese valor a TRUE cuando el colegio la apruebe. El año escolar no se
fija por calendario: configúrelo desde el portal. Los datos ficticios siguen
separados en `modulos/matricula/datos-prueba/` y no se ejecutan en producción.

Estos archivos son para PostgreSQL: dbdiagram puede no admitir vistas, índices
especiales, extensión o restricciones. PL y Datos no se importan como diagrama.
Para una representación exacta en dbdiagram se requiere DBML aparte.

Los scripts de `pasos/` son la fuente de mantenimiento. Si cambian, actualice
la parte consolidada correspondiente en `instalacion/`. No ejecutar ambos
recorridos sobre la misma instalación.

### Alternativa: aplicar por pasos a una base vacía ya creada

Con el ejecutor existente:

```bash
cd mpaz-matricula/database

PGPASSWORD='<clave admin>' ./aplicar.sh mpaz_matricula \
  --clave-app '<clave de mpaz_app>' \
  --con-permisos          # omitir hasta que el colegio apruebe la matriz
```

Equivale a `comun/pasos/00`, después en orden `modulos/matricula/pasos/01`,
`03` (opcional), `04`, `05`, `06` y `07`, y al final `comun/pasos/02`.

El año con `activo_matricula = true` es el único que usa el módulo de Matrícula.
Se cambia desde **Configuración → Año escolar** (Secretaría o Dirección), no por
fecha del sistema.

Si las tablas de Identity se aplican con el bundle de EF Core
(`efbundle --connection '...'`), usar `--sin-identidad` para no duplicar el paso.

Para varios módulos en la misma base:

```bash
./aplicar.sh mpaz --todos --neon --clave-app '...'     # todo Neon
./aplicar.sh mpaz --modulo asistencia --modulo notas    # sólo algunos
```

## Orden en el artefacto de despliegue

`deploy/build-release.sh` copia `comun/` y todos los módulos (sólo
`instalacion/` y `pasos/`, sin `datos-prueba/` ni la plantilla) en `database/`
del release, así que en el servidor los pasos quedan disponibles con la misma
numeración:

```bash
/opt/mpaz-matriculas/releases/<release>/database/aplicar.sh mpaz_matricula ...
```

## Neon (una base, un esquema por módulo)

Proyecto `mpaz-database`, rama principal. Base única `mpaz`: cada módulo
vive en su esquema y los JOIN entre módulos funcionan directo.

| Esquema (en `mpaz`) | Contenido | Cómo se creó |
|---|---|---|
| `mpaz_matricula` | Dominio (01, 05, 06, 07) + Identity (7 tablas `AspNet*`, vía `comun/02`) + datos del desarrollo (889 filas, año 2027 activo) + 03 | `CREATE DATABASE mpaz` + pasos, migración con triggers de usuario pausados |
| `mpaz_asistencia` | 11 tablas + 9 enums + `ck_alumno_en_curso`, vacío (sin datos que migrar) | Volcado de esquema desde la base `mpaz_asistencia`; guardado como `modulos/asistencia/pasos/10-ddl-asistencia.sql` |

Adaptaciones respecto al recorrido local (Neon no permite `CREATE ROLE` por
SQL y el dueño `neondb_owner` ya tiene todos los derechos sobre sus bases):

- Se omite `comun/pasos/00-rol-aplicacion.sql` (no hay rol `mpaz_app` en Neon).
  En su lugar: `ALTER DATABASE mpaz SET search_path = mpaz_matricula, public`.
- Se omite `modulos/matricula/pasos/04-privilegios-aplicacion.sql` (otorga
  sobre `mpaz_app`); sin ese rol no aplica.
- Se omite `03-permisos-iniciales.sql` hasta que el colegio apruebe la matriz
  (igual que en local).
- `pg_trgm` se instaló con `CREATE EXTENSION` explícito.
- Las bases antiguas (`mpaz_matricula`, `mpaz_auth`, `mpaz_asistencia`) ya se
  eliminaron tras verificar el portal contra `mpaz`.

Las aplicaciones se conectan con el rol `neondb_owner` con una sola cadena:
`Default` (base `mpaz`); `Auth` apunta a la misma base y existe por si algún
despliegue separa las cuentas. Las claves van en user-secrets en desarrollo
(`ConnectionStrings:Default`, `ConnectionStrings:Auth`) y en variables de
entorno en el servidor; nunca en archivos versionados. Se aceptan en formato
palabra-clave de Npgsql (`Host=...;Database=...`) o como URI tal como la
entrega Neon (`postgresql://...`); `AddPersistencia` normaliza la URI sola,
porque Npgsql no la entiende directo. Compartir las claves de DataProtection
entre sistemas es lo que falta para el login único (misma cookie válida en
todos).
