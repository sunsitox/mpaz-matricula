-- =====================================================================
-- MPAZ · Paso 22 — Conservación: poda de alerta_revision a 24 meses
--
-- Decisión 13 (propuesta por defecto): no se borra el expediente ni la
-- auditoría; solo se poda el resumen técnico de alertas
-- (alerta_revision) con más de 24 meses. La poda la ejecuta el proceso
-- programado (AlertasProgramadas), por eso el rol de la aplicación
-- necesita DELETE en esta tabla y nada más.
-- Idempotente.
-- =====================================================================

SET search_path = mpaz_matricula, public;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mpaz_app') THEN
        EXECUTE 'GRANT DELETE ON TABLE alerta_revision TO mpaz_app';
    END IF;
END
$$;

COMMENT ON TABLE alerta_revision IS
    'Resumen periódico de v_alertas_vencimiento escrito por el proceso programado MAT-17. Se conserva 24 meses (decisión 13); expediente y auditoría no se podan.';

SELECT count(*) AS revisiones_conservadas
FROM alerta_revision
WHERE revisada_en >= now() - interval '24 months';
