-- =====================================================================
-- 10. DDL del módulo de asistencia (esquema mpaz_asistencia).
--
-- Fuente: base mpaz_asistencia del proyecto Neon, creada por el equipo de
-- asistencia. Se consolida aquí como semilla del módulo para que cada
-- módulo viva en su carpeta con su esquema dentro de la misma base.
-- Se eliminaron los DEFAULT PRIVILEGES internos de Neon (roles
-- cloud_admin/neon_superuser), que no aplican fuera de su plataforma.
-- =====================================================================

--
-- PostgreSQL database dump
--

\restrict dIHYonMXrwIQOQc91s7mGLvTJrQWPPRRYZavsW7zaleDkzoAQiLEvm3EbNyMGSk

-- Dumped from database version 18.6 (ac70818)
-- Dumped by pg_dump version 18.6

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: mpaz_asistencia; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA mpaz_asistencia;


--
-- Name: btree_gist; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS btree_gist WITH SCHEMA public;


--
-- Name: EXTENSION btree_gist; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION btree_gist IS 'support for indexing common datatypes in GiST';


--
-- Name: estado_activo; Type: TYPE; Schema: mpaz_asistencia; Owner: -
--

CREATE TYPE mpaz_asistencia.estado_activo AS ENUM (
    'activo',
    'inactivo'
);


--
-- Name: estado_alumno; Type: TYPE; Schema: mpaz_asistencia; Owner: -
--

CREATE TYPE mpaz_asistencia.estado_alumno AS ENUM (
    'activo',
    'inactivo',
    'retirado'
);


--
-- Name: estado_asistencia_clase; Type: TYPE; Schema: mpaz_asistencia; Owner: -
--

CREATE TYPE mpaz_asistencia.estado_asistencia_clase AS ENUM (
    'presente',
    'atrasado',
    'justificado',
    'no_presente'
);


--
-- Name: estado_sesion; Type: TYPE; Schema: mpaz_asistencia; Owner: -
--

CREATE TYPE mpaz_asistencia.estado_sesion AS ENUM (
    'borrador',
    'confirmada',
    'anulada'
);


--
-- Name: estado_tarjeta; Type: TYPE; Schema: mpaz_asistencia; Owner: -
--

CREATE TYPE mpaz_asistencia.estado_tarjeta AS ENUM (
    'disponible',
    'asignada',
    'bloqueada',
    'extraviada'
);


--
-- Name: evento_tarjeta; Type: TYPE; Schema: mpaz_asistencia; Owner: -
--

CREATE TYPE mpaz_asistencia.evento_tarjeta AS ENUM (
    'registrada',
    'asignada',
    'liberada',
    'bloqueada',
    'desbloqueada',
    'extraviada'
);


--
-- Name: modalidad_sesion; Type: TYPE; Schema: mpaz_asistencia; Owner: -
--

CREATE TYPE mpaz_asistencia.modalidad_sesion AS ENUM (
    'por_clase',
    'diaria'
);


--
-- Name: origen_asistencia; Type: TYPE; Schema: mpaz_asistencia; Owner: -
--

CREATE TYPE mpaz_asistencia.origen_asistencia AS ENUM (
    'manual',
    'rfid'
);


--
-- Name: rol_usuario; Type: TYPE; Schema: mpaz_asistencia; Owner: -
--

CREATE TYPE mpaz_asistencia.rol_usuario AS ENUM (
    'administrador',
    'profesor'
);


--
-- Name: ck_alumno_en_curso(); Type: FUNCTION; Schema: mpaz_asistencia; Owner: -
--

CREATE FUNCTION mpaz_asistencia.ck_alumno_en_curso() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE c integer;
BEGIN
  SELECT curso_id INTO c FROM alumnos WHERE id = NEW.alumno_id;
  IF c IS DISTINCT FROM NEW.curso_id THEN
    RAISE EXCEPTION 'El alumno % no pertenece al curso % de la sesión', NEW.alumno_id, NEW.curso_id
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: alumnos; Type: TABLE; Schema: mpaz_asistencia; Owner: -
--

