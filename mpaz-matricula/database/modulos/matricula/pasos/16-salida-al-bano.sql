-- 16 · Salida al baño con columna propia (MAT-05)
--
-- Antes la respuesta se guardaba dentro de antecedente_salud.necesidades_atencion,
-- sobrescribiendo su contenido y mostrándose en la ficha como "necesidades de
-- atención". Idempotente: se puede reaplicar sin efectos.

SET search_path = mpaz_matricula, public;

ALTER TABLE mpaz_matricula.antecedente_salud
    ADD COLUMN IF NOT EXISTS sale_al_bano BOOLEAN;

COMMENT ON COLUMN mpaz_matricula.antecedente_salud.sale_al_bano IS
    '¿Requiere salir al baño durante la jornada? (ficha, MAT-05)';

-- Los expedientes cargados antes del cambio traen la respuesta en el texto
-- anterior: se rescata y se libera el campo de necesidades de atención.
UPDATE mpaz_matricula.antecedente_salud
SET sale_al_bano = CASE
        WHEN necesidades_atencion LIKE '%: SI%' THEN TRUE
        WHEN necesidades_atencion LIKE '%: NO%' THEN FALSE
    END,
    necesidades_atencion = NULL
WHERE sale_al_bano IS NULL
  AND necesidades_atencion LIKE 'Sale al baño durante la jornada:%';
