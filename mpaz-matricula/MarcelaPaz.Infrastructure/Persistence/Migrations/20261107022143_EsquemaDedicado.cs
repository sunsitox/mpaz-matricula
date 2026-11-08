using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarcelaPaz.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lleva todos los objetos del portal al esquema <c>mpaz_matricula</c>:
    /// las tablas institucionales que estaban en <c>public</c>, las de Identity
    /// que estaban en <c>auth</c> y el historial de migraciones.
    ///
    /// Las tablas institucionales no las administra EF, por eso el movimiento se
    /// hace con SQL idempotente: sirve tanto para una base con datos como para
    /// una instalación nueva, donde el DDL ya las crea en el esquema correcto.
    /// </summary>
    public partial class EsquemaDedicado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mpaz_matricula");

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    objeto record;
                BEGIN
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
                """);

            // auth queda vacío: se elimina para no dejar un nombre genérico
            // que choque cuando varias bases convivan en el mismo servidor.
            migrationBuilder.Sql(
                """
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'auth') THEN
                        CREATE SCHEMA auth;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    objeto record;
                    destino text;
                BEGIN
                    FOR objeto IN
                        SELECT c.relname AS nombre
                        FROM pg_class c
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE c.relkind IN ('r', 'p')
                          AND n.nspname = 'mpaz_matricula'
                          AND NOT EXISTS (
                              SELECT 1 FROM pg_depend d
                              WHERE d.objid = c.oid AND d.deptype = 'e')
                        ORDER BY c.relname
                    LOOP
                        destino := CASE WHEN objeto.nombre LIKE 'AspNet%' THEN 'auth' ELSE 'public' END;
                        EXECUTE format('ALTER TABLE mpaz_matricula.%I SET SCHEMA %I',
                                       objeto.nombre, destino);
                    END LOOP;

                    FOR objeto IN
                        SELECT c.relname AS nombre
                        FROM pg_class c
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE c.relkind = 'S'
                          AND n.nspname = 'mpaz_matricula'
                          AND NOT EXISTS (
                              SELECT 1 FROM pg_depend d
                              WHERE d.objid = c.oid AND d.deptype = 'e')
                    LOOP
                        EXECUTE format('ALTER SEQUENCE mpaz_matricula.%I SET SCHEMA public',
                                       objeto.nombre);
                    END LOOP;

                    FOR objeto IN
                        SELECT c.relname AS nombre, c.relkind
                        FROM pg_class c
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE c.relkind IN ('v', 'm')
                          AND n.nspname = 'mpaz_matricula'
                    LOOP
                        EXECUTE format('ALTER %s mpaz_matricula.%I SET SCHEMA public',
                                       CASE WHEN objeto.relkind = 'm' THEN 'MATERIALIZED VIEW' ELSE 'VIEW' END,
                                       objeto.nombre);
                    END LOOP;

                    FOR objeto IN
                        SELECT p.proname AS nombre,
                               pg_get_function_identity_arguments(p.oid) AS argumentos
                        FROM pg_proc p
                        JOIN pg_namespace n ON n.oid = p.pronamespace
                        WHERE n.nspname = 'mpaz_matricula'
                          AND NOT EXISTS (
                              SELECT 1 FROM pg_depend d
                              WHERE d.objid = p.oid AND d.deptype = 'e')
                    LOOP
                        EXECUTE format('ALTER FUNCTION mpaz_matricula.%I(%s) SET SCHEMA public',
                                       objeto.nombre, objeto.argumentos);
                    END LOOP;
                END $$;
                """);
        }
    }
}
