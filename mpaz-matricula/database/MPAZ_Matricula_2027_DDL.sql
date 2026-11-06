-- =====================================================================
-- MPAZ Web · Módulo 3 — Portal de Matrícula 2027
-- Escuela Escritora Marcela Paz, San Bernardo
--
-- DDL PostgreSQL 14+ (probado en 16), basado en:
--   · "Requerimientos Portal de Matrícula 2027" v1.0 (03-11-2026)
--   · Ficha de Matrícula 2026 (proceso actual)
--
-- Cubre las Etapas I, II y III (captura/PDF, expediente, continuidad).
-- La Etapa IV (integración con Asistencia, Notas y Apoyo) queda fuera.
--
-- DECISIONES DE DISEÑO
--   1. Las tablas "vivas" guardan el estado actual. Cada vez que se cierra
--      una ficha para firma se congela una copia completa en
--      ficha_version.datos_snapshot (JSONB). Lo firmado nunca se sobrescribe
--      (MAT-14, MAT-18).
--   2. Las relaciones y contactos usan vigencia (vigente_desde/hasta) en vez
--      de borrar: un cambio de apoderado cierra una fila y abre otra.
--   3. Los datos sensibles van en tablas separadas, para poder aplicar
--      permisos por área en el servidor (MAT-12).
--   4. Auditoría (historial_cambios, registros_acceso) y registro de
--      administración de medicamentos son solo-inserción.
--   5. Sin ON DELETE CASCADE: nada de lo firmado o auditado se borra en cascada.
--   6. Los valores permitidos usan CHECK ... IN (...) en lugar de ENUM, para
--      poder modificarlos con un simple ALTER TABLE.
--
-- CONVENCIÓN PARA LA APP
--   · Antes de escribir, la app debe ejecutar en la misma transacción:
--         SET LOCAL app.funcionario_id = '<id del funcionario>';
--     para que historial_cambios sepa quién hizo el cambio.
--   · Bloqueo optimista (edición concurrente): leer la fila con su "version"
--     y actualizar con  UPDATE ... WHERE id = :id AND version = :version.
--     Si afecta 0 filas, alguien más la modificó.
--   · Los RUN se guardan normalizados: sin puntos, con guion, K mayúscula
--     (12345678-5). Usar normalizar_rut() antes de insertar.
--
-- PENDIENTES QUE NO SE PUEDEN RESOLVER EN EL ESQUEMA (sección 16 del doc)
--   · Textos finales de autorizaciones (plantilla_autorizacion queda vacía).
--   · Matriz de permisos por rol (rol_permiso queda vacía; hay un ejemplo).
--   · Plazos de conservación y eliminación de datos y originales físicos.
--   · Límite de tamaño de archivos (aquí 15 MB; ajustar en ck_documento_tamano).
-- =====================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS pg_trgm;   -- búsqueda por nombre (MAT-11)

-- =====================================================================
-- 0. FUNCIONES AUXILIARES
-- =====================================================================

-- Normaliza un RUN: quita puntos y espacios, agrega guion, K mayúscula.
CREATE OR REPLACE FUNCTION normalizar_rut(p TEXT) RETURNS TEXT
LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE v TEXT := upper(regexp_replace(coalesce(p, ''), '[\.\s]', '', 'g'));
BEGIN
    IF position('-' IN v) = 0 AND length(v) >= 2 THEN
        v := substr(v, 1, length(v) - 1) || '-' || right(v, 1);
    END IF;
    RETURN v;
END $$;

-- Valida formato y dígito verificador (módulo 11) de un RUN normalizado.
CREATE OR REPLACE FUNCTION rut_valido(p TEXT) RETURNS BOOLEAN
LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE
    cuerpo   TEXT;
    dv       TEXT;
    suma     INTEGER := 0;
    factor   INTEGER := 2;
    resto    INTEGER;
    esperado TEXT;
BEGIN
    IF p IS NULL OR p !~ '^[0-9]{7,8}-[0-9K]$' THEN
        RETURN FALSE;
    END IF;
    cuerpo := split_part(p, '-', 1);
    dv     := split_part(p, '-', 2);
    FOR i IN REVERSE length(cuerpo)..1 LOOP
        suma   := suma + substr(cuerpo, i, 1)::INTEGER * factor;
        factor := CASE WHEN factor = 7 THEN 2 ELSE factor + 1 END;
    END LOOP;
    resto    := 11 - (suma % 11);
    esperado := CASE resto WHEN 11 THEN '0' WHEN 10 THEN 'K' ELSE resto::TEXT END;
    RETURN dv = esperado;
END $$;

-- Mantiene updated_at y version (bloqueo optimista).
CREATE OR REPLACE FUNCTION fn_actualizar_version() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    NEW.version    := OLD.version + 1;
    NEW.updated_at := now();
    RETURN NEW;
END $$;

-- Impide UPDATE y DELETE (tablas solo-inserción).
CREATE OR REPLACE FUNCTION fn_solo_insercion() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'La tabla % es solo-inserción: % no permitido', TG_TABLE_NAME, TG_OP
        USING ERRCODE = 'integrity_constraint_violation';
END $$;

-- Impide DELETE (se usan estados o vigencias en su lugar).
CREATE OR REPLACE FUNCTION fn_bloquear_delete() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'No se permite borrar filas de %: usar estado o vigencia', TG_TABLE_NAME
        USING ERRCODE = 'integrity_constraint_violation';
END $$;

-- Folio correlativo por año: MP-2027-00001
CREATE TABLE folio_secuencia (
    anio   SMALLINT PRIMARY KEY,
    ultimo INTEGER NOT NULL DEFAULT 0
);

CREATE OR REPLACE FUNCTION siguiente_folio(p_anio SMALLINT) RETURNS TEXT
LANGUAGE plpgsql AS $$
DECLARE n INTEGER;
BEGIN
    INSERT INTO folio_secuencia (anio, ultimo) VALUES (p_anio, 1)
    ON CONFLICT (anio) DO UPDATE SET ultimo = folio_secuencia.ultimo + 1
    RETURNING ultimo INTO n;
    RETURN 'MP-' || p_anio || '-' || lpad(n::TEXT, 5, '0');
END $$;

-- =====================================================================
-- 1. CATÁLOGOS
-- =====================================================================

