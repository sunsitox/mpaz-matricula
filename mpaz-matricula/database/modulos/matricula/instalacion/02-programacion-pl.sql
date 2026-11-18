-- PARTE 2 / 3: PL (programación y seguridad). PostgreSQL 14+.
-- Ejecutar después de 01-estructura-bd.sql, conectado a la misma base.
-- Incluye funciones, triggers, restricciones que dependen de funciones y privilegios.
-- mpaz_app se crea sin contraseña si no existe: asignarla por un canal seguro antes de usar la app.
BEGIN;
SET LOCAL search_path TO mpaz_matricula, public;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mpaz_app') THEN
        CREATE ROLE mpaz_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE;
    END IF;
END $$;
DO $$
BEGIN
    EXECUTE format('REVOKE CONNECT ON DATABASE %I FROM PUBLIC', current_database());
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO mpaz_app', current_database());
    EXECUTE format('ALTER ROLE mpaz_app IN DATABASE %I SET search_path = mpaz_matricula, public', current_database());
    EXECUTE format('ALTER DATABASE %I SET search_path = mpaz_matricula, public', current_database());
END $$;
CREATE OR REPLACE FUNCTION normalizar_rut(p TEXT) RETURNS TEXT
LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE v TEXT := upper(regexp_replace(coalesce(p, ''), '[\.\s]', '', 'g'));
BEGIN
    IF position('-' IN v) = 0 AND length(v) >= 2 THEN
        v := substr(v, 1, length(v) - 1) || '-' || right(v, 1);
    END IF;
    RETURN v;
END $$;

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

CREATE OR REPLACE FUNCTION fn_actualizar_version() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    NEW.version    := OLD.version + 1;
    NEW.updated_at := now();
    RETURN NEW;
END $$;

CREATE OR REPLACE FUNCTION fn_solo_insercion() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'La tabla % es solo-inserción: % no permitido', TG_TABLE_NAME, TG_OP
        USING ERRCODE = 'integrity_constraint_violation';
END $$;

CREATE OR REPLACE FUNCTION fn_bloquear_delete() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'No se permite borrar filas de %: usar estado o vigencia', TG_TABLE_NAME
        USING ERRCODE = 'integrity_constraint_violation';
END $$;

CREATE OR REPLACE FUNCTION siguiente_folio(p_anio SMALLINT) RETURNS TEXT
LANGUAGE plpgsql AS $$
DECLARE n INTEGER;
BEGIN
    INSERT INTO folio_secuencia (anio, ultimo) VALUES (p_anio, 1)
    ON CONFLICT (anio) DO UPDATE SET ultimo = folio_secuencia.ultimo + 1
    RETURNING ultimo INTO n;
    RETURN 'MP-' || p_anio || '-' || lpad(n::TEXT, 5, '0');
END $$;

CREATE OR REPLACE FUNCTION fn_ficha_asignar_folio() RETURNS TRIGGER
LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.folio IS NULL OR NEW.folio = '' THEN
        NEW.folio := siguiente_folio((SELECT anio FROM matricula_anual WHERE id = NEW.matricula_id));
    END IF;
    RETURN NEW;
END $$;

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

ALTER TABLE estudiante ADD CONSTRAINT ck_estudiante_run CHECK (tipo_identificador <> 'RUN' OR rut_valido(identificador));

ALTER TABLE persona ADD CONSTRAINT ck_persona_run CHECK (tipo_identificador <> 'RUN' OR rut_valido(identificador));

ALTER TABLE retiro_autorizado ADD CONSTRAINT ck_ra_run CHECK (tipo_identificador <> 'RUN' OR rut_valido(identificador));

CREATE TRIGGER trg_ficha_folio   BEFORE INSERT ON ficha_version
    FOR EACH ROW EXECUTE FUNCTION fn_ficha_asignar_folio();

CREATE TRIGGER trg_ficha_proteger BEFORE UPDATE ON ficha_version
    FOR EACH ROW EXECUTE FUNCTION fn_ficha_proteger();

