# Conexión a la base de datos

El portal usa PostgreSQL y EF Core (Npgsql). La cadena de conexión se entrega en
`Program.cs` a `AddPersistencia(...)`, que vive en `MarcelaPaz.Infrastructure`:
la capa Web no conoce el proveedor, sólo pasa la cadena.

## Dónde está configurada

| Entorno | Mecanismo | Archivo |
|---|---|---|
| Desarrollo local (Visual Studio, `dotnet run`) | `appsettings.Development.json` | `project/MarcelaPaz.Web/appsettings.Development.json` |
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
  "Default": "Host=localhost;Port=5432;Database=mpaz-matricula;Username=postgres;Password=..."
}
```

La contraseña quedó versionada a propósito, para que la configuración sea
auditable. Antes de publicar el repositorio, reemplazarla por una credencial
propia de desarrollo.

## Servidor

1. Definir la variable de entorno antes de iniciar la aplicación:

   ```
   ConnectionStrings__Default = Host=<servidor>;Port=5432;Database=mpaz-matricula;Username=mpaz_app;Password=<clave>
   ```

   El doble guion bajo `__` es la forma de escribir `ConnectionStrings:Default`
   en una variable de entorno.

2. No usar el superusuario `postgres` en el servidor: crear el rol de
   aplicación `mpaz_app` con permisos sólo sobre esta base.

3. Crear el esquema: ejecutar el DDL institucional (`public`) y luego

   ```bash
   dotnet ef database update --project project/MarcelaPaz.Infrastructure \
     --startup-project project/MarcelaPaz.Web
   ```

## Comprobar que la conexión funciona

```bash
dotnet ef database update --project project/MarcelaPaz.Infrastructure \
  --startup-project project/MarcelaPaz.Web --connection "<cadena>"
```

O iniciar la aplicación y abrir `/admin/funcionarios`: esa vista consulta
`public.funcionario`, así que un 200 sin excepciones confirma la conexión.
