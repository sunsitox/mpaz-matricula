-- Rol de la aplicación. Se crea antes del DDL para poder otorgarle permisos.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mpaz_app') THEN
        EXECUTE 'CREATE ROLE mpaz_app LOGIN PASSWORD ''mpaz_app_2027'' NOSUPERUSER NOCREATEDB NOCREATEROLE';
    END IF;
END $$;

GRANT CONNECT ON DATABASE mpaz_matricula TO mpaz_app;
REVOKE CONNECT ON DATABASE mpaz_matricula FROM PUBLIC;
