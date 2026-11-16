# Conexión a la base de datos

El portal usa PostgreSQL y EF Core (Npgsql). La cadena de conexión se entrega en
`Program.cs` a `AddPersistencia(...)`, que vive en `MarcelaPaz.Infrastructure`:
la capa Web no conoce el proveedor, sólo pasa la cadena. La base de trabajo es
`mpaz` en Neon, también al ejecutar el portal en el equipo de desarrollo.

## Dos caminos a la misma base

El portal intenta conectarse en este orden:

1. **Servicio web** — la cadena `ConnectionStrings:Default` tal como está
   configurada (en Neon, el host publicado con `-pooler`).
2. **Conexión directa a Neon** — el mismo host sin `-pooler`, que acepta las
   mismas credenciales y se deriva solo de la anterior.

La comprobación (`SELECT 1` con espera corta) ocurre una vez al arrancar y
queda en el registro: `Base de datos: endpoint del servicio web.` o
`Base de datos: conexión directa a Neon (el servicio web no respondió).` Si
ninguno responde, se conserva la conexión configurada y el portal muestra su
aviso al consultar.

Para usar otra conexión directa (otro host, un rol distinto), definir
`ConnectionStrings:Directa` en user-secrets o en el entorno; si se omite, se
deriva quitando `-pooler` al host. El código vive en
`MarcelaPaz.Infrastructure/Persistence/ConexionBaseDatos.cs`.

## Esquema dedicado

Todos los objetos del portal viven en el esquema **`mpaz_matricula`**:

| Qué | Dónde |
|---|---|
| Tablas del DDL institucional (`estudiante`, `matricula_anual`, …) | `mpaz_matricula` |
| Tablas de ASP.NET Identity (`"AspNetUsers"`, …) | `mpaz_matricula` |
| Historial de migraciones (`"__EFMigrationsHistory"`) | `mpaz_matricula` |
| Funciones, triggers y vistas (`normalizar_rut`, `v_pendientes_documentales`, …) | `mpaz_matricula` |
| Extensión `pg_trgm` | `public` (compartida con otras bases del servidor) |

Así esta base puede convivir con otras bases del colegio en el mismo servidor
sin que dos sistemas tengan, por ejemplo, una tabla `estudiante` distinta en el
mismo espacio de nombres. El esquema `auth` ya no se usa.

La cadena de conexión agrega `Search Path=mpaz_matricula,public` para que los
triggers y las vistas resuelvan los nombres sin calificar.

## Dónde está configurada

| Entorno | Mecanismo | Archivo |
|---|---|---|
| Desarrollo (Visual Studio, `dotnet run`) | Neon en `appsettings.Development.json`, credenciales completas en user-secrets | `mpaz-matricula/MarcelaPaz.Web/appsettings.Development.json` |
| Servidor | variable de entorno `ConnectionStrings__Default` | no hay archivo: la define el hosting |
| Sin conexión configurada | El arranque informa el error | No hay conexión local de respaldo |

ASP.NET Core aplica esta precedencia (de menor a mayor):

1. `appsettings.json`
2. `appsettings.{Entorno}.json` — acá está la cadena de desarrollo
3. User-secrets (sólo en `Development`)
4. Variables de entorno — acá debe ir la del servidor
5. Argumentos de línea de comandos

Por eso `appsettings.json` deja `ConnectionStrings:Default` vacío: es el valor
base, y cualquier entorno lo sobrescribe. En desarrollo los user-secrets
completan y sobrescriben la cadena del archivo; en el servidor (`Production`)
manda la variable de entorno.

## Desarrollo con Neon

```json
"ConnectionStrings": {
  "Default": "Host=ep-lucky-wildflower-be8llkch-pooler.c-9.us-east-2.aws.neon.tech;Port=5432;Database=mpaz;Username=neondb_owner;Ssl Mode=require;Search Path=mpaz_matricula,public"
}
```

La contraseña no está versionada: vive en user-secrets (`dotnet user-secrets set "ConnectionStrings:Default"
"<cadena>"`) y sobrescribe al archivo. Antes de publicar el repositorio,
verificar que ningún archivo versionado traiga una clave real.

