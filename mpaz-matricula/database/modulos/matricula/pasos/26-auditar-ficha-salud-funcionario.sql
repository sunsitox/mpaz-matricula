-- =====================================================================
-- MPAZ · Paso 26 — Auditoría de la ficha de salud del funcionario
--
-- La ficha de salud (paso 25) guarda datos sensibles del personal. Sus
-- cambios deben quedar en historial_cambios, igual que antecedente_salud y
-- situacion_medica: registros_acceso sólo admite acciones de lectura y no
-- puede guardar el alta o la edición.
--
-- El trigger usa fn_auditar_cambio(), que toma el funcionario responsable
-- desde app.funcionario_id (lo publica EscrituraAuditada en la transacción).
-- Idempotente. Ejecutar después del paso 25.
-- =====================================================================

SET search_path = mpaz_matricula, public;

DROP TRIGGER IF EXISTS trg_ficha_salud_funcionario_auditoria
    ON mpaz_matricula.ficha_salud_funcionario;

CREATE TRIGGER trg_ficha_salud_funcionario_auditoria
    AFTER INSERT OR UPDATE OR DELETE ON mpaz_matricula.ficha_salud_funcionario
    FOR EACH ROW EXECUTE FUNCTION mpaz_matricula.fn_auditar_cambio();