CREATE TABLE mpaz_asistencia.alumnos (
    id integer NOT NULL,
    rut text NOT NULL,
    nombre_completo text NOT NULL,
    curso_id integer NOT NULL,
    estado mpaz_asistencia.estado_alumno DEFAULT 'activo'::mpaz_asistencia.estado_alumno NOT NULL,
    fecha_baja date,
    CONSTRAINT ck_alumno_baja CHECK (((estado = 'retirado'::mpaz_asistencia.estado_alumno) = (fecha_baja IS NOT NULL))),
    CONSTRAINT ck_alumno_rut CHECK ((rut ~ '^[0-9]{1,8}-[0-9Kk]$'::text))
);


--
-- Name: alumnos_id_seq; Type: SEQUENCE; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE mpaz_asistencia.alumnos ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME mpaz_asistencia.alumnos_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: asignaciones_docentes; Type: TABLE; Schema: mpaz_asistencia; Owner: -
--

CREATE TABLE mpaz_asistencia.asignaciones_docentes (
    id integer NOT NULL,
    profesor_id integer NOT NULL,
    curso_id integer NOT NULL,
    asignatura_id integer NOT NULL,
    estado mpaz_asistencia.estado_activo DEFAULT 'activo'::mpaz_asistencia.estado_activo NOT NULL
);


--
-- Name: asignaciones_docentes_id_seq; Type: SEQUENCE; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE mpaz_asistencia.asignaciones_docentes ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME mpaz_asistencia.asignaciones_docentes_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: asignaturas; Type: TABLE; Schema: mpaz_asistencia; Owner: -
--

CREATE TABLE mpaz_asistencia.asignaturas (
    id integer NOT NULL,
    nombre text NOT NULL,
    estado mpaz_asistencia.estado_activo DEFAULT 'activo'::mpaz_asistencia.estado_activo NOT NULL
);


--
-- Name: asignaturas_id_seq; Type: SEQUENCE; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE mpaz_asistencia.asignaturas ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME mpaz_asistencia.asignaturas_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: asistencias; Type: TABLE; Schema: mpaz_asistencia; Owner: -
--

CREATE TABLE mpaz_asistencia.asistencias (
    id integer NOT NULL,
    alumno_id integer NOT NULL,
    tarjeta_id integer,
    fecha date GENERATED ALWAYS AS (((COALESCE(hora_entrada, hora_salida) AT TIME ZONE 'America/Santiago'::text))::date) STORED,
    hora_entrada timestamp with time zone,
    hora_salida timestamp with time zone,
    tipo_salida text,
    totem_entrada_id integer,
    totem_salida_id integer,
    evento_entrada_id text,
    evento_salida_id text,
    CONSTRAINT ck_asistencia_marcaje CHECK (((hora_entrada IS NOT NULL) OR (hora_salida IS NOT NULL))),
    CONSTRAINT ck_asistencia_orden CHECK (((hora_entrada IS NULL) OR (hora_salida IS NULL) OR (hora_salida >= hora_entrada))),
    CONSTRAINT ck_asistencia_tipo_sal CHECK (((tipo_salida IS NULL) OR (hora_salida IS NOT NULL))),
    CONSTRAINT ck_asistencia_totem_ent CHECK (((hora_entrada IS NULL) OR (totem_entrada_id IS NOT NULL))),
    CONSTRAINT ck_asistencia_totem_sal CHECK (((hora_salida IS NULL) OR (totem_salida_id IS NOT NULL)))
);


--
-- Name: TABLE asistencias; Type: COMMENT; Schema: mpaz_asistencia; Owner: -
--

COMMENT ON TABLE mpaz_asistencia.asistencias IS 'Jornada RFID: una fila por alumno y día. fecha = día operativo en America/Santiago (generada). Independiente de asistencias_clase a propósito; el único puente es alumnos.';


--
-- Name: asistencias_clase; Type: TABLE; Schema: mpaz_asistencia; Owner: -
--

