# Conexión a la base de datos

El portal usa PostgreSQL y EF Core (Npgsql). La cadena de conexión se entrega en
`Program.cs` a `AddPersistencia(...)`, que vive en `MarcelaPaz.Infrastructure`:
la capa Web no conoce el proveedor, sólo pasa la cadena.

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
| Desarrollo local (Visual Studio, `dotnet run`) | `appsettings.Development.json` | `mpaz-matricula/MarcelaPaz.Web/appsettings.Development.json` |
| Servidor | variable de entorno `ConnectionStrings__Default` | no hay archivo: la define el hosting |
| Respaldo si no hay nada configurado | `CadenaPorDefecto` en `Infrastructure/DependencyInjection.cs` | `Database=mpaz`, `Username=mpaz_app` |

ASP.NET Core aplica esta precedencia (de menor a mayor):

1. `appsettings.json`
2. `appsettings.{Entorno}.json` — acá está la cadena de desarrollo
3. Variables de entorno — acá debe ir la del servidor
4. Argumentos de línea de comandos

Por eso `appsettings.json` deja `ConnectionStrings:Default` vacío: es el valor
base, y cualquier entorno lo sobrescribe. En desarrollo manda
`appsettings.Development.json`; en el servidor (`Production`) manda la variable
de entorno.

## Desarrollo local

```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=mpaz-matricula;Username=postgres;Password=...;Search Path=mpaz_matricula,public"
}
```

La contraseña quedó versionada a propósito, para que la configuración sea
auditable. Antes de publicar el repositorio, reemplazarla por una credencial
propia de desarrollo.

## Instalación nueva

Los scripts de `database/` están numerados y se aplican en ese orden; el
detalle está en `database/DESPLIEGUE.md`.

1. Crear la base vacía.
2. Aplicar la secuencia completa con un solo comando:

   ```bash
   cd mpaz-matricula/database
   PGPASSWORD='<clave admin>' ./aplicar.sh mpaz_matricula \
     --clave-app '<clave de mpaz_app>'
   ```

   Ejecuta `00-rol-aplicacion.sql`, `01-ddl-institucional.sql`,
   `02-identidad.sql` y `04-privilegios-aplicacion.sql`. Agregar
   `--con-permisos` sólo cuando el colegio apruebe la matriz.

3. Alternativa: si las tablas de Identity se aplican con el bundle de EF Core,
   usar `--sin-identidad` y ejecutar `efbundle` después del DDL.

## Base que ya existe (pasarla al esquema dedicado)

Una base creada antes del esquema dedicado tiene las tablas institucionales en
`public` y las de Identity en `auth`. Para moverla sin perder datos:

```bash
# 1. Respaldo
pg_dump -Fc -d <base> -f respaldo-antes-esquema.dump

# 2. Mover todos los objetos a mpaz_matricula (idempotente)
psql -U <propietario> -d <base> -f database/90-migrar-bases-existentes.sql

# 3. Registrar la migración en EF (no vuelve a mover nada)
dotnet ef database update --project mpaz-matricula/MarcelaPaz.Infrastructure \
  --startup-project mpaz-matricula/MarcelaPaz.Web
```

El script mueve tablas, secuencias, vistas y funciones; conserva los datos, los
índices, los triggers y el historial de migraciones, y elimina el esquema `auth`
cuando queda vacío. También ajusta el `search_path` del rol `mpaz_app` si existe.

## Servidor

1. Definir la variable de entorno antes de iniciar la aplicación:

   ```
   ConnectionStrings__Default = Host=127.0.0.1;Port=5432;Database=mpaz_matricula;Username=mpaz_app;Password=<clave>;SSL Mode=Disable;Search Path=mpaz_matricula,public
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
