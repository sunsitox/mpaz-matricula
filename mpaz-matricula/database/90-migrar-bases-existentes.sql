-- =====================================================================
-- MPAZ Web · Portal de Matrícula 2027 — Esquema dedicado
--
-- Deja TODOS los objetos del portal en el esquema mpaz_matricula, para que
-- esta base pueda convivir con otras bases del colegio cuando se unifiquen
-- en un mismo servidor sin mezclar tablas homónimas (estudiante, persona,
-- curso, etc. son nombres que se repiten entre sistemas).
--
-- Qué mueve, si todavía están en public o auth:
--   · tablas institucionales y __EFMigrationsHistory   (public)
--   · tablas de ASP.NET Identity                        (auth)
--   · vistas, secuencias, funciones y sus triggers
--
-- Es idempotente: se puede ejecutar varias veces sin efecto adicional.
-- Si la base venía con un esquema anterior llamado "matricula", lo renombra.
--
-- Uso sobre una base que ya tiene datos:
--   psql -U <propietario> -d <base> -f 90-migrar-bases-existentes.sql
--
-- Después de mover, la aplicación debe conectarse con:
--   Search Path=mpaz_matricula,public
-- =====================================================================

BEGIN;

-- Si existe un esquema "matricula" de una versión anterior, se renombra.
-- Si ambos existen, se traslada su contenido y se elimina el que quede vacío.
DO $$
DECLARE
    objeto record;
BEGIN
    IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'matricula') THEN
        IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'mpaz_matricula') THEN
            ALTER SCHEMA matricula RENAME TO mpaz_matricula;
        ELSE
            FOR objeto IN
                SELECT c.relname AS nombre
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relkind IN ('r', 'p')
                  AND n.nspname = 'matricula'
                  AND NOT EXISTS (
                      SELECT 1 FROM pg_depend d
                      WHERE d.objid = c.oid AND d.deptype = 'e')
            LOOP
                EXECUTE format('ALTER TABLE matricula.%I SET SCHEMA mpaz_matricula', objeto.nombre);
            END LOOP;

            FOR objeto IN
                SELECT c.relname AS nombre
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relkind = 'S'
                  AND n.nspname = 'matricula'
                  AND NOT EXISTS (
                      SELECT 1 FROM pg_depend d
                      WHERE d.objid = c.oid AND d.deptype = 'e')
            LOOP
                EXECUTE format('ALTER SEQUENCE matricula.%I SET SCHEMA mpaz_matricula', objeto.nombre);
            END LOOP;

            FOR objeto IN
                SELECT c.relname AS nombre, c.relkind
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relkind IN ('v', 'm')
                  AND n.nspname = 'matricula'
            LOOP
                EXECUTE format('ALTER %s matricula.%I SET SCHEMA mpaz_matricula',
                               CASE WHEN objeto.relkind = 'm' THEN 'MATERIALIZED VIEW' ELSE 'VIEW' END,
                               objeto.nombre);
            END LOOP;

            FOR objeto IN
                SELECT p.proname AS nombre,
                       pg_get_function_identity_arguments(p.oid) AS argumentos
                FROM pg_proc p
                JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE n.nspname = 'matricula'
                  AND NOT EXISTS (
                      SELECT 1 FROM pg_depend d
                      WHERE d.objid = p.oid AND d.deptype = 'e')
            LOOP
                EXECUTE format('ALTER FUNCTION matricula.%I(%s) SET SCHEMA mpaz_matricula',
                               objeto.nombre, objeto.argumentos);
            END LOOP;
        END IF;
    END IF;
END $$;

CREATE SCHEMA IF NOT EXISTS mpaz_matricula;

DO $$
DECLARE
    objeto record;
