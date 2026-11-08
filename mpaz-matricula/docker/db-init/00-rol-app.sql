-- Rol de la aplicación. Se crea antes del DDL para poder otorgarle permisos.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mpaz_app') THEN
        EXECUTE 'CREATE ROLE mpaz_app LOGIN PASSWORD ''mpaz_app_2027'' NOSUPERUSER NOCREATEDB NOCREATEROLE';
    END IF;
END $$;

GRANT CONNECT ON DATABASE mpaz_matricula TO mpaz_app;
REVOKE CONNECT ON DATABASE mpaz_matricula FROM PUBLIC;

-- Los objetos del portal viven en el esquema mpaz_matricula.
ALTER ROLE mpaz_app IN DATABASE mpaz_matricula SET search_path = mpaz_matricula, public;
ALTER DATABASE mpaz_matricula SET search_path = mpaz_matricula, public;
