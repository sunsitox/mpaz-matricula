-- =====================================================================
-- MPAZ · Paso 17 — Notas de retiro y fichas médicas del personal
--
-- Requerimientos nuevos de UTP (19-11-2026):
--   · registrar una nota de retiro por orden de alejamiento, retiro
--     autorizado o retiro no autorizado;
--   · llevar las fichas y certificados médicos de los funcionarios,
--     visibles sólo para UTP.
--
-- Idempotente. No toca datos existentes. Ejecutar después del paso 16.
-- =====================================================================

SET search_path = mpaz_matricula, public;

-- Notas de retiro del estudiante -----------------------------------------
CREATE TABLE IF NOT EXISTS mpaz_matricula.nota_retiro (
    id             INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id  INTEGER NOT NULL REFERENCES mpaz_matricula.estudiante(id),
    tipo           TEXT NOT NULL
                   CHECK (tipo IN ('ORDEN_ALEJAMIENTO', 'AUTORIZADO', 'NO_AUTORIZADO')),
    detalle        TEXT NOT NULL,
    vigente_desde  DATE,
    vigente_hasta  DATE,
    registrado_por INTEGER REFERENCES mpaz_matricula.funcionario(id),
    registrado_en  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_nota_retiro_estudiante
    ON mpaz_matricula.nota_retiro (estudiante_id, registrado_en DESC);

-- Fichas y certificados médicos de funcionarios --------------------------
CREATE TABLE IF NOT EXISTS mpaz_matricula.ficha_medica_funcionario (
    id             INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    funcionario_id INTEGER NOT NULL REFERENCES mpaz_matricula.funcionario(id),
    tipo           TEXT NOT NULL,
    descripcion    TEXT,
    emisor         TEXT,
    emitido_en     DATE,
    vigente_hasta  DATE,
    archivo_ruta   TEXT,
    archivo_nombre TEXT,
    registrado_por INTEGER REFERENCES mpaz_matricula.funcionario(id),
    registrado_en  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_ficha_medica_funcionario
    ON mpaz_matricula.ficha_medica_funcionario (funcionario_id, registrado_en DESC);

-- UTP puede registrar notas de retiro. El resto de sus áreas sigue en
-- sólo lectura: no se le conceden CREAR/EDITAR/EXPORTAR en otras áreas.
INSERT INTO mpaz_matricula.rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM mpaz_matricula.rol r
JOIN (VALUES
    ('UTP', 'RETIRO', 'LEER'),
    ('UTP', 'RETIRO', 'CREAR'),
    ('UTP', 'RETIRO', 'EDITAR')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;