CREATE TRIGGER trg_ficha_no_delete BEFORE DELETE ON ficha_version
    FOR EACH ROW EXECUTE FUNCTION fn_bloquear_delete();

CREATE TRIGGER trg_documento_no_delete BEFORE DELETE ON documento
    FOR EACH ROW EXECUTE FUNCTION fn_bloquear_delete();

CREATE TRIGGER trg_autorizacion_no_delete BEFORE DELETE ON autorizacion
    FOR EACH ROW EXECUTE FUNCTION fn_bloquear_delete();

CREATE TRIGGER trg_prescripcion_no_delete BEFORE DELETE ON prescripcion
    FOR EACH ROW EXECUTE FUNCTION fn_bloquear_delete();

CREATE TRIGGER trg_adm_validar BEFORE INSERT ON administracion_medicamento
    FOR EACH ROW EXECUTE FUNCTION fn_validar_administracion();

CREATE TRIGGER trg_adm_solo_insercion BEFORE UPDATE OR DELETE ON administracion_medicamento
    FOR EACH ROW EXECUTE FUNCTION fn_solo_insercion();

CREATE TRIGGER trg_historial_solo_insercion BEFORE UPDATE OR DELETE ON historial_cambios
    FOR EACH ROW EXECUTE FUNCTION fn_solo_insercion();

CREATE TRIGGER trg_acceso_solo_insercion BEFORE UPDATE OR DELETE ON registros_acceso
    FOR EACH ROW EXECUTE FUNCTION fn_solo_insercion();





CREATE TRIGGER trg_funcionario_rol_auditoria AFTER INSERT OR UPDATE OR DELETE ON funcionario_rol
    FOR EACH ROW EXECUTE FUNCTION fn_auditar_funcionario_rol();

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
REVOKE ALL ON SCHEMA mpaz_matricula FROM PUBLIC;
GRANT USAGE ON SCHEMA mpaz_matricula TO mpaz_app;

-- Datos institucionales. No se concede DELETE de forma general.
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA mpaz_matricula TO mpaz_app;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA mpaz_matricula TO mpaz_app;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA mpaz_matricula TO mpaz_app;

-- La implementación actual reemplaza las situaciones médicas del borrador
-- mediante DELETE + INSERT. Este permiso debe retirarse cuando ese flujo use
-- vigencia o actualización sin borrado.
GRANT DELETE ON TABLE mpaz_matricula.situacion_medica TO mpaz_app;

-- Auditoría y medicación son solo-inserción. Los triggers del DDL también
-- rechazan UPDATE/DELETE, pero se revocan aquí como defensa adicional.
REVOKE UPDATE, DELETE ON TABLE
    mpaz_matricula.historial_cambios,
    mpaz_matricula.registros_acceso,
    mpaz_matricula.administracion_medicamento
FROM mpaz_app;

-- El GRANT general de arriba concede UPDATE sobre todo el esquema: esta
-- comprobación impide que un GRANT posterior o un reordenamiento del script
-- devuelva el permiso sin que nadie lo note.
DO $$
BEGIN
    IF has_table_privilege('mpaz_app', 'mpaz_matricula.historial_cambios', 'UPDATE')
       OR has_table_privilege('mpaz_app', 'mpaz_matricula.registros_acceso', 'UPDATE')
       OR has_table_privilege('mpaz_app', 'mpaz_matricula.administracion_medicamento', 'UPDATE')
       OR has_table_privilege('mpaz_app', 'mpaz_matricula.historial_cambios', 'DELETE')
       OR has_table_privilege('mpaz_app', 'mpaz_matricula.registros_acceso', 'DELETE')
       OR has_table_privilege('mpaz_app', 'mpaz_matricula.administracion_medicamento', 'DELETE') THEN
        RAISE EXCEPTION 'mpaz_app conserva UPDATE/DELETE sobre tablas de auditoría';
    END IF;
