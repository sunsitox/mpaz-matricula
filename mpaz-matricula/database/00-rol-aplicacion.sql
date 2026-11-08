-- =====================================================================
-- 00 · Rol de aplicación
--
-- Crea el rol mpaz_app (sin privilegios de superusuario) y deja la base
-- lista para que la aplicación resuelva el esquema mpaz_matricula.
--
-- Requiere ejecutarse como propietario de la base o superusuario, en la base
-- que se está instalando. La contraseña se recibe como variable de psql:
--
--   psql -d <base> -v clave_app='<clave>' -f 00-rol-aplicacion.sql
--
-- Es idempotente: si el rol ya existe no cambia su contraseña.
-- =====================================================================

\set ON_ERROR_STOP on

-- Rol de aplicación, sin permisos de administración del servidor.
SELECT format(
    'CREATE ROLE mpaz_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD %L',
    :'clave_app')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mpaz_app')
\gexec

-- Sólo el rol de la aplicación puede conectarse a esta base.
SELECT format('REVOKE CONNECT ON DATABASE %I FROM PUBLIC', current_database())
\gexec

SELECT format('GRANT CONNECT ON DATABASE %I TO mpaz_app', current_database())
\gexec

-- El esquema del portal se resuelve sin calificar (triggers y vistas).
SELECT format(
    'ALTER ROLE mpaz_app IN DATABASE %I SET search_path = mpaz_matricula, public',
    current_database())
\gexec

SELECT format(
    'ALTER DATABASE %I SET search_path = mpaz_matricula, public',
    current_database())
\gexec
