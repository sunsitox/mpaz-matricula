-- =====================================================================
-- MPAZ · Paso 20 — Firmante persistido del certificado de alumno regular
--
-- Decisión 17 (colegio, 20-11-2026): sin director presente, firma el
-- subrogante. En el documento el cargo impreso es «Director» y el nombre
-- es el del funcionario que emite (subrogante). Ambos se guardan en la
-- fila de emisión para que el papel y la validación pública coincidan
-- con lo registrado, en vez de depender de la configuración.
--
-- Las columnas son nulas: los certificados emitidos antes de este paso
-- no tienen firmante guardado. Idempotente.
-- =====================================================================

SET search_path = mpaz_matricula, public;

ALTER TABLE certificado_emitido
    ADD COLUMN IF NOT EXISTS firmante_nombre VARCHAR(150),
    ADD COLUMN IF NOT EXISTS firmante_cargo  VARCHAR(120);

COMMENT ON COLUMN certificado_emitido.firmante_nombre IS
    'Nombre del subrogante que firma (funcionario que emite)';
COMMENT ON COLUMN certificado_emitido.firmante_cargo IS
    'Cargo impreso en el certificado: Director';

SELECT column_name
FROM information_schema.columns
WHERE table_schema = 'mpaz_matricula'
  AND table_name = 'certificado_emitido'
  AND column_name LIKE 'firmante%'
ORDER BY column_name;