END
$$;

-- Identity necesita administrar sesiones, credenciales, bloqueos y tokens,
-- pero sólo en sus propias tablas: el resto del esquema es SELECT/INSERT/UPDATE.
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
    mpaz_matricula."AspNetUsers",
    mpaz_matricula."AspNetRoles",
    mpaz_matricula."AspNetRoleClaims",
    mpaz_matricula."AspNetUserClaims",
    mpaz_matricula."AspNetUserLogins",
    mpaz_matricula."AspNetUserRoles",
    mpaz_matricula."AspNetUserTokens"
TO mpaz_app;

-- Que el rol resuelva los nombres sin calificar de triggers y vistas.
ALTER ROLE mpaz_app SET search_path = mpaz_matricula, public;

-- Las funciones usan tablas sin calificar: fijarles el search_path las hace
-- independientes de la sesión que dispare el trigger (paso 07).
ALTER FUNCTION mpaz_matricula.normalizar_rut(TEXT)
    SET search_path = mpaz_matricula, public;
ALTER FUNCTION mpaz_matricula.rut_valido(TEXT)
    SET search_path = mpaz_matricula, public;
ALTER FUNCTION mpaz_matricula.fn_actualizar_version()
    SET search_path = mpaz_matricula, public;
ALTER FUNCTION mpaz_matricula.fn_solo_insercion()
    SET search_path = mpaz_matricula, public;
ALTER FUNCTION mpaz_matricula.fn_bloquear_delete()
    SET search_path = mpaz_matricula, public;
ALTER FUNCTION mpaz_matricula.siguiente_folio(SMALLINT)
    SET search_path = mpaz_matricula, public;
ALTER FUNCTION mpaz_matricula.fn_ficha_asignar_folio()
    SET search_path = mpaz_matricula, public;
ALTER FUNCTION mpaz_matricula.fn_ficha_proteger()
    SET search_path = mpaz_matricula, public;
ALTER FUNCTION mpaz_matricula.fn_validar_administracion()
    SET search_path = mpaz_matricula, public;
ALTER FUNCTION mpaz_matricula.fn_auditar_cambio()
    SET search_path = mpaz_matricula, public;
ALTER FUNCTION mpaz_matricula.fn_auditar_funcionario_rol()
    SET search_path = mpaz_matricula, public;




COMMIT;

-- =====================================================================
-- 13. MAT-17 · Revisión automática de alertas de vencimiento.
-- Canal in-app (/Alertas), cada 15 minutos, responsable: proceso programado.
-- =====================================================================
CREATE TABLE IF NOT EXISTS mpaz_matricula.alerta_revision (
    id                              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    revisada_en                     TIMESTAMPTZ NOT NULL DEFAULT now(),
    anio                            SMALLINT NOT NULL CHECK (anio BETWEEN 2000 AND 2100),
    vencidas_autorizaciones         INTEGER NOT NULL DEFAULT 0 CHECK (vencidas_autorizaciones >= 0),
    vencidas_prescripciones         INTEGER NOT NULL DEFAULT 0 CHECK (vencidas_prescripciones >= 0),
    por_vencer_autorizaciones       INTEGER NOT NULL DEFAULT 0 CHECK (por_vencer_autorizaciones >= 0),
    por_vencer_prescripciones       INTEGER NOT NULL DEFAULT 0 CHECK (por_vencer_prescripciones >= 0),
    resumen                         TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_alerta_revision_en
    ON mpaz_matricula.alerta_revision (revisada_en DESC);

-- La revisión la escribe el proceso programado con el rol de la aplicación:
-- solo lectura e inserción, nunca UPDATE/DELETE.
GRANT SELECT, INSERT ON TABLE mpaz_matricula.alerta_revision TO mpaz_app;


COMMENT ON TABLE mpaz_matricula.alerta_revision IS
    'Resumen periódico de v_alertas_vencimiento escrito por el proceso programado MAT-17.';
