-- =====================================================================
-- 13. MAT-17 · Revisión automática de alertas de vencimiento.
--
-- Canal: in-app (/Alertas), sin correo ni servicios externos.
-- Frecuencia: cada 15 minutos, más una primera revisión al arrancar el portal.
-- Responsable del resumen: proceso programado (sin funcionario asignado).
--
-- La pantalla /Alertas sigue consultando la vista en vivo; esta tabla deja
-- constancia de cuándo se calculó por última vez y con qué totales. Es
-- aditiva e idempotente: no borra ni modifica datos.
-- =====================================================================

BEGIN;
SET search_path TO mpaz_matricula, public;

CREATE TABLE IF NOT EXISTS alerta_revision (
    id                              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    revisada_en                     TIMESTAMPTZ NOT NULL DEFAULT now(),
    anio                            SMALLINT NOT NULL CHECK (anio BETWEEN 2000 AND 2100),
    vencidas_autorizaciones         INTEGER NOT NULL DEFAULT 0 CHECK (vencidas_autorizaciones >= 0),
    vencidas_prescripciones         INTEGER NOT NULL DEFAULT 0 CHECK (vencidas_prescripciones >= 0),
    por_vencer_autorizaciones       INTEGER NOT NULL DEFAULT 0 CHECK (por_vencer_autorizaciones >= 0),
    por_vencer_prescripciones       INTEGER NOT NULL DEFAULT 0 CHECK (por_vencer_prescripciones >= 0),
    resumen                         TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_alerta_revision_en
    ON alerta_revision (revisada_en DESC);

-- La revisión la escribe el proceso programado con el rol de la aplicación:
-- solo lectura e inserción, nunca UPDATE/DELETE.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mpaz_app') THEN
        EXECUTE 'GRANT SELECT, INSERT ON TABLE alerta_revision TO mpaz_app';
    END IF;
END
$$;

COMMENT ON TABLE alerta_revision IS
    'Resumen periódico de v_alertas_vencimiento escrito por el proceso programado MAT-17.';

COMMENT ON COLUMN alerta_revision.anio IS
    'Año escolar activo al momento de la revisión.';

COMMENT ON COLUMN alerta_revision.resumen IS
    'Texto listo para mostrar junto a la última revisión automática.';

COMMIT;
