-- =====================================================================
-- MPAZ Web · Año escolar de matrícula (configuración, no calculado)
--
-- El calendario no coincide con el periodo de matrícula: en enero y febrero
-- todavía se matricula para el año que ya comenzó. Por eso el año con el que
-- trabaja el módulo de Matrícula se configura aquí y nunca se deduce de la
-- fecha del sistema.
--
-- Sólo un año puede tener activo_matricula = true. Al cambiarlo (por ejemplo,
-- de 2027 a 2028) todo lo nuevo —expedientes, fichas y folios— sale con el año
-- nuevo sin tocar código. Las fichas ya generadas conservan su año porque el
-- folio y la matrícula guardan el suyo.
--
-- Prerrequisito: 01-ddl-institucional.sql aplicado.
-- Ejecutar como propietario de los objetos o administrador PostgreSQL.
-- =====================================================================

BEGIN;

SET LOCAL search_path TO mpaz_matricula, public;

CREATE TABLE IF NOT EXISTS anio_escolar (
    anio             SMALLINT PRIMARY KEY CHECK (anio BETWEEN 2000 AND 2100),
    activo_matricula BOOLEAN NOT NULL DEFAULT FALSE,
    apertura         DATE,
    cierre           DATE,
    actualizado_en   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_anio_ventana CHECK (cierre IS NULL OR apertura IS NULL OR cierre >= apertura)
);

COMMENT ON TABLE anio_escolar IS
    'Años escolares del portal. Sólo uno tiene activo_matricula = true: el que usa Matrícula.';
COMMENT ON COLUMN anio_escolar.activo_matricula IS
    'Año con el que se generan matrículas, fichas y folios nuevos. Se cambia desde Configuración.';
COMMENT ON COLUMN anio_escolar.apertura IS
    'Inicio del periodo de matrícula (opcional; permite automatizar la apertura por fecha).';
COMMENT ON COLUMN anio_escolar.cierre IS
    'Fin del periodo de matrícula (opcional).';

-- Un solo año activo a la vez.
CREATE UNIQUE INDEX IF NOT EXISTS uk_anio_matricula_activo
    ON anio_escolar (activo_matricula)
    WHERE activo_matricula;

-- Instalaciones existentes: se registran los años que ya tienen matrículas y,
-- si todavía no hay ninguno activo, queda activo el más reciente.
INSERT INTO anio_escolar (anio)
SELECT DISTINCT anio FROM matricula_anual
ON CONFLICT (anio) DO NOTHING;

UPDATE anio_escolar
SET activo_matricula = TRUE
WHERE anio = (SELECT max(anio) FROM matricula_anual)
  AND NOT EXISTS (SELECT 1 FROM anio_escolar WHERE activo_matricula);

-- La tabla nueva no hereda los GRANT de 04-privilegios-aplicacion.sql.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mpaz_app') THEN
        EXECUTE 'GRANT SELECT, INSERT, UPDATE ON TABLE anio_escolar TO mpaz_app';
    END IF;
END $$;

COMMIT;

-- Verificación: debe haber exactamente un año activo. El nombre va calificado
-- porque SET LOCAL termina con la transacción.
SELECT anio, activo_matricula, apertura, cierre
FROM mpaz_matricula.anio_escolar
ORDER BY anio DESC;
