-- PARTE 1 / 3: BD (estructura). PostgreSQL 14+.
-- Crear primero una base VACÍA con su administrador de PostgreSQL y conectarse a ella.
-- Ejecutar este archivo completo; después 02-programacion-pl.sql y 03-datos-iniciales.sql.
-- No usar sobre una base existente. No contiene funciones, triggers ni carga de datos.
-- Las restricciones RUN se agregan en la parte PL: no usar la app hasta completar las 3 partes.
BEGIN;
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


-- Todo el portal vive en su propio esquema: así esta base puede convivir con
-- otras bases del colegio sin mezclar tablas homónimas.
CREATE SCHEMA IF NOT EXISTS mpaz_matricula;
CREATE EXTENSION IF NOT EXISTS pg_trgm;   -- búsqueda por nombre (MAT-11), queda en public
SET LOCAL search_path TO mpaz_matricula, public;

-- =====================================================================
-- 0. FUNCIONES AUXILIARES
-- =====================================================================

-- Normaliza un RUN: quita puntos y espacios, agrega guion, K mayúscula.


-- Valida formato y dígito verificador (módulo 11) de un RUN normalizado.


-- Mantiene updated_at y version (bloqueo optimista).


-- Impide UPDATE y DELETE (tablas solo-inserción).


-- Impide DELETE (se usan estados o vigencias en su lugar).


-- Folio correlativo por año: MP-2027-00001
CREATE TABLE folio_secuencia (
    anio   SMALLINT PRIMARY KEY,
    ultimo INTEGER NOT NULL DEFAULT 0
);



-- =====================================================================
-- 1. CATÁLOGOS
-- =====================================================================

CREATE TABLE comuna (
    id     INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE curso (
    id     INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre VARCHAR(20) NOT NULL UNIQUE,          -- ej. 'PK A', '1° A', '8° B'
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
    CONSTRAINT uk_estudiante_identificador UNIQUE (tipo_identificador, identificador)
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
    CONSTRAINT uk_persona_identificador UNIQUE (tipo_identificador, identificador)
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
    CONSTRAINT ck_ra_vigencia CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
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



-- Lo generado y lo validado no se modifica.







-- =====================================================================
-- 8. SALUD  (Bloque D)   [Etapa I]   — área SALUD
-- =====================================================================

CREATE TABLE antecedente_salud (
    id                         INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estudiante_id              INTEGER NOT NULL UNIQUE REFERENCES estudiante(id),
    -- Ficha: "cuenta con alguna situación médica, cualquiera que sea". NULL = sin responder.
    tiene_situacion_medica     BOOLEAN,
    necesidades_atencion       TEXT,
    -- ¿Requiere salir al baño durante la jornada? (MAT-05, columna propia).
    sale_al_bano               BOOLEAN,
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




-- Trigger genérico: guarda la fila anterior y la nueva en historial_cambios.
-- Requiere que la tabla tenga columna "id". Toma el funcionario de la
-- variable de sesión app.funcionario_id.


-- =====================================================================
-- 14. TRIGGERS DE VERSIÓN Y AUDITORÍA
-- =====================================================================



-- funcionario_rol tiene clave compuesta, no "id": se audita con un trigger propio.



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
--
-- Reglas de acotamiento, para que las alertas no se acumulen para siempre:
--   1. Incluye las autorizaciones ya marcadas VENCIDA: al consultar al alumno,
--      la aplicación cambia OTORGADA → VENCIDA y, sin este filtro, la alerta
--      desaparecería del contador justo cuando alguien la revisa.
--   2. No lista una autorización que ya fue reemplazada: si existe otra
--      OTORGADA vigente del mismo tipo y estudiante registrada después, la fila
--      anterior es historia y se consulta en la ficha, no como alerta. La
--      comparación usa la fecha y, si falta, el id de la fila, de modo que una
--      autorización sin vigente_desde ni fecha_firma también queda excluida.
--      Sólo aplica a imagen y convivencia: la medicación admite una
--      autorización por medicamento y sus filas no se reemplazan entre sí.
--   3. Pasados 90 días desde el vencimiento la alerta deja de listarse; el
--      registro sigue en la ficha y en el historial.
CREATE VIEW v_alertas_vencimiento AS
SELECT 'PRESCRIPCION'::TEXT AS origen, p.id AS registro_id, p.estudiante_id,
       p.vigente_hasta AS vence_el, (p.vigente_hasta < CURRENT_DATE) AS vencida
FROM prescripcion p
WHERE p.estado IN ('VIGENTE', 'PENDIENTE_VALIDACION')
  AND p.vigente_hasta BETWEEN CURRENT_DATE - 90 AND CURRENT_DATE + 30
UNION ALL
SELECT 'AUTORIZACION', a.id, a.estudiante_id,
       a.vigente_hasta, (a.vigente_hasta < CURRENT_DATE)
FROM autorizacion a
WHERE a.estado IN ('OTORGADA', 'VENCIDA') AND a.revocada_en IS NULL
  AND a.vigente_hasta BETWEEN CURRENT_DATE - 90 AND CURRENT_DATE + 30
  AND (
      a.tipo = 'MEDICACION'
      OR NOT EXISTS (
          SELECT 1
          FROM autorizacion n
          WHERE n.estudiante_id = a.estudiante_id
            AND n.tipo = a.tipo
            AND n.id <> a.id
            AND n.estado = 'OTORGADA'
            AND n.revocada_en IS NULL
            AND (coalesce(n.vigente_desde, n.fecha_firma, DATE '0001-01-01'), n.id)
              > (coalesce(a.vigente_desde, a.fecha_firma, DATE '0001-01-01'), a.id)
      )
  );

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


-- Identity (sin datos ni migraciones históricas de esquema).
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

CREATE TABLE "AspNetRoles" (
        "Id" text NOT NULL,
        "Name" character varying(256),
        "NormalizedName" character varying(256),
        "ConcurrencyStamp" text,
        CONSTRAINT "PK_AspNetRoles" PRIMARY KEY ("Id")
    );

CREATE TABLE "AspNetUsers" (
        "Id" text NOT NULL,
        "UserName" character varying(256),
        "NormalizedUserName" character varying(256),
        "Email" character varying(256),
        "NormalizedEmail" character varying(256),
        "EmailConfirmed" boolean NOT NULL,
        "PasswordHash" text,
        "SecurityStamp" text,
        "ConcurrencyStamp" text,
        "PhoneNumber" text,
        "PhoneNumberConfirmed" boolean NOT NULL,
        "TwoFactorEnabled" boolean NOT NULL,
        "LockoutEnd" timestamp with time zone,
        "LockoutEnabled" boolean NOT NULL,
        "AccessFailedCount" integer NOT NULL,
        CONSTRAINT "PK_AspNetUsers" PRIMARY KEY ("Id")
    );

CREATE TABLE "AspNetRoleClaims" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "RoleId" text NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
    );

CREATE TABLE "AspNetUserClaims" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "UserId" text NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );

CREATE TABLE "AspNetUserLogins" (
        "LoginProvider" text NOT NULL,
        "ProviderKey" text NOT NULL,
        "ProviderDisplayName" text,
        "UserId" text NOT NULL,
        CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
        CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );

CREATE TABLE "AspNetUserRoles" (
        "UserId" text NOT NULL,
        "RoleId" text NOT NULL,
        CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
        CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );

CREATE TABLE "AspNetUserTokens" (
        "UserId" text NOT NULL,
        "LoginProvider" text NOT NULL,
        "Name" text NOT NULL,
        "Value" text,
        CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
        CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );

CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");

CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");

CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");

CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");

CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");

CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");

CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");
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

COMMIT;
