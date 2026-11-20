-- =====================================================================
-- MPAZ · Paso 21 — Observaciones de situación familiar (decisión 19)
--
-- Notas de situación familiar del estudiante, sólo para UTP,
-- Convivencia Escolar y Dirección. No reutilizan SALUD ni JUDICIAL:
-- es un área propia, SITUACION_FAMILIAR, porque su sensibilidad es
-- distinta (contexto familiar, no clínico ni judicial).
--
--   · DIRECCION:      LEER (no crea ni edita, como en el resto del portal).
--   · UTP:            LEER + CREAR + EDITAR.
--   · CONVIVENCIA:    LEER + CREAR + EDITAR.
-- UTP y Convivencia son perfiles distintos (confirmado 20-11-2026):
-- no se fusionan en Coordinación Educativa.
--
-- Sin adjuntos documento_id en esta versión: la observación es texto
-- con vigencia opcional, igual que las notas de retiro (paso 17).
-- Idempotente.
-- =====================================================================

SET search_path = mpaz_matricula, public;

BEGIN;

CREATE TABLE IF NOT EXISTS observacion_situacion_familiar (
    id             INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id  INTEGER NOT NULL REFERENCES estudiante(id),
    detalle        TEXT NOT NULL,
    vigente_desde  DATE,
    vigente_hasta  DATE,
    registrado_por INTEGER REFERENCES funcionario(id),
    registrado_en  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_observacion_familiar_estudiante
    ON observacion_situacion_familiar (estudiante_id, registrado_en DESC);

-- Área nueva en el CHECK de rol_permiso (sin tocar las demás áreas).
ALTER TABLE rol_permiso DROP CONSTRAINT IF EXISTS rol_permiso_area_check;
ALTER TABLE rol_permiso ADD CONSTRAINT rol_permiso_area_check CHECK (area IN (
    'GENERAL', 'FAMILIA', 'RETIRO', 'SALUD', 'PIE', 'JUDICIAL',
    'AUTORIZACIONES', 'MEDICACION', 'DOCUMENTOS', 'AUDITORIA', 'ADMIN',
    'SITUACION_FAMILIAR'));

INSERT INTO rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM rol r
JOIN (VALUES
    ('DIRECCION',   'SITUACION_FAMILIAR', 'LEER'),
    ('UTP',         'SITUACION_FAMILIAR', 'LEER'),
    ('UTP',         'SITUACION_FAMILIAR', 'CREAR'),
    ('UTP',         'SITUACION_FAMILIAR', 'EDITAR'),
    ('CONVIVENCIA', 'SITUACION_FAMILIAR', 'LEER'),
    ('CONVIVENCIA', 'SITUACION_FAMILIAR', 'CREAR'),
    ('CONVIVENCIA', 'SITUACION_FAMILIAR', 'EDITAR')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;

COMMIT;

SELECT r.codigo AS rol, p.accion
FROM rol_permiso p JOIN rol r ON r.id = p.rol_id
WHERE p.area = 'SITUACION_FAMILIAR'
ORDER BY r.codigo, p.accion;

SELECT count(*) AS observaciones FROM observacion_situacion_familiar;