CREATE TABLE mpaz_asistencia.asistencias_clase (
    id integer NOT NULL,
    sesion_clase_id integer NOT NULL,
    curso_id integer NOT NULL,
    alumno_id integer NOT NULL,
    estado mpaz_asistencia.estado_asistencia_clase NOT NULL,
    origen mpaz_asistencia.origen_asistencia DEFAULT 'manual'::mpaz_asistencia.origen_asistencia NOT NULL,
    observacion text,
    fecha_registro timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE asistencias_clase; Type: COMMENT; Schema: mpaz_asistencia; Owner: -
--

COMMENT ON TABLE mpaz_asistencia.asistencias_clase IS 'Asistencia por clase. curso_id es snapshot histórico: el trigger valida contra el curso actual solo al insertar.';


--
-- Name: asistencias_clase_id_seq; Type: SEQUENCE; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE mpaz_asistencia.asistencias_clase ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME mpaz_asistencia.asistencias_clase_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: asistencias_id_seq; Type: SEQUENCE; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE mpaz_asistencia.asistencias ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME mpaz_asistencia.asistencias_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: cursos; Type: TABLE; Schema: mpaz_asistencia; Owner: -
--

CREATE TABLE mpaz_asistencia.cursos (
    id integer NOT NULL,
    nombre text NOT NULL,
    estado mpaz_asistencia.estado_activo DEFAULT 'activo'::mpaz_asistencia.estado_activo NOT NULL
);


--
-- Name: cursos_id_seq; Type: SEQUENCE; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE mpaz_asistencia.cursos ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME mpaz_asistencia.cursos_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: sesiones_clase; Type: TABLE; Schema: mpaz_asistencia; Owner: -
--

CREATE TABLE mpaz_asistencia.sesiones_clase (
    id integer NOT NULL,
    asignacion_id integer NOT NULL,
    curso_id integer NOT NULL,
    fecha date NOT NULL,
    hora_inicio time without time zone NOT NULL,
    hora_fin time without time zone,
    modalidad mpaz_asistencia.modalidad_sesion NOT NULL,
    estado mpaz_asistencia.estado_sesion DEFAULT 'borrador'::mpaz_asistencia.estado_sesion NOT NULL,
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL,
    fecha_confirmacion timestamp with time zone,
    CONSTRAINT ck_sesion_confirmada CHECK (((estado <> 'confirmada'::mpaz_asistencia.estado_sesion) OR (fecha_confirmacion IS NOT NULL))),
    CONSTRAINT ck_sesion_horas CHECK (((hora_fin IS NULL) OR (hora_fin > hora_inicio)))
);


--
-- Name: sesiones_clase_id_seq; Type: SEQUENCE; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE mpaz_asistencia.sesiones_clase ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME mpaz_asistencia.sesiones_clase_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tarjeta_historial; Type: TABLE; Schema: mpaz_asistencia; Owner: -
--

CREATE TABLE mpaz_asistencia.tarjeta_historial (
    id integer NOT NULL,
    tarjeta_id integer NOT NULL,
    alumno_id integer,
    usuario_id integer,
    evento mpaz_asistencia.evento_tarjeta NOT NULL,
    motivo text,
    fecha timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE tarjeta_historial; Type: COMMENT; Schema: mpaz_asistencia; Owner: -
--

COMMENT ON TABLE mpaz_asistencia.tarjeta_historial IS 'Bitácora de eventos de cada tarjeta. alumno_id nunca se nulifica (RESTRICT) para no perder la auditoría.';


--
-- Name: tarjeta_historial_id_seq; Type: SEQUENCE; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE mpaz_asistencia.tarjeta_historial ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME mpaz_asistencia.tarjeta_historial_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tarjetas; Type: TABLE; Schema: mpaz_asistencia; Owner: -
--

CREATE TABLE mpaz_asistencia.tarjetas (
    id integer NOT NULL,
    alumno_id integer,
    uid text NOT NULL,
    estado mpaz_asistencia.estado_tarjeta DEFAULT 'disponible'::mpaz_asistencia.estado_tarjeta NOT NULL,
    fecha_registro date DEFAULT CURRENT_DATE NOT NULL,
    CONSTRAINT ck_tarjeta_asignada_con_alumno CHECK (((estado <> 'asignada'::mpaz_asistencia.estado_tarjeta) OR (alumno_id IS NOT NULL))),
    CONSTRAINT ck_tarjeta_disponible_sin_alumno CHECK (((estado <> 'disponible'::mpaz_asistencia.estado_tarjeta) OR (alumno_id IS NULL))),
    CONSTRAINT ck_tarjeta_uid CHECK ((uid ~ '^[0-9A-F]{2,}(:[0-9A-F]{2,})*$'::text))
);


--
-- Name: tarjetas_id_seq; Type: SEQUENCE; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE mpaz_asistencia.tarjetas ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME mpaz_asistencia.tarjetas_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: totems; Type: TABLE; Schema: mpaz_asistencia; Owner: -
--

CREATE TABLE mpaz_asistencia.totems (
    id integer NOT NULL,
    codigo text NOT NULL,
    nombre text NOT NULL,
    ubicacion text,
    estado mpaz_asistencia.estado_activo DEFAULT 'activo'::mpaz_asistencia.estado_activo NOT NULL,
    fecha_registro date DEFAULT CURRENT_DATE NOT NULL,
    ultima_conexion timestamp with time zone
);


--
-- Name: totems_id_seq; Type: SEQUENCE; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE mpaz_asistencia.totems ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME mpaz_asistencia.totems_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: usuarios; Type: TABLE; Schema: mpaz_asistencia; Owner: -
--

CREATE TABLE mpaz_asistencia.usuarios (
    id integer NOT NULL,
    nombre_completo text NOT NULL,
    usuario text NOT NULL,
    password_hash text NOT NULL,
    rol mpaz_asistencia.rol_usuario NOT NULL,
    estado mpaz_asistencia.estado_activo DEFAULT 'activo'::mpaz_asistencia.estado_activo NOT NULL,
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: usuarios_id_seq; Type: SEQUENCE; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE mpaz_asistencia.usuarios ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME mpaz_asistencia.usuarios_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: alumnos alumnos_pkey; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.alumnos
    ADD CONSTRAINT alumnos_pkey PRIMARY KEY (id);


--
-- Name: alumnos alumnos_rut_key; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.alumnos
    ADD CONSTRAINT alumnos_rut_key UNIQUE (rut);


--
-- Name: asignaciones_docentes asignaciones_docentes_pkey; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asignaciones_docentes
    ADD CONSTRAINT asignaciones_docentes_pkey PRIMARY KEY (id);


--
-- Name: asignaturas asignaturas_nombre_key; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asignaturas
    ADD CONSTRAINT asignaturas_nombre_key UNIQUE (nombre);


--
-- Name: asignaturas asignaturas_pkey; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asignaturas
    ADD CONSTRAINT asignaturas_pkey PRIMARY KEY (id);


--
-- Name: asistencias_clase asistencias_clase_pkey; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asistencias_clase
    ADD CONSTRAINT asistencias_clase_pkey PRIMARY KEY (id);


--
-- Name: asistencias asistencias_pkey; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asistencias
    ADD CONSTRAINT asistencias_pkey PRIMARY KEY (id);


--
-- Name: cursos cursos_nombre_key; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.cursos
    ADD CONSTRAINT cursos_nombre_key UNIQUE (nombre);


--
-- Name: cursos cursos_pkey; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.cursos
    ADD CONSTRAINT cursos_pkey PRIMARY KEY (id);


--
-- Name: sesiones_clase ex_sesion_solape_curso; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.sesiones_clase
    ADD CONSTRAINT ex_sesion_solape_curso EXCLUDE USING gist (curso_id WITH =, tsrange((fecha + hora_inicio), (fecha + COALESCE(hora_fin, (hora_inicio + '00:45:00'::interval)))) WITH &&) WHERE ((estado <> 'anulada'::mpaz_asistencia.estado_sesion));


--
-- Name: sesiones_clase sesiones_clase_pkey; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.sesiones_clase
    ADD CONSTRAINT sesiones_clase_pkey PRIMARY KEY (id);


--
-- Name: tarjeta_historial tarjeta_historial_pkey; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.tarjeta_historial
    ADD CONSTRAINT tarjeta_historial_pkey PRIMARY KEY (id);


--
-- Name: tarjetas tarjetas_pkey; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.tarjetas
    ADD CONSTRAINT tarjetas_pkey PRIMARY KEY (id);


--
-- Name: tarjetas tarjetas_uid_key; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.tarjetas
    ADD CONSTRAINT tarjetas_uid_key UNIQUE (uid);


--
-- Name: totems totems_codigo_key; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.totems
    ADD CONSTRAINT totems_codigo_key UNIQUE (codigo);


--
-- Name: totems totems_pkey; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.totems
    ADD CONSTRAINT totems_pkey PRIMARY KEY (id);


--
-- Name: asignaciones_docentes uq_asignacion_docente; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asignaciones_docentes
    ADD CONSTRAINT uq_asignacion_docente UNIQUE (profesor_id, curso_id, asignatura_id);


--
-- Name: asignaciones_docentes uq_asignacion_id_curso; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asignaciones_docentes
    ADD CONSTRAINT uq_asignacion_id_curso UNIQUE (id, curso_id);


--
-- Name: asistencias uq_asistencia_alumno_dia; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asistencias
    ADD CONSTRAINT uq_asistencia_alumno_dia UNIQUE (alumno_id, fecha);


--
-- Name: asistencias_clase uq_asistencia_clase; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asistencias_clase
    ADD CONSTRAINT uq_asistencia_clase UNIQUE (sesion_clase_id, alumno_id);


--
-- Name: sesiones_clase uq_sesion_clase; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.sesiones_clase
    ADD CONSTRAINT uq_sesion_clase UNIQUE (asignacion_id, fecha, hora_inicio);


--
-- Name: sesiones_clase uq_sesion_id_curso; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.sesiones_clase
    ADD CONSTRAINT uq_sesion_id_curso UNIQUE (id, curso_id);


--
-- Name: usuarios usuarios_pkey; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.usuarios
    ADD CONSTRAINT usuarios_pkey PRIMARY KEY (id);


--
-- Name: usuarios usuarios_usuario_key; Type: CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.usuarios
    ADD CONSTRAINT usuarios_usuario_key UNIQUE (usuario);


--
-- Name: ix_alumnos_curso; Type: INDEX; Schema: mpaz_asistencia; Owner: -
--

CREATE INDEX ix_alumnos_curso ON mpaz_asistencia.alumnos USING btree (curso_id);


--
-- Name: ix_asignaciones_curso; Type: INDEX; Schema: mpaz_asistencia; Owner: -
--

CREATE INDEX ix_asignaciones_curso ON mpaz_asistencia.asignaciones_docentes USING btree (curso_id);


--
-- Name: ix_asistencias_clase_alumno; Type: INDEX; Schema: mpaz_asistencia; Owner: -
--

CREATE INDEX ix_asistencias_clase_alumno ON mpaz_asistencia.asistencias_clase USING btree (alumno_id);


--
-- Name: ix_asistencias_fecha; Type: INDEX; Schema: mpaz_asistencia; Owner: -
--

CREATE INDEX ix_asistencias_fecha ON mpaz_asistencia.asistencias USING btree (fecha);


--
-- Name: ix_sesiones_fecha; Type: INDEX; Schema: mpaz_asistencia; Owner: -
--

CREATE INDEX ix_sesiones_fecha ON mpaz_asistencia.sesiones_clase USING btree (fecha);


--
-- Name: ix_tarjeta_historial_tarjeta_fecha; Type: INDEX; Schema: mpaz_asistencia; Owner: -
--

CREATE INDEX ix_tarjeta_historial_tarjeta_fecha ON mpaz_asistencia.tarjeta_historial USING btree (tarjeta_id, fecha);


--
-- Name: ix_tarjetas_alumno; Type: INDEX; Schema: mpaz_asistencia; Owner: -
--

CREATE INDEX ix_tarjetas_alumno ON mpaz_asistencia.tarjetas USING btree (alumno_id);


--
-- Name: uq_asistencia_evento_entrada; Type: INDEX; Schema: mpaz_asistencia; Owner: -
--

CREATE UNIQUE INDEX uq_asistencia_evento_entrada ON mpaz_asistencia.asistencias USING btree (totem_entrada_id, evento_entrada_id) WHERE (evento_entrada_id IS NOT NULL);


--
-- Name: uq_asistencia_evento_salida; Type: INDEX; Schema: mpaz_asistencia; Owner: -
--

CREATE UNIQUE INDEX uq_asistencia_evento_salida ON mpaz_asistencia.asistencias USING btree (totem_salida_id, evento_salida_id) WHERE (evento_salida_id IS NOT NULL);


--
-- Name: uq_tarjeta_asignada_por_alumno; Type: INDEX; Schema: mpaz_asistencia; Owner: -
--

CREATE UNIQUE INDEX uq_tarjeta_asignada_por_alumno ON mpaz_asistencia.tarjetas USING btree (alumno_id) WHERE ((estado = 'asignada'::mpaz_asistencia.estado_tarjeta) AND (alumno_id IS NOT NULL));


--
-- Name: uq_usuarios_usuario_ci; Type: INDEX; Schema: mpaz_asistencia; Owner: -
--

CREATE UNIQUE INDEX uq_usuarios_usuario_ci ON mpaz_asistencia.usuarios USING btree (lower(usuario));


--
-- Name: asistencias_clase trg_asistclase_curso; Type: TRIGGER; Schema: mpaz_asistencia; Owner: -
--

CREATE TRIGGER trg_asistclase_curso BEFORE INSERT OR UPDATE OF alumno_id, curso_id ON mpaz_asistencia.asistencias_clase FOR EACH ROW EXECUTE FUNCTION mpaz_asistencia.ck_alumno_en_curso();


--
-- Name: alumnos alumnos_curso_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.alumnos
    ADD CONSTRAINT alumnos_curso_id_fkey FOREIGN KEY (curso_id) REFERENCES mpaz_asistencia.cursos(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: asignaciones_docentes asignaciones_docentes_asignatura_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asignaciones_docentes
    ADD CONSTRAINT asignaciones_docentes_asignatura_id_fkey FOREIGN KEY (asignatura_id) REFERENCES mpaz_asistencia.asignaturas(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: asignaciones_docentes asignaciones_docentes_curso_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asignaciones_docentes
    ADD CONSTRAINT asignaciones_docentes_curso_id_fkey FOREIGN KEY (curso_id) REFERENCES mpaz_asistencia.cursos(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: asignaciones_docentes asignaciones_docentes_profesor_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asignaciones_docentes
    ADD CONSTRAINT asignaciones_docentes_profesor_id_fkey FOREIGN KEY (profesor_id) REFERENCES mpaz_asistencia.usuarios(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: asistencias asistencias_alumno_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asistencias
    ADD CONSTRAINT asistencias_alumno_id_fkey FOREIGN KEY (alumno_id) REFERENCES mpaz_asistencia.alumnos(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: asistencias_clase asistencias_clase_alumno_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asistencias_clase
    ADD CONSTRAINT asistencias_clase_alumno_id_fkey FOREIGN KEY (alumno_id) REFERENCES mpaz_asistencia.alumnos(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: asistencias asistencias_tarjeta_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asistencias
    ADD CONSTRAINT asistencias_tarjeta_id_fkey FOREIGN KEY (tarjeta_id) REFERENCES mpaz_asistencia.tarjetas(id) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- Name: asistencias asistencias_totem_entrada_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asistencias
    ADD CONSTRAINT asistencias_totem_entrada_id_fkey FOREIGN KEY (totem_entrada_id) REFERENCES mpaz_asistencia.totems(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: asistencias asistencias_totem_salida_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asistencias
    ADD CONSTRAINT asistencias_totem_salida_id_fkey FOREIGN KEY (totem_salida_id) REFERENCES mpaz_asistencia.totems(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: asistencias_clase fk_asistclase_sesion_curso; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.asistencias_clase
    ADD CONSTRAINT fk_asistclase_sesion_curso FOREIGN KEY (sesion_clase_id, curso_id) REFERENCES mpaz_asistencia.sesiones_clase(id, curso_id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: sesiones_clase fk_sesion_asignacion_curso; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.sesiones_clase
    ADD CONSTRAINT fk_sesion_asignacion_curso FOREIGN KEY (asignacion_id, curso_id) REFERENCES mpaz_asistencia.asignaciones_docentes(id, curso_id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: tarjeta_historial tarjeta_historial_alumno_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.tarjeta_historial
    ADD CONSTRAINT tarjeta_historial_alumno_id_fkey FOREIGN KEY (alumno_id) REFERENCES mpaz_asistencia.alumnos(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: tarjeta_historial tarjeta_historial_tarjeta_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.tarjeta_historial
    ADD CONSTRAINT tarjeta_historial_tarjeta_id_fkey FOREIGN KEY (tarjeta_id) REFERENCES mpaz_asistencia.tarjetas(id) ON UPDATE CASCADE ON DELETE RESTRICT;


--
-- Name: tarjeta_historial tarjeta_historial_usuario_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.tarjeta_historial
    ADD CONSTRAINT tarjeta_historial_usuario_id_fkey FOREIGN KEY (usuario_id) REFERENCES mpaz_asistencia.usuarios(id) ON UPDATE CASCADE ON DELETE SET NULL;


--
-- Name: tarjetas tarjetas_alumno_id_fkey; Type: FK CONSTRAINT; Schema: mpaz_asistencia; Owner: -
--

ALTER TABLE ONLY mpaz_asistencia.tarjetas
    ADD CONSTRAINT tarjetas_alumno_id_fkey FOREIGN KEY (alumno_id) REFERENCES mpaz_asistencia.alumnos(id) ON UPDATE CASCADE ON DELETE SET NULL;