BEGIN
    -- Tablas (incluye __EFMigrationsHistory y las de Identity).
    -- Se omiten los objetos de extensiones (pg_trgm queda en public).
    FOR objeto IN
        SELECT n.nspname AS esquema, c.relname AS nombre
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind IN ('r', 'p')
          AND n.nspname IN ('public', 'auth')
          AND NOT EXISTS (
              SELECT 1 FROM pg_depend d
              WHERE d.objid = c.oid AND d.deptype = 'e')
        ORDER BY c.relname
    LOOP
        EXECUTE format('ALTER TABLE %I.%I SET SCHEMA mpaz_matricula',
                       objeto.esquema, objeto.nombre);
    END LOOP;

    -- Secuencias sueltas (las de columnas identity viajan con su tabla).
    FOR objeto IN
        SELECT n.nspname AS esquema, c.relname AS nombre
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind = 'S'
          AND n.nspname IN ('public', 'auth')
          AND NOT EXISTS (
              SELECT 1 FROM pg_depend d
              WHERE d.objid = c.oid AND d.deptype = 'e')
    LOOP
        EXECUTE format('ALTER SEQUENCE %I.%I SET SCHEMA mpaz_matricula',
                       objeto.esquema, objeto.nombre);
    END LOOP;

    -- Vistas y vistas materializadas.
    FOR objeto IN
        SELECT n.nspname AS esquema, c.relname AS nombre, c.relkind
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind IN ('v', 'm')
          AND n.nspname IN ('public', 'auth')
    LOOP
        EXECUTE format('ALTER %s %I.%I SET SCHEMA mpaz_matricula',
                       CASE WHEN objeto.relkind = 'm' THEN 'MATERIALIZED VIEW' ELSE 'VIEW' END,
                       objeto.esquema, objeto.nombre);
    END LOOP;

    -- Funciones (normalizar_rut, rut_valido, fn_* y las de trigger).
    -- Los triggers guardan el OID de la función, así que siguen apuntando bien.
    FOR objeto IN
        SELECT n.nspname AS esquema,
               p.proname AS nombre,
               pg_get_function_identity_arguments(p.oid) AS argumentos
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname IN ('public', 'auth')
          AND NOT EXISTS (
              SELECT 1 FROM pg_depend d
              WHERE d.objid = p.oid AND d.deptype = 'e')
    LOOP
        EXECUTE format('ALTER FUNCTION %I.%I(%s) SET SCHEMA mpaz_matricula',
                       objeto.esquema, objeto.nombre, objeto.argumentos);
    END LOOP;
END $$;

-- Si el esquema anterior "matricula" quedó vacío, se elimina.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'matricula')
       AND NOT EXISTS (
           SELECT 1 FROM pg_class c
           JOIN pg_namespace n ON n.oid = c.relnamespace
           WHERE n.nspname = 'matricula')
       AND NOT EXISTS (
           SELECT 1 FROM pg_proc p
           JOIN pg_namespace n ON n.oid = p.pronamespace
           WHERE n.nspname = 'matricula')
       AND NOT EXISTS (
           SELECT 1 FROM pg_type t
           JOIN pg_namespace n ON n.oid = t.typnamespace
           WHERE n.nspname = 'matricula') THEN
        DROP SCHEMA matricula;
    END IF;
END $$;

-- El esquema auth queda vacío tras mover Identity: se elimina para no dejar
-- un nombre genérico que choque con otras bases.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'auth')
       AND NOT EXISTS (
           SELECT 1 FROM pg_class c
           JOIN pg_namespace n ON n.oid = c.relnamespace
           WHERE n.nspname = 'auth')
       AND NOT EXISTS (
           SELECT 1 FROM pg_proc p
           JOIN pg_namespace n ON n.oid = p.pronamespace
           WHERE n.nspname = 'auth')
       AND NOT EXISTS (
           SELECT 1 FROM pg_type t
           JOIN pg_namespace n ON n.oid = t.typnamespace
           WHERE n.nspname = 'auth') THEN
        DROP SCHEMA auth;
    END IF;
END $$;

-- El rol de la aplicación necesita entrar al esquema y resolver los nombres
-- sin calificar que usan los triggers y las vistas.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mpaz_app') THEN
        GRANT USAGE ON SCHEMA mpaz_matricula TO mpaz_app;
        EXECUTE format(
            'ALTER ROLE mpaz_app IN DATABASE %I SET search_path = mpaz_matricula, public',
            current_database());
    END IF;
END $$;

COMMIT;
