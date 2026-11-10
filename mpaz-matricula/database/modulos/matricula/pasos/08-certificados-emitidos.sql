-- =====================================================================
-- MPAZ · Paso 08 — Registro de certificados de alumno regular emitidos
--
-- Cada emisión del portal deja una fila con folio único y el hash del
-- código de verificación, para validar la autenticidad del papel sin
-- guardar el código en claro. Tabla nueva: no toca datos existentes.
-- Re-ejecutable.
-- =====================================================================

CREATE TABLE IF NOT EXISTS mpaz_matricula.certificado_emitido (
    id            INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    folio         TEXT NOT NULL UNIQUE,
    codigo_hash   TEXT NOT NULL,
    matricula_id  INTEGER NOT NULL REFERENCES mpaz_matricula.matricula_anual(id),
    estudiante_id INTEGER NOT NULL REFERENCES mpaz_matricula.estudiante(id),
    anio          SMALLINT NOT NULL,
    emitido_por   INTEGER REFERENCES mpaz_matricula.funcionario(id),
    emitido_en    TIMESTAMPTZ NOT NULL DEFAULT now(),
    proposito     TEXT,
    anulado_en    TIMESTAMPTZ,
    anulado_por   INTEGER REFERENCES mpaz_matricula.funcionario(id)
);

CREATE INDEX IF NOT EXISTS ix_certificado_emitido_estudiante
    ON mpaz_matricula.certificado_emitido (estudiante_id, anio);