`ConnectionStrings:Auth`, si está definido en user-secrets o en el entorno,
debe apuntar a la misma base `mpaz` en Neon: lo usan Identity y OpenIddict.
Si se omite, ambos utilizan `ConnectionStrings:Default`.

El script `scripts/probar-sso.py` también usa Neon. Antes de ejecutarlo, definir
`MPAZ_DB_CONEXION` con la URI PostgreSQL de esa misma base, además de las
variables de prueba indicadas en el script. La consulta de comparación abre
una conexión de sólo lectura. El portal puede seguir ejecutándose en localhost;
eso no implica usar PostgreSQL local.

## Instalación nueva

La instalación está dividida en tres partes, en
`database/modulos/matricula/instalacion/`:
`01-estructura-bd.sql` (estructura), `02-programacion-pl.sql` (funciones,
triggers y seguridad) y `03-datos-iniciales.sql` (datos iniciales). Crear
primero una base vacía y aplicarlos en ese orden, conectado a ella. Comandos
y precauciones en `database/DESPLIEGUE.md`. No utilizarlos para actualizar
una base existente.

Como alternativa, los scripts de `database/comun/pasos/` y de
`database/modulos/matricula/pasos/` están numerados y pueden aplicarse con el
ejecutor a una base vacía ya creada:

1. Crear la base vacía.
2. Aplicar la secuencia completa con un solo comando:

   ```bash
   cd mpaz-matricula/database
   PGPASSWORD='<clave admin>' ./aplicar.sh mpaz_matricula \
     --clave-app '<clave de mpaz_app>'
   ```

   Ejecuta `comun/pasos/00-rol-aplicacion.sql`,
   `modulos/matricula/pasos/01-ddl-institucional.sql`,
   `comun/pasos/02-identidad.sql` y
   `modulos/matricula/pasos/04-privilegios-aplicacion.sql`. Agregar
   `--con-permisos` sólo cuando el colegio apruebe la matriz.

3. Alternativa: si las tablas de Identity se aplican con el bundle de EF Core,
   usar `--sin-identidad` y ejecutar `efbundle` después del DDL.

## Base que ya existe

Las bases con el diseño anterior (`public`/`auth`, una por sistema) ya se
migraron y eliminaron. La ruta de migración (`90-migrar-bases-existentes.sql`)
se retiró del repositorio; su historial queda en git. Para mover datos basta
un volcado y carga entre esquemas:

```bash
# 1. Respaldo
pg_dump -Fc -d <base-origen> -f respaldo.dump

# 2. Cargar en la base destino (mismo esquema mpaz_matricula)
pg_restore -d <base-destino> respaldo.dump
```

## Servidor

1. Definir la variable de entorno antes de iniciar la aplicación:

   ```
   ConnectionStrings__Default = Host=<host-neon>;Port=5432;Database=mpaz;Username=<rol-aplicacion>;Password=<clave>;SSL Mode=Require;Search Path=mpaz_matricula,public
   ```

   El doble guion bajo `__` es la forma de escribir `ConnectionStrings:Default`
   en una variable de entorno.

2. No usar el superusuario `postgres` en el servidor: crear el rol de
   aplicación `mpaz_app` con permisos sólo sobre esta base y su esquema.

## Comprobar que la conexión funciona

```bash
dotnet ef database update --project mpaz-matricula/MarcelaPaz.Infrastructure \
  --startup-project mpaz-matricula/MarcelaPaz.Web --connection "<cadena>"
```

O iniciar la aplicación y abrir `/admin/funcionarios`: esa vista consulta
`mpaz_matricula.funcionario`, así que un 200 sin excepciones confirma la conexión.

Para verificar el esquema directamente:

```sql
SELECT table_schema, count(*)
FROM information_schema.tables
WHERE table_schema IN ('mpaz_matricula', 'public', 'auth')
GROUP BY 1;
```

Debe aparecer sólo `mpaz_matricula` con las tablas del portal.
