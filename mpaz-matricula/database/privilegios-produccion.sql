-- =====================================================================
-- MPAZ Web · Privilegios del rol de aplicación en producción
--
-- Prerrequisitos:
--   1. El rol mpaz_app ya existe.
--   2. MPAZ_Matricula_2027_DDL.sql ya fue aplicado.
--   3. El bundle EF ya creó el esquema auth.
--
-- Ejecutar como propietario de los objetos o administrador PostgreSQL.
-- =====================================================================

BEGIN;

REVOKE ALL ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON SCHEMA auth FROM PUBLIC;

GRANT USAGE ON SCHEMA public, auth TO mpaz_app;

-- Datos institucionales. No se concede DELETE de forma general.
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO mpaz_app;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO mpaz_app;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO mpaz_app;

-- La implementación actual reemplaza las situaciones médicas del borrador
-- mediante DELETE + INSERT. Este permiso debe retirarse cuando ese flujo use
-- vigencia o actualización sin borrado.
GRANT DELETE ON TABLE public.situacion_medica TO mpaz_app;

-- Auditoría y medicación son solo-inserción. Los triggers del DDL también
-- rechazan UPDATE/DELETE, pero se revocan aquí como defensa adicional.
REVOKE UPDATE, DELETE ON TABLE
    public.historial_cambios,
    public.registros_acceso,
    public.administracion_medicamento
FROM mpaz_app;

-- Identity necesita administrar sesiones, credenciales, bloqueos y tokens.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA auth TO mpaz_app;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA auth TO mpaz_app;

COMMIT;

-- Verificación resumida: todas deben devolver el valor esperado.
SELECT
    has_table_privilege('mpaz_app', 'public.estudiante', 'SELECT') AS puede_leer_estudiante,
    has_table_privilege('mpaz_app', 'public.estudiante', 'DELETE') AS puede_borrar_estudiante,
    has_table_privilege('mpaz_app', 'public.situacion_medica', 'DELETE') AS puede_reemplazar_situacion,
    has_table_privilege('mpaz_app', 'public.registros_acceso', 'UPDATE') AS puede_modificar_auditoria,
    has_table_privilege('mpaz_app', 'auth."AspNetUsers"', 'UPDATE') AS puede_actualizar_identity;
