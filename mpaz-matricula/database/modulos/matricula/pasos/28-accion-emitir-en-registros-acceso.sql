-- =====================================================================
-- MPAZ Web · Matrícula 2027 — Acción EMITIR en registros_acceso
-- 26-11-2026
--
-- Problema: CertificadosController audita la emisión con la acción
-- 'EMITIR', pero el CHECK de registros_acceso sólo admitía
-- LEER/BUSCAR/DESCARGAR/EXPORTAR/REEMPLAZAR/DENEGADO. El INSERT fallaba,
-- AuditoriaAcceso lo capturaba y lo dejaba sólo en el log: la emisión de
-- un certificado quedaba sin rastro en la auditoría (MAT-13).
--
-- Este paso agrega 'EMITIR' al CHECK en las bases ya instaladas. El DDL
-- (pasos/01 e instalacion/01-estructura-bd.sql) ya lo incluye para las
-- instalaciones nuevas.
--
-- Re-ejecutable: quita el CHECK existente y lo vuelve a crear.
-- Ejecutar con search_path = mpaz_matricula, public.
-- =====================================================================

BEGIN;

DO $$
DECLARE
    nombre text;
BEGIN
    FOR nombre IN
        SELECT con.conname
        FROM pg_constraint con
        JOIN pg_class rel ON rel.oid = con.conrelid
        JOIN pg_namespace esq ON esq.oid = rel.relnamespace
        WHERE esq.nspname = 'mpaz_matricula'
          AND rel.relname = 'registros_acceso'
          AND con.contype = 'c'
          AND pg_get_constraintdef(con.oid) LIKE '%accion%'
    LOOP
        EXECUTE format('ALTER TABLE mpaz_matricula.registros_acceso DROP CONSTRAINT %I', nombre);
    END LOOP;
END $$;

ALTER TABLE mpaz_matricula.registros_acceso
    ADD CONSTRAINT registros_acceso_accion_check CHECK (accion IN
        ('LEER', 'BUSCAR', 'DESCARGAR', 'EXPORTAR', 'REEMPLAZAR', 'DENEGADO', 'EMITIR'));

COMMENT ON CONSTRAINT registros_acceso_accion_check ON mpaz_matricula.registros_acceso IS
    'Acciones auditables. EMITIR cubre la emisión de certificados (26-11-2026).';

COMMIT;
