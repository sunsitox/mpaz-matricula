-- =====================================================================
-- 06. Validación institucional de la matrícula (MAT-12, acción VALIDAR).
-- La validación de Dirección es un acto distinto de confirmar: deja fecha y
-- responsable en la propia matrícula y queda auditada por el trigger
-- trg_matricula_anual_auditoria. Aditivo e idempotente.
-- =====================================================================

ALTER TABLE matricula_anual
    ADD COLUMN IF NOT EXISTS validada_en TIMESTAMPTZ;

ALTER TABLE matricula_anual
    ADD COLUMN IF NOT EXISTS validada_por INTEGER REFERENCES funcionario(id);
