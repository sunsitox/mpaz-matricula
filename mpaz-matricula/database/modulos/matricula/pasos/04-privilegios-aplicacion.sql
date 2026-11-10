-- =====================================================================
-- MPAZ Web · Privilegios del rol de aplicación en producción
--
-- Todos los objetos del portal viven en el esquema matricula
-- (ver 90-migrar-bases-existentes.sql). El esquema public sólo aloja la extensión
-- pg_trgm, compartida con otras bases del mismo servidor.
--
-- Prerrequisitos:
--   1. El rol mpaz_app ya existe.
--   2. 01-ddl-institucional.sql ya fue aplicado.
--   3. El bundle EF ya creó las tablas de Identity en mpaz_matricula.
--
-- Ejecutar como propietario de los objetos o administrador PostgreSQL.
-- =====================================================================

BEGIN;

REVOKE ALL ON SCHEMA mpaz_matricula FROM PUBLIC;
GRANT USAGE ON SCHEMA mpaz_matricula TO mpaz_app;

-- Datos institucionales. No se concede DELETE de forma general.
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA mpaz_matricula TO mpaz_app;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA mpaz_matricula TO mpaz_app;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA mpaz_matricula TO mpaz_app;

-- La implementación actual reemplaza las situaciones médicas del borrador
-- mediante DELETE + INSERT. Este permiso debe retirarse cuando ese flujo use
-- vigencia o actualización sin borrado.
GRANT DELETE ON TABLE mpaz_matricula.situacion_medica TO mpaz_app;

-- Auditoría y medicación son solo-inserción. Los triggers del DDL también
-- rechazan UPDATE/DELETE, pero se revocan aquí como defensa adicional.
REVOKE UPDATE, DELETE ON TABLE
    mpaz_matricula.historial_cambios,
    mpaz_matricula.registros_acceso,
    mpaz_matricula.administracion_medicamento
FROM mpaz_app;

-- Identity necesita administrar sesiones, credenciales, bloqueos y tokens,
-- pero sólo en sus propias tablas: el resto del esquema es SELECT/INSERT/UPDATE.
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
    mpaz_matricula."AspNetUsers",
    mpaz_matricula."AspNetRoles",
    mpaz_matricula."AspNetRoleClaims",
    mpaz_matricula."AspNetUserClaims",
    mpaz_matricula."AspNetUserLogins",
    mpaz_matricula."AspNetUserRoles",
    mpaz_matricula."AspNetUserTokens"
TO mpaz_app;

-- Que el rol resuelva los nombres sin calificar de triggers y vistas.
ALTER ROLE mpaz_app SET search_path = mpaz_matricula, public;

COMMIT;

-- Verificación resumida: todas deben devolver el valor esperado.
SELECT
    has_table_privilege('mpaz_app', 'mpaz_matricula.estudiante', 'SELECT') AS puede_leer_estudiante,
    has_table_privilege('mpaz_app', 'mpaz_matricula.estudiante', 'DELETE') AS puede_borrar_estudiante,
    has_table_privilege('mpaz_app', 'mpaz_matricula.situacion_medica', 'DELETE') AS puede_reemplazar_situacion,
    has_table_privilege('mpaz_app', 'mpaz_matricula.registros_acceso', 'UPDATE') AS puede_modificar_auditoria,
    has_table_privilege('mpaz_app', 'mpaz_matricula."AspNetUsers"', 'UPDATE') AS puede_actualizar_identity;
