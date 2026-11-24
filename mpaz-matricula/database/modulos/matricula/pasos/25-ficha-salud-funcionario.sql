-- =====================================================================
-- MPAZ · Paso 25 — Ficha de salud del funcionario
--
-- Formulario pedido por el colegio (23-11-2026, sólo local por ahora):
--   · identificación: nombre, RUN o pasaporte, teléfono y dirección;
--   · dos contactos de emergencia con teléfono;
--   · sistema de salud, centro preferente, ambulancia en convenio,
--     seguro complementario, alergias, enfermedades crónicas,
--     medicamentos frecuentes y aceptación de transfusión.
--
-- Información sensible del personal: sólo UTP y Dirección (área SALUD).
-- Idempotente. No toca datos existentes. Ejecutar después del paso 24.
-- =====================================================================

SET search_path = mpaz_matricula, public;

CREATE TABLE IF NOT EXISTS mpaz_matricula.ficha_salud_funcionario (
    id                      INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    funcionario_id          INTEGER NOT NULL REFERENCES mpaz_matricula.funcionario(id),
    nombre_completo         TEXT NOT NULL,
    rut                     TEXT,
    pasaporte               TEXT,
    telefono_personal       TEXT,
    direccion_personal      TEXT,
    contacto1_nombre        TEXT,
    contacto1_telefono      TEXT,
    contacto2_nombre        TEXT,
    contacto2_telefono      TEXT,
    sistema_salud           TEXT
                            CHECK (sistema_salud IS NULL
                                   OR sistema_salud IN ('Fonasa', 'Isapre', 'Otro')),
    centro_medico_preferente TEXT,
    centro_ubicacion        TEXT,
    centro_telefono         TEXT,
    ambulancia_convenio     TEXT,
    ambulancia_telefono     TEXT,
    seguro_complementario   TEXT,
    otros                   TEXT,
    alergias_medicamentos   TEXT,
    enfermedades_cronicas   TEXT,
    medicamentos_frecuentes TEXT,
    acepta_transfusion      TEXT
                            CHECK (acepta_transfusion IS NULL
                                   OR acepta_transfusion IN ('Si', 'No')),
    motivo_transfusion      TEXT,
    registrado_por          INTEGER REFERENCES mpaz_matricula.funcionario(id),
    registrado_en           TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ficha_salud_documento_chk
        CHECK (rut IS NOT NULL OR pasaporte IS NOT NULL),
    CONSTRAINT ficha_salud_motivo_chk
        CHECK (acepta_transfusion IS DISTINCT FROM 'No'
               OR motivo_transfusion IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS ix_ficha_salud_funcionario
    ON mpaz_matricula.ficha_salud_funcionario (funcionario_id, registrado_en DESC);

-- UTP y Dirección registran y leen la ficha de salud (área SALUD).
INSERT INTO mpaz_matricula.rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM mpaz_matricula.rol r
JOIN (VALUES
    ('UTP',       'SALUD', 'LEER'),
    ('UTP',       'SALUD', 'CREAR'),
    ('UTP',       'SALUD', 'EDITAR'),
    ('DIRECCION', 'SALUD', 'LEER'),
    ('DIRECCION', 'SALUD', 'CREAR'),
    ('DIRECCION', 'SALUD', 'EDITAR')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;
