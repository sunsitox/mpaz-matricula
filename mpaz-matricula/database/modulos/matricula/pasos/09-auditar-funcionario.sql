-- =====================================================================
-- 09. Auditoría del catálogo de funcionarios (MAT-13).
--
-- `funcionario` tiene clave simple `id`, así que usa el mismo trigger que el
-- resto de las tablas auditadas. Hasta ahora sólo `funcionario_rol` quedaba
-- auditado: un cambio de nombre, de correo o de estado de la cuenta no dejaba
-- rastro en historial_cambios.
--
-- Aditivo e idempotente: se puede aplicar sobre una base ya instalada.
-- =====================================================================

SET search_path TO mpaz_matricula, public;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_trigger
        WHERE tgname = 'trg_funcionario_auditoria'
          AND tgrelid = 'funcionario'::regclass
    ) THEN
        CREATE TRIGGER trg_funcionario_auditoria
            AFTER INSERT OR UPDATE OR DELETE ON funcionario
            FOR EACH ROW EXECUTE FUNCTION fn_auditar_cambio();
    END IF;
END $$;
