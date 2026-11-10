# Común

Pasos compartidos por todos los módulos. `aplicar.sh` los ejecuta siempre,
antes que los pasos del módulo indicado.

| Paso | Archivo | Qué hace |
|---|---|---|
| 00 | `pasos/00-rol-aplicacion.sql` | Crea el rol `mpaz_app` y deja el `search_path` en `mpaz_matricula, public`. Requiere superusuario o propietario. |
| 02 | `pasos/02-identidad.sql` | Crea las tablas de ASP.NET Identity y el historial de migraciones en `mpaz_matricula`. Se omite con `--sin-identidad` si se usa el bundle de EF Core. |

La numeración deja hueco (`01`, `03`–`09`) para futuros pasos comunes sin
renumerar los módulos existentes.