CREATE TABLE comuna (
    id     INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE curso (
    id     INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre VARCHAR(20) NOT NULL UNIQUE,          -- ej. '1° A', 'Kinder B'
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

-- =====================================================================
-- 2. FUNCIONARIOS, ROLES Y PERMISOS  (MAT-12)
--    Si MPAZ Web ya tiene usuarios/SSO, reemplazar "funcionario" por esa
--    tabla y mantener las FK. El control real se aplica en el servidor.
-- =====================================================================

CREATE TABLE funcionario (
    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre      VARCHAR(150) NOT NULL,
    correo      VARCHAR(255) NOT NULL UNIQUE,
    sso_sujeto  VARCHAR(255) UNIQUE,             -- identificador en el inicio de sesión único
    activo      BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE rol (
    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    codigo      VARCHAR(30) NOT NULL UNIQUE,
    nombre      VARCHAR(100) NOT NULL
);

CREATE TABLE funcionario_rol (
    funcionario_id INTEGER NOT NULL REFERENCES funcionario(id),
    rol_id         INTEGER NOT NULL REFERENCES rol(id),
    desde          DATE NOT NULL DEFAULT CURRENT_DATE,
    hasta          DATE,
    PRIMARY KEY (funcionario_id, rol_id),
    CONSTRAINT ck_funcionario_rol_fechas CHECK (hasta IS NULL OR hasta >= desde)
);

-- Qué acción puede hacer cada rol en cada área de datos.
CREATE TABLE rol_permiso (
    rol_id INTEGER NOT NULL REFERENCES rol(id),
    area   VARCHAR(30) NOT NULL CHECK (area IN (
               'GENERAL',        -- identificación, matrícula, curso
               'FAMILIA',        -- apoderados y contactos
               'RETIRO',         -- retiro y salida sola
               'SALUD',          -- antecedentes y situaciones médicas
               'PIE',            -- PIE y apoyos
               'JUDICIAL',       -- restricciones y documentos judiciales
               'AUTORIZACIONES', -- imagen, convivencia
               'MEDICACION',     -- prescripciones y administración
               'DOCUMENTOS',     -- adjuntos generales
               'AUDITORIA',
               'ADMIN')),
    accion VARCHAR(15) NOT NULL CHECK (accion IN
               ('LEER', 'CREAR', 'EDITAR', 'EXPORTAR', 'VALIDAR', 'ADMINISTRAR')),
    PRIMARY KEY (rol_id, area, accion)
);

-- Perfiles de la sección 10 (propuesta a validar con Dirección).
INSERT INTO rol (codigo, nombre) VALUES
    ('SECRETARIA',  'Secretaría / matrícula'),
    ('DIRECCION',   'Dirección'),
    ('INSPECTORIA', 'Inspectoría'),
    ('EQUIPO_PIE',  'Equipo PIE'),
    ('TENS',        'TENS'),
    ('DOCENTE',     'Docentes'),
    ('ADMIN_TI',    'Administración TI');

-- Ejemplo de cómo cargar la matriz (NO activado; falta validación institucional):
-- INSERT INTO rol_permiso (rol_id, area, accion)
-- SELECT id, a.area, a.accion FROM rol,
--        (VALUES ('GENERAL','LEER'),('GENERAL','CREAR'),('GENERAL','EDITAR'),
--                ('FAMILIA','LEER'),('FAMILIA','CREAR'),('FAMILIA','EDITAR')) AS a(area, accion)
-- WHERE codigo = 'SECRETARIA';

-- =====================================================================
-- 3. ESTUDIANTE Y PERSONAS  (Bloques A y B)   [Etapa I]
-- =====================================================================

-- Bloque A. Estudiante.  MAT-02: identificador único (RUN, IPE, IPA u otro).
-- Curso, año y estado viven en matricula_anual, no aquí.
CREATE TABLE estudiante (
    id                      INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo_identificador      VARCHAR(10) NOT NULL DEFAULT 'RUN'
                            CHECK (tipo_identificador IN ('RUN', 'IPE', 'IPA', 'OTRO')),
    identificador           VARCHAR(20) NOT NULL,
    nombres                 VARCHAR(100) NOT NULL,
    apellido_paterno        VARCHAR(50)  NOT NULL,
    apellido_materno        VARCHAR(50),
    fecha_nacimiento        DATE NOT NULL,
    nacionalidad            VARCHAR(50)  NOT NULL,
    domicilio               VARCHAR(200) NOT NULL,   -- calle, nº, villa o población (como en la ficha)
    comuna_id               INTEGER REFERENCES comuna(id),  -- opcional: la ficha no la pide
    -- Ficha, sección 7: "se encuentra autorizado para salir solo". NULL = sin responder.
    sale_solo_final_jornada BOOLEAN,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    version                 INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT uk_estudiante_identificador UNIQUE (tipo_identificador, identificador),
    CONSTRAINT ck_estudiante_run CHECK (tipo_identificador <> 'RUN' OR rut_valido(identificador))
);
CREATE INDEX idx_estudiante_comuna ON estudiante(comuna_id);
CREATE INDEX idx_estudiante_nombre_trgm ON estudiante USING gin (
    (lower(nombres || ' ' || apellido_paterno || ' ' || coalesce(apellido_materno, ''))) gin_trgm_ops);

-- Personas adultas: madre, padre, apoderados y cualquier otra persona relacionada.
CREATE TABLE persona (
    id                  INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo_identificador  VARCHAR(10) NOT NULL DEFAULT 'RUN'
                        CHECK (tipo_identificador IN ('RUN', 'IPE', 'IPA', 'OTRO')),
    identificador       VARCHAR(20) NOT NULL,
    nombres             VARCHAR(100) NOT NULL,
    apellidos           VARCHAR(100) NOT NULL,
    telefono            VARCHAR(20),
    telefono_alternativo VARCHAR(20),
    correo              VARCHAR(255)
                        CHECK (correo IS NULL OR correo ~* '^[^@\s]+@[^@\s]+\.[^@\s]+$'),
    domicilio           VARCHAR(200),
    escolaridad         VARCHAR(100),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    version             INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT uk_persona_identificador UNIQUE (tipo_identificador, identificador),
    CONSTRAINT ck_persona_run CHECK (tipo_identificador <> 'RUN' OR rut_valido(identificador))
);
CREATE INDEX idx_persona_nombre_trgm ON persona USING gin (
    (lower(nombres || ' ' || apellidos)) gin_trgm_ops);

-- Relación estudiante–persona con vigencia.
-- Reemplaza a es_principal: la ficha tiene madre, padre, apoderado principal
-- y apoderado secundario, y una persona puede cumplir más de un rol.
--   parentesco       : MADRE / PADRE / OTRO (OTRO exige 'vinculo', ej. 'tía')
--   tipo_apoderado   : PRINCIPAL / SECUNDARIO / NULL (no es apoderado)
-- Una persona con parentesco OTRO y sin tipo_apoderado sirve también para
-- registrar a alguien con restricción que no es apoderado.
CREATE TABLE estudiante_persona (
    id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id    INTEGER NOT NULL REFERENCES estudiante(id),
    persona_id       INTEGER NOT NULL REFERENCES persona(id),
    parentesco       VARCHAR(10) NOT NULL CHECK (parentesco IN ('MADRE', 'PADRE', 'OTRO')),
    vinculo          VARCHAR(50),
    tipo_apoderado   VARCHAR(15) CHECK (tipo_apoderado IN ('PRINCIPAL', 'SECUNDARIO')),
    -- Ficha: "¿existe restricción judicial o familiar que prohíba vincularse?"
    -- NULL = sin responder. El detalle va en restriccion_familiar (área JUDICIAL).
    tiene_restriccion BOOLEAN,
    vigente_desde    DATE NOT NULL DEFAULT CURRENT_DATE,
    vigente_hasta    DATE,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    version          INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT ck_ep_vinculo CHECK (parentesco <> 'OTRO' OR vinculo IS NOT NULL),
    CONSTRAINT ck_ep_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
);
CREATE INDEX idx_ep_estudiante ON estudiante_persona(estudiante_id);
CREATE INDEX idx_ep_persona    ON estudiante_persona(persona_id);
-- Reglas sobre relaciones VIGENTES:
CREATE UNIQUE INDEX uk_ep_persona_vigente     ON estudiante_persona (estudiante_id, persona_id)
    WHERE vigente_hasta IS NULL;
CREATE UNIQUE INDEX uk_ep_principal_vigente   ON estudiante_persona (estudiante_id)
    WHERE vigente_hasta IS NULL AND tipo_apoderado = 'PRINCIPAL';
CREATE UNIQUE INDEX uk_ep_secundario_vigente  ON estudiante_persona (estudiante_id)
    WHERE vigente_hasta IS NULL AND tipo_apoderado = 'SECUNDARIO';
CREATE UNIQUE INDEX uk_ep_madre_padre_vigente ON estudiante_persona (estudiante_id, parentesco)
    WHERE vigente_hasta IS NULL AND parentesco IN ('MADRE', 'PADRE');

-- =====================================================================
-- 4. EMERGENCIA Y RETIRO  (Bloque C)   [Etapa I]
-- =====================================================================

CREATE TABLE contacto_emergencia (
    id             INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id  INTEGER NOT NULL REFERENCES estudiante(id),
    orden          SMALLINT NOT NULL DEFAULT 1 CHECK (orden >= 1),
    nombre         VARCHAR(150) NOT NULL,           -- "Identificación" en la ficha
    telefono       VARCHAR(20)  NOT NULL,
    vinculo        VARCHAR(50)  NOT NULL,
    vigente_desde  DATE NOT NULL DEFAULT CURRENT_DATE,
    vigente_hasta  DATE,
    CONSTRAINT ck_ce_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
);
CREATE UNIQUE INDEX uk_ce_orden_vigente ON contacto_emergencia (estudiante_id, orden)
    WHERE vigente_hasta IS NULL;

-- "El estudiante solo puede ser retirado por:" (nombre, RUT, vínculo)
CREATE TABLE retiro_autorizado (
    id                  INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id       INTEGER NOT NULL REFERENCES estudiante(id),
    nombre              VARCHAR(150) NOT NULL,
    tipo_identificador  VARCHAR(10) NOT NULL DEFAULT 'RUN'
                        CHECK (tipo_identificador IN ('RUN', 'IPE', 'IPA', 'OTRO')),
    identificador       VARCHAR(20) NOT NULL,
    vinculo             VARCHAR(50) NOT NULL,
    vigente_desde       DATE NOT NULL DEFAULT CURRENT_DATE,
    vigente_hasta       DATE,
    CONSTRAINT ck_ra_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde),
    CONSTRAINT ck_ra_run CHECK (tipo_identificador <> 'RUN' OR rut_valido(identificador))
);
CREATE UNIQUE INDEX uk_ra_vigente ON retiro_autorizado (estudiante_id, tipo_identificador, identificador)
    WHERE vigente_hasta IS NULL;

-- =====================================================================
-- 5. MATRÍCULA ANUAL  (MAT-01)   [Etapa I / III]
-- =====================================================================

CREATE TABLE matricula_anual (
    id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id    INTEGER NOT NULL REFERENCES estudiante(id),
    anio             SMALLINT NOT NULL CHECK (anio BETWEEN 2024 AND 2100),
    curso_id         INTEGER REFERENCES curso(id),   -- encabezado de la ficha
    tipo             VARCHAR(12) NOT NULL CHECK (tipo IN ('NUEVA', 'RENOVACION')),
    estado           VARCHAR(15) NOT NULL DEFAULT 'BORRADOR' CHECK (estado IN (
                         'BORRADOR',        -- paso 1-3 del proceso
                         'FICHA_GENERADA',  -- PDF cerrado, pendiente de firma
                         'FIRMADA',         -- escaneo subido
                         'CONFIRMADA',      -- confirmada según procedimiento oficial
                         'ANULADA')),
    confirmada_en    TIMESTAMPTZ,
    confirmada_por   INTEGER REFERENCES funcionario(id),
    observaciones    TEXT,
    creada_por       INTEGER REFERENCES funcionario(id),
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    version          INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT ck_matricula_confirmada CHECK (
        estado <> 'CONFIRMADA' OR (confirmada_en IS NOT NULL AND confirmada_por IS NOT NULL))
);
-- Una sola matrícula no anulada por estudiante y año.
CREATE UNIQUE INDEX uk_matricula_estudiante_anio ON matricula_anual (estudiante_id, anio)
    WHERE estado <> 'ANULADA';
CREATE INDEX idx_matricula_curso_anio ON matricula_anual (anio, curso_id, estado);

-- =====================================================================
-- 6. DOCUMENTOS  (Bloque I, MAT-06, MAT-07, MAT-09)   [Etapa II]
-- =====================================================================

CREATE TABLE documento (
    id                   INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id        INTEGER NOT NULL REFERENCES estudiante(id),
    matricula_id         INTEGER REFERENCES matricula_anual(id),
    tipo                 VARCHAR(30) NOT NULL CHECK (tipo IN (
                             'FICHA_PDF', 'FICHA_FIRMADA', 'ANEXO_FIRMADO',
                             'CEDULA_IDENTIDAD', 'CERTIFICADO_NACIMIENTO', 'PODER_TERCERO',
                             'ORDEN_MEDICA', 'RESPALDO_SALUD',
                             'CERTIFICADO_PIE', 'DOCUMENTO_JUDICIAL', 'OTRO')),
    -- Área de permiso que controla quién puede verlo.
    sensibilidad         VARCHAR(10) NOT NULL DEFAULT 'GENERAL'
                         CHECK (sensibilidad IN ('GENERAL', 'SALUD', 'PIE', 'JUDICIAL')),
    nombre_original      VARCHAR(255) NOT NULL,
    mime_type            VARCHAR(100) NOT NULL
                         CHECK (mime_type IN ('application/pdf', 'image/jpeg', 'image/png')),
    tamano_bytes         INTEGER NOT NULL,
    ruta_almacenamiento  TEXT NOT NULL UNIQUE,     -- ruta relativa, fuera de directorios públicos
    hash_sha256          CHAR(64) NOT NULL CHECK (hash_sha256 ~ '^[0-9a-f]{64}$'),
    estado_analisis      VARCHAR(10) NOT NULL DEFAULT 'PENDIENTE'
                         CHECK (estado_analisis IN ('PENDIENTE', 'LIMPIO', 'RECHAZADO')),
    activo               BOOLEAN NOT NULL DEFAULT TRUE,
    reemplaza_id         INTEGER REFERENCES documento(id),   -- MAT-13: el anterior se conserva
    subido_por           INTEGER NOT NULL REFERENCES funcionario(id),
    subido_en            TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_documento_tamano CHECK (tamano_bytes BETWEEN 1 AND 15728640),  -- 15 MB
    -- Un documento sensible nunca puede quedar clasificado como GENERAL.
    CONSTRAINT ck_documento_sensibilidad CHECK (
        CASE tipo
            WHEN 'ORDEN_MEDICA'       THEN sensibilidad = 'SALUD'
            WHEN 'RESPALDO_SALUD'     THEN sensibilidad = 'SALUD'
            WHEN 'CERTIFICADO_PIE'    THEN sensibilidad = 'PIE'
            WHEN 'DOCUMENTO_JUDICIAL' THEN sensibilidad = 'JUDICIAL'
            ELSE TRUE
        END)
);
CREATE INDEX idx_documento_estudiante ON documento(estudiante_id, tipo);
CREATE INDEX idx_documento_matricula  ON documento(matricula_id);

-- Estado de cada documento requerido: recibido / no aplica / pendiente.
-- Nunca bloquea la matrícula por sí mismo (MAT-07).
CREATE TABLE requisito_documental (
    id              INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    matricula_id    INTEGER NOT NULL REFERENCES matricula_anual(id),
    tipo_documento  VARCHAR(30) NOT NULL CHECK (tipo_documento IN (
                        'CEDULA_IDENTIDAD', 'CERTIFICADO_NACIMIENTO', 'PODER_TERCERO',
                        'ORDEN_MEDICA', 'RESPALDO_SALUD', 'CERTIFICADO_PIE',
                        'DOCUMENTO_JUDICIAL', 'OTRO')),
    estado          VARCHAR(10) NOT NULL DEFAULT 'PENDIENTE'
                    CHECK (estado IN ('RECIBIDO', 'NO_APLICA', 'PENDIENTE')),
    documento_id    INTEGER REFERENCES documento(id),
    observacion     TEXT,
    registrado_por  INTEGER REFERENCES funcionario(id),
    registrado_en   TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    version         INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT uk_requisito UNIQUE (matricula_id, tipo_documento),
    CONSTRAINT ck_requisito_recibido CHECK (estado <> 'RECIBIDO' OR documento_id IS NOT NULL)
);

-- =====================================================================
-- 7. FICHA GENERADA, FIRMA Y ARCHIVO FÍSICO  (MAT-08, 09, 10, 14, 18)   [Etapa I–II]
-- =====================================================================

-- Cada fila es una versión cerrada de la ficha de una matrícula.
-- Una corrección NO edita la ficha firmada: crea una versión nueva.
CREATE TABLE ficha_version (
    id                        INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    matricula_id              INTEGER NOT NULL REFERENCES matricula_anual(id),
    numero_version            SMALLINT NOT NULL DEFAULT 1 CHECK (numero_version >= 1),
    folio                     VARCHAR(20) NOT NULL UNIQUE,   -- se asigna solo (trigger)
    estado                    VARCHAR(12) NOT NULL DEFAULT 'VIGENTE'
                              CHECK (estado IN ('VIGENTE', 'REEMPLAZADA')),
    datos_snapshot            JSONB NOT NULL,                -- copia congelada de todos los datos
    motivo_correccion         TEXT,
    generada_por              INTEGER NOT NULL REFERENCES funcionario(id),
    generada_en               TIMESTAMPTZ NOT NULL DEFAULT now(),
    pdf_documento_id          INTEGER REFERENCES documento(id),
    -- Firma manuscrita y escaneo
    firmante_persona_id       INTEGER REFERENCES persona(id),
    fecha_firma               DATE,
    escaneo_documento_id      INTEGER REFERENCES documento(id),
    escaneo_subido_por        INTEGER REFERENCES funcionario(id),
    escaneo_subido_en         TIMESTAMPTZ,
    -- Archivo físico y validación documental (MAT-10)
    ubicacion_original_fisico VARCHAR(200),
    estado_validacion         VARCHAR(10) NOT NULL DEFAULT 'PENDIENTE'
                              CHECK (estado_validacion IN ('PENDIENTE', 'VALIDADA', 'OBSERVADA')),
    validado_por              INTEGER REFERENCES funcionario(id),
    validado_en               TIMESTAMPTZ,
    observacion_validacion    TEXT,
    CONSTRAINT uk_ficha_version UNIQUE (matricula_id, numero_version),
    CONSTRAINT ck_ficha_correccion CHECK (numero_version = 1 OR motivo_correccion IS NOT NULL),
    CONSTRAINT ck_ficha_validada CHECK (
        estado_validacion <> 'VALIDADA'
        OR (escaneo_documento_id IS NOT NULL AND fecha_firma IS NOT NULL
            AND validado_por IS NOT NULL AND validado_en IS NOT NULL))
);
-- Una sola versión vigente por matrícula.
CREATE UNIQUE INDEX uk_ficha_vigente ON ficha_version (matricula_id) WHERE estado = 'VIGENTE';
CREATE INDEX idx_ficha_folio ON ficha_version (folio);

CREATE OR REPLACE FUNCTION fn_ficha_asignar_folio() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.folio IS NULL OR NEW.folio = '' THEN
        NEW.folio := siguiente_folio((SELECT anio FROM matricula_anual WHERE id = NEW.matricula_id));
    END IF;
    RETURN NEW;
END $$;

-- Lo generado y lo validado no se modifica.
CREATE OR REPLACE FUNCTION fn_ficha_proteger() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.matricula_id   <> OLD.matricula_id
       OR NEW.numero_version <> OLD.numero_version
       OR NEW.folio       <> OLD.folio
       OR NEW.datos_snapshot IS DISTINCT FROM OLD.datos_snapshot
       OR NEW.pdf_documento_id IS DISTINCT FROM OLD.pdf_documento_id
       OR NEW.generada_en <> OLD.generada_en THEN
        RAISE EXCEPTION 'La ficha % está cerrada: crear una versión nueva en vez de editarla', OLD.folio
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    IF OLD.estado_validacion = 'VALIDADA' AND (
           NEW.escaneo_documento_id IS DISTINCT FROM OLD.escaneo_documento_id
        OR NEW.firmante_persona_id  IS DISTINCT FROM OLD.firmante_persona_id
        OR NEW.fecha_firma          IS DISTINCT FROM OLD.fecha_firma) THEN
        RAISE EXCEPTION 'La ficha % ya fue validada: no se puede cambiar su firma ni su escaneo', OLD.folio
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    RETURN NEW;
END $$;

CREATE TRIGGER trg_ficha_folio   BEFORE INSERT ON ficha_version
    FOR EACH ROW EXECUTE FUNCTION fn_ficha_asignar_folio();
CREATE TRIGGER trg_ficha_proteger BEFORE UPDATE ON ficha_version
    FOR EACH ROW EXECUTE FUNCTION fn_ficha_proteger();
CREATE TRIGGER trg_ficha_no_delete BEFORE DELETE ON ficha_version
    FOR EACH ROW EXECUTE FUNCTION fn_bloquear_delete();
CREATE TRIGGER trg_documento_no_delete BEFORE DELETE ON documento
    FOR EACH ROW EXECUTE FUNCTION fn_bloquear_delete();

-- =====================================================================
-- 8. SALUD  (Bloque D)   [Etapa I]   — área SALUD
-- =====================================================================

CREATE TABLE antecedente_salud (
    id                         INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id              INTEGER NOT NULL UNIQUE REFERENCES estudiante(id),
    -- Ficha: "cuenta con alguna situación médica, cualquiera que sea". NULL = sin responder.
    tiene_situacion_medica     BOOLEAN,
    necesidades_atencion       TEXT,
    contacto_sanitario_nombre  VARCHAR(150),
    contacto_sanitario_telefono VARCHAR(20),
    created_at                 TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                 TIMESTAMPTZ NOT NULL DEFAULT now(),
    version                    INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE situacion_medica (
    id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id    INTEGER NOT NULL REFERENCES estudiante(id),
    tipo             VARCHAR(10) NOT NULL DEFAULT 'CONDICION'
                     CHECK (tipo IN ('ALERGIA', 'CONDICION', 'OTRO')),
    descripcion      VARCHAR(255) NOT NULL,
    en_tratamiento   BOOLEAN NOT NULL DEFAULT FALSE,
    respaldo_documento_id INTEGER REFERENCES documento(id),
    vigente_desde    DATE NOT NULL DEFAULT CURRENT_DATE,
    vigente_hasta    DATE,
    CONSTRAINT ck_sm_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
);
CREATE INDEX idx_situacion_medica_estudiante ON situacion_medica(estudiante_id);

-- =====================================================================
-- 9. PIE Y APOYOS  (Bloque E)   [Etapa I]   — área PIE
-- =====================================================================

CREATE TABLE antecedente_apoyo (
    id                  INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id       INTEGER NOT NULL UNIQUE REFERENCES estudiante(id),
    participa_pie       VARCHAR(10) NOT NULL DEFAULT 'PENDIENTE'
                        CHECK (participa_pie IN ('SI', 'NO', 'PENDIENTE')),
    apoyos_informados   TEXT,
    tiene_diagnostico   BOOLEAN,              -- existencia de diagnóstico o documentación
    documento_id        INTEGER REFERENCES documento(id),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    version             INTEGER NOT NULL DEFAULT 1
);

-- =====================================================================
-- 10. RESTRICCIONES FAMILIARES / JUDICIALES  (Bloque B)   — área JUDICIAL
-- =====================================================================

CREATE TABLE restriccion_familiar (
    id                    INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_persona_id INTEGER NOT NULL REFERENCES estudiante_persona(id),
    tipo                  VARCHAR(10) NOT NULL CHECK (tipo IN ('JUDICIAL', 'FAMILIAR')),
    descripcion           TEXT NOT NULL,
    documento_id          INTEGER REFERENCES documento(id),
    vigente_desde         DATE NOT NULL DEFAULT CURRENT_DATE,
    vigente_hasta         DATE,
    registrada_por        INTEGER REFERENCES funcionario(id),
    CONSTRAINT ck_rf_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
);
CREATE INDEX idx_rf_ep ON restriccion_familiar(estudiante_persona_id);

-- =====================================================================
-- 11. AUTORIZACIONES: IMAGEN Y CONVIVENCIA  (Bloques F, G; MAT-04)   [Etapa II]
-- =====================================================================

-- Texto exacto que el apoderado acepta. Cada cambio de texto = versión nueva.
-- Queda vacía hasta que Dirección, Convivencia y el sostenedor aprueben los textos.
CREATE TABLE plantilla_autorizacion (
    id             INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo           VARCHAR(15) NOT NULL CHECK (tipo IN ('IMAGEN', 'CONVIVENCIA', 'MEDICACION')),
    version        SMALLINT NOT NULL CHECK (version >= 1),
    texto          TEXT NOT NULL,
    aprobado_por   VARCHAR(150),
    vigente_desde  DATE NOT NULL,
    vigente_hasta  DATE,
    CONSTRAINT uk_plantilla UNIQUE (tipo, version),
    CONSTRAINT uk_plantilla_tipo UNIQUE (id, tipo),
    CONSTRAINT ck_plantilla_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
);

CREATE TABLE autorizacion (
    id                    INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id         INTEGER NOT NULL REFERENCES estudiante(id),
    tipo                  VARCHAR(15) NOT NULL CHECK (tipo IN ('IMAGEN', 'CONVIVENCIA', 'MEDICACION')),
    plantilla_id          INTEGER NOT NULL,
    estado                VARCHAR(12) NOT NULL DEFAULT 'PENDIENTE' CHECK (estado IN (
                              'PENDIENTE', 'OTORGADA', 'RECHAZADA',
                              'REVOCADA', 'VENCIDA', 'REEMPLAZADA')),
    proposito             TEXT,                          -- ej. para IMAGEN
    firmante_persona_id   INTEGER REFERENCES persona(id),
    fecha_firma           DATE,
    ficha_version_id      INTEGER REFERENCES ficha_version(id),   -- ficha en que se firmó
    documento_id          INTEGER REFERENCES documento(id),       -- escaneo firmado
    vigente_desde         DATE,
    vigente_hasta         DATE,
    revocada_en           TIMESTAMPTZ,
    revocada_registrada_por INTEGER REFERENCES funcionario(id),
    motivo_revocacion     TEXT,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    version               INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT fk_autorizacion_plantilla FOREIGN KEY (plantilla_id, tipo)
        REFERENCES plantilla_autorizacion (id, tipo),
    CONSTRAINT uk_autorizacion_compuesta UNIQUE (id, estudiante_id, tipo),
    CONSTRAINT ck_aut_vigencia CHECK (vigente_hasta IS NULL OR vigente_desde IS NULL
                                      OR vigente_hasta >= vigente_desde),
    -- Una autorización otorgada tiene firmante y fecha. Una revocada, fecha de revocación.
    CONSTRAINT ck_aut_otorgada CHECK (
        estado <> 'OTORGADA' OR (firmante_persona_id IS NOT NULL AND fecha_firma IS NOT NULL)),
    CONSTRAINT ck_aut_revocada CHECK (estado <> 'REVOCADA' OR revocada_en IS NOT NULL)
);
CREATE INDEX idx_autorizacion_estudiante ON autorizacion(estudiante_id, tipo, estado);
-- Imagen y convivencia: una sola autorización otorgada a la vez por estudiante.
-- (La medicación admite varias: una por medicamento.)
CREATE UNIQUE INDEX uk_autorizacion_otorgada ON autorizacion (estudiante_id, tipo)
    WHERE estado = 'OTORGADA' AND tipo IN ('IMAGEN', 'CONVIVENCIA');

-- Alcance por canal: permite autorizar o rechazar cada uno (sección 7.2).
--   IMAGEN: 'CAPTACION_INTERNA', 'SITIO_INSTITUCIONAL', 'REDES_SOCIALES', 'OTROS_MEDIOS'
--   CONVIVENCIA: tipo de acompañamiento o intervención definido en el texto aprobado
CREATE TABLE autorizacion_alcance (
    id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    autorizacion_id  INTEGER NOT NULL REFERENCES autorizacion(id),
    alcance          VARCHAR(60) NOT NULL,
    otorgado         BOOLEAN NOT NULL,
    CONSTRAINT uk_alcance UNIQUE (autorizacion_id, alcance)
);

CREATE TRIGGER trg_autorizacion_no_delete BEFORE DELETE ON autorizacion
    FOR EACH ROW EXECUTE FUNCTION fn_bloquear_delete();

-- =====================================================================
-- 12. MEDICACIÓN POR TENS  (Bloque H; MAT-05, MAT-17)   [Etapa II]   — área MEDICACION
--     Sustituye a "autoriza_paracetamol". La ficha por sí sola no es orden
--     para medicar: toda administración exige orden médica y autorización.
-- =====================================================================

CREATE TABLE prescripcion (
    id                    INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id         INTEGER NOT NULL REFERENCES estudiante(id),
    autorizacion_id       INTEGER NOT NULL UNIQUE,       -- una autorización por medicamento
    tipo_autorizacion     VARCHAR(15) NOT NULL DEFAULT 'MEDICACION'
                          CHECK (tipo_autorizacion = 'MEDICACION'),
    medicamento           VARCHAR(150) NOT NULL,
    dosis_cantidad        NUMERIC(8, 2) NOT NULL CHECK (dosis_cantidad > 0),
    dosis_unidad          VARCHAR(20) NOT NULL,          -- mg, ml, gotas, comprimidos
    via                   VARCHAR(30) NOT NULL,
    frecuencia            VARCHAR(100) NOT NULL,
    horario               VARCHAR(100),
    precauciones          TEXT,
    medico_nombre         VARCHAR(150),
    orden_documento_id    INTEGER NOT NULL REFERENCES documento(id),   -- orden médica adjunta
    vigente_desde         DATE NOT NULL,
    vigente_hasta         DATE NOT NULL,
    estado                VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE_VALIDACION'
                          CHECK (estado IN ('PENDIENTE_VALIDACION', 'VIGENTE', 'SUSPENDIDA', 'VENCIDA')),
    validada_por          INTEGER REFERENCES funcionario(id),      -- verifica la orden antes de habilitar
    validada_en           TIMESTAMPTZ,
    tens_responsable_id   INTEGER REFERENCES funcionario(id),
    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    version               INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT fk_prescripcion_autorizacion FOREIGN KEY (autorizacion_id, estudiante_id, tipo_autorizacion)
        REFERENCES autorizacion (id, estudiante_id, tipo),
    CONSTRAINT ck_prescripcion_vigencia CHECK (vigente_hasta >= vigente_desde),
    CONSTRAINT ck_prescripcion_validada CHECK (
        estado <> 'VIGENTE' OR (validada_por IS NOT NULL AND validada_en IS NOT NULL))
);
CREATE INDEX idx_prescripcion_estudiante ON prescripcion(estudiante_id, estado);
CREATE INDEX idx_prescripcion_vencimiento ON prescripcion(vigente_hasta) WHERE estado = 'VIGENTE';
CREATE TRIGGER trg_prescripcion_no_delete BEFORE DELETE ON prescripcion
    FOR EACH ROW EXECUTE FUNCTION fn_bloquear_delete();

-- Registro de cada dosis efectivamente administrada. Solo-inserción.
-- Una corrección se registra como fila nueva que apunta a la anterior.
CREATE TABLE administracion_medicamento (
    id                INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    prescripcion_id   INTEGER NOT NULL REFERENCES prescripcion(id),
    administrado_en   TIMESTAMPTZ NOT NULL,
    dosis_cantidad    NUMERIC(8, 2) NOT NULL CHECK (dosis_cantidad > 0),
    dosis_unidad      VARCHAR(20) NOT NULL,
    tens_id           INTEGER NOT NULL REFERENCES funcionario(id),
    incidencias       TEXT,
    observaciones     TEXT,
    corrige_a_id      INTEGER REFERENCES administracion_medicamento(id),
    registrado_en     TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_adm_prescripcion ON administracion_medicamento(prescripcion_id, administrado_en);

-- MAT-05: solo un TENS registra, y solo con orden vigente y validada
-- + autorización otorgada, no revocada y dentro de su vigencia.
CREATE OR REPLACE FUNCTION fn_validar_administracion() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
DECLARE
    p   prescripcion%ROWTYPE;
    a   autorizacion%ROWTYPE;
    dia DATE := (NEW.administrado_en AT TIME ZONE 'America/Santiago')::DATE;
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM funcionario_rol fr
        JOIN rol r ON r.id = fr.rol_id
        JOIN funcionario f ON f.id = fr.funcionario_id
        WHERE fr.funcionario_id = NEW.tens_id AND r.codigo = 'TENS'
          AND f.activo AND (fr.hasta IS NULL OR fr.hasta >= dia)) THEN
        RAISE EXCEPTION 'Solo un funcionario con rol TENS activo puede registrar administración de medicamentos'
            USING ERRCODE = 'insufficient_privilege';
    END IF;

    SELECT * INTO p FROM prescripcion  WHERE id = NEW.prescripcion_id;
    SELECT * INTO a FROM autorizacion  WHERE id = p.autorizacion_id;

    IF p.estado <> 'VIGENTE' OR p.validada_en IS NULL THEN
        RAISE EXCEPTION 'La orden médica no está vigente y validada (estado: %)', p.estado
            USING ERRCODE = 'check_violation';
    END IF;
    IF dia < p.vigente_desde OR dia > p.vigente_hasta THEN
        RAISE EXCEPTION 'La fecha % está fuera de la vigencia de la orden (% a %)',
            dia, p.vigente_desde, p.vigente_hasta USING ERRCODE = 'check_violation';
    END IF;
    IF a.estado <> 'OTORGADA' OR a.revocada_en IS NOT NULL THEN
        RAISE EXCEPTION 'La autorización del apoderado no está otorgada (estado: %)', a.estado
            USING ERRCODE = 'check_violation';
    END IF;
    IF (a.vigente_desde IS NOT NULL AND dia < a.vigente_desde)
       OR (a.vigente_hasta IS NOT NULL AND dia > a.vigente_hasta) THEN
        RAISE EXCEPTION 'La autorización del apoderado está fuera de su vigencia'
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END $$;

CREATE TRIGGER trg_adm_validar BEFORE INSERT ON administracion_medicamento
    FOR EACH ROW EXECUTE FUNCTION fn_validar_administracion();
CREATE TRIGGER trg_adm_solo_insercion BEFORE UPDATE OR DELETE ON administracion_medicamento
    FOR EACH ROW EXECUTE FUNCTION fn_solo_insercion();

-- =====================================================================
-- 13. AUDITORÍA  (MAT-13)   [Etapa III]   — solo-inserción
-- =====================================================================

-- Cambios de datos, registrados por trigger.
CREATE TABLE historial_cambios (
    id                BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tabla             VARCHAR(63) NOT NULL,
    registro_id       BIGINT NOT NULL,
    accion            VARCHAR(6) NOT NULL CHECK (accion IN ('INSERT', 'UPDATE', 'DELETE')),
    datos_anteriores  JSONB,
    datos_nuevos      JSONB,
    funcionario_id    INTEGER REFERENCES funcionario(id),
    cambiado_en       TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_historial_registro ON historial_cambios (tabla, registro_id, cambiado_en);
CREATE INDEX idx_historial_funcionario ON historial_cambios (funcionario_id, cambiado_en);

-- Accesos, búsquedas, descargas y denegaciones. Los escribe la app.
CREATE TABLE registros_acceso (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    funcionario_id  INTEGER REFERENCES funcionario(id),
    accion          VARCHAR(12) NOT NULL CHECK (accion IN
                        ('LEER', 'BUSCAR', 'DESCARGAR', 'EXPORTAR', 'REEMPLAZAR', 'DENEGADO')),
    area            VARCHAR(30),
    recurso         VARCHAR(63),
    recurso_id      BIGINT,
    estudiante_id   INTEGER REFERENCES estudiante(id),
    detalle         JSONB,
    ip              INET,
    ocurrido_en     TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_acceso_estudiante ON registros_acceso (estudiante_id, ocurrido_en);
CREATE INDEX idx_acceso_funcionario ON registros_acceso (funcionario_id, ocurrido_en);

CREATE TRIGGER trg_historial_solo_insercion BEFORE UPDATE OR DELETE ON historial_cambios
    FOR EACH ROW EXECUTE FUNCTION fn_solo_insercion();
CREATE TRIGGER trg_acceso_solo_insercion BEFORE UPDATE OR DELETE ON registros_acceso
    FOR EACH ROW EXECUTE FUNCTION fn_solo_insercion();

-- Trigger genérico: guarda la fila anterior y la nueva en historial_cambios.
-- Requiere que la tabla tenga columna "id". Toma el funcionario de la
-- variable de sesión app.funcionario_id.
CREATE OR REPLACE FUNCTION fn_auditar_cambio() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
DECLARE
    v_func INTEGER := NULLIF(current_setting('app.funcionario_id', TRUE), '')::INTEGER;
BEGIN
    IF TG_OP = 'DELETE' THEN
        INSERT INTO historial_cambios (tabla, registro_id, accion, datos_anteriores, funcionario_id)
        VALUES (TG_TABLE_NAME, (to_jsonb(OLD) ->> 'id')::BIGINT, 'DELETE', to_jsonb(OLD), v_func);
    ELSIF TG_OP = 'UPDATE' THEN
        INSERT INTO historial_cambios (tabla, registro_id, accion, datos_anteriores, datos_nuevos, funcionario_id)
        VALUES (TG_TABLE_NAME, (to_jsonb(NEW) ->> 'id')::BIGINT, 'UPDATE', to_jsonb(OLD), to_jsonb(NEW), v_func);
    ELSE
        INSERT INTO historial_cambios (tabla, registro_id, accion, datos_nuevos, funcionario_id)
        VALUES (TG_TABLE_NAME, (to_jsonb(NEW) ->> 'id')::BIGINT, 'INSERT', to_jsonb(NEW), v_func);
    END IF;
    RETURN NULL;
END $$;

-- =====================================================================
-- 14. TRIGGERS DE VERSIÓN Y AUDITORÍA
-- =====================================================================

DO $$
DECLARE t TEXT;
BEGIN
    -- Bloqueo optimista + updated_at
    FOREACH t IN ARRAY ARRAY[
        'estudiante', 'persona', 'estudiante_persona', 'matricula_anual',
        'requisito_documental', 'antecedente_salud', 'antecedente_apoyo',
        'autorizacion', 'prescripcion']
    LOOP
        EXECUTE format(
            'CREATE TRIGGER trg_%1$s_version BEFORE UPDATE ON %1$I
             FOR EACH ROW EXECUTE FUNCTION fn_actualizar_version()', t);
    END LOOP;

    -- Auditoría de cambios (datos personales y sensibles: restringir lectura de
    -- historial_cambios al área AUDITORIA, porque guarda copias de estas filas)
    FOREACH t IN ARRAY ARRAY[
        'estudiante', 'persona', 'estudiante_persona', 'restriccion_familiar',
        'contacto_emergencia', 'retiro_autorizado', 'matricula_anual',
        'documento', 'requisito_documental', 'ficha_version',
        'antecedente_salud', 'situacion_medica', 'antecedente_apoyo',
        'autorizacion', 'autorizacion_alcance', 'prescripcion',
        'plantilla_autorizacion']
    LOOP
        EXECUTE format(
            'CREATE TRIGGER trg_%1$s_auditoria AFTER INSERT OR UPDATE OR DELETE ON %1$I
             FOR EACH ROW EXECUTE FUNCTION fn_auditar_cambio()', t);
    END LOOP;
END $$;

-- funcionario_rol tiene clave compuesta, no "id": se audita con un trigger propio.
CREATE OR REPLACE FUNCTION fn_auditar_funcionario_rol() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
DECLARE
    v_func INTEGER := NULLIF(current_setting('app.funcionario_id', TRUE), '')::INTEGER;
    r funcionario_rol%ROWTYPE;
BEGIN
    r := CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
    INSERT INTO historial_cambios (tabla, registro_id, accion, datos_anteriores, datos_nuevos, funcionario_id)
    VALUES ('funcionario_rol', r.funcionario_id, TG_OP,
            CASE WHEN TG_OP <> 'INSERT' THEN to_jsonb(OLD) END,
            CASE WHEN TG_OP <> 'DELETE' THEN to_jsonb(NEW) END, v_func);
    RETURN NULL;
END $$;
CREATE TRIGGER trg_funcionario_rol_auditoria AFTER INSERT OR UPDATE OR DELETE ON funcionario_rol
    FOR EACH ROW EXECUTE FUNCTION fn_auditar_funcionario_rol();

-- =====================================================================
-- 15. VISTAS DE APOYO
-- =====================================================================

-- Autorizaciones vigentes hoy, por canal (MAT-04). Una revocación o un
-- vencimiento hace que la fila deje de aparecer: difusión bloqueada.
CREATE VIEW v_autorizaciones_vigentes AS
SELECT a.estudiante_id, a.tipo, al.alcance, a.id AS autorizacion_id,
       a.fecha_firma, a.vigente_hasta
FROM autorizacion a
JOIN autorizacion_alcance al ON al.autorizacion_id = a.id AND al.otorgado
WHERE a.estado = 'OTORGADA'
  AND a.revocada_en IS NULL
  AND (a.vigente_desde IS NULL OR a.vigente_desde <= CURRENT_DATE)
  AND (a.vigente_hasta IS NULL OR a.vigente_hasta >= CURRENT_DATE);

-- Órdenes médicas y autorizaciones vencidas o por vencer en 30 días (MAT-17).
CREATE VIEW v_alertas_vencimiento AS
SELECT 'PRESCRIPCION'::TEXT AS origen, p.id AS registro_id, p.estudiante_id,
       p.vigente_hasta AS vence_el, (p.vigente_hasta < CURRENT_DATE) AS vencida
FROM prescripcion p
WHERE p.estado IN ('VIGENTE', 'PENDIENTE_VALIDACION')
  AND p.vigente_hasta <= CURRENT_DATE + 30
UNION ALL
SELECT 'AUTORIZACION', a.id, a.estudiante_id,
       a.vigente_hasta, (a.vigente_hasta < CURRENT_DATE)
FROM autorizacion a
WHERE a.estado = 'OTORGADA' AND a.vigente_hasta IS NOT NULL
  AND a.vigente_hasta <= CURRENT_DATE + 30;

-- Pendientes documentales por matrícula (MAT-16).
CREATE VIEW v_pendientes_documentales AS
SELECT m.anio, c.nombre AS curso, e.id AS estudiante_id,
       e.tipo_identificador, e.identificador,
       e.apellido_paterno, e.nombres,
       r.tipo_documento, r.observacion, m.id AS matricula_id, m.estado AS estado_matricula
FROM requisito_documental r
JOIN matricula_anual m ON m.id = r.matricula_id AND m.estado <> 'ANULADA'
JOIN estudiante e      ON e.id = m.estudiante_id
LEFT JOIN curso c      ON c.id = m.curso_id
WHERE r.estado = 'PENDIENTE';

-- =====================================================================
-- 16. ROLES DE BASE DE DATOS (ejemplo, NO activado)
--     La app debería conectarse con un rol sin permiso de borrar ni
--     modificar las tablas solo-inserción, además de los triggers.
-- =====================================================================
-- CREATE ROLE mpaz_app LOGIN PASSWORD '...';
-- GRANT USAGE ON SCHEMA public TO mpaz_app;
-- GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO mpaz_app;
-- REVOKE UPDATE, DELETE ON historial_cambios, registros_acceso,
--        administracion_medicamento FROM mpaz_app;
-- GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO mpaz_app;

COMMIT;
