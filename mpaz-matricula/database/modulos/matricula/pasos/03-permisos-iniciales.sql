-- =====================================================================
-- MPAZ Web · Matrícula 2027 — Matriz inicial de permisos
--
-- Carga una sola vez, después del DDL (01-ddl-institucional.sql).
-- Traduce la sección 10 de los requerimientos a las áreas y acciones del
-- esquema (rol_permiso). Es una propuesta: debe validarse con Dirección
-- antes de operar con datos reales.
--
-- Ejecutar:
--   psql -U <usuario> -d <base> -f 03-permisos-iniciales.sql
-- =====================================================================

BEGIN;

INSERT INTO rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM rol r
JOIN (VALUES
    -- SECRETARIA: crea y mantiene la matrícula y los datos que ella captura.
    ('SECRETARIA', 'GENERAL',       'LEER'),
    ('SECRETARIA', 'GENERAL',       'CREAR'),
    ('SECRETARIA', 'GENERAL',       'EDITAR'),
    ('SECRETARIA', 'FAMILIA',       'LEER'),
    ('SECRETARIA', 'FAMILIA',       'CREAR'),
    ('SECRETARIA', 'FAMILIA',       'EDITAR'),
    ('SECRETARIA', 'RETIRO',        'LEER'),
    ('SECRETARIA', 'RETIRO',        'CREAR'),
    ('SECRETARIA', 'RETIRO',        'EDITAR'),
    ('SECRETARIA', 'AUTORIZACIONES','LEER'),
    ('SECRETARIA', 'AUTORIZACIONES','CREAR'),
    ('SECRETARIA', 'AUTORIZACIONES','EDITAR'),
    ('SECRETARIA', 'DOCUMENTOS',    'LEER'),
    ('SECRETARIA', 'DOCUMENTOS',    'CREAR'),
    ('SECRETARIA', 'DOCUMENTOS',    'EDITAR'),
    ('SECRETARIA', 'GENERAL',       'EXPORTAR'),
    ('SECRETARIA', 'DOCUMENTOS',    'EXPORTAR'),
    ('SECRETARIA', 'SALUD',         'LEER'),
    ('SECRETARIA', 'SALUD',         'CREAR'),
    ('SECRETARIA', 'SALUD',         'EDITAR'),
    ('SECRETARIA', 'JUDICIAL',      'LEER'),
    ('SECRETARIA', 'JUDICIAL',      'CREAR'),
    ('SECRETARIA', 'JUDICIAL',      'EDITAR'),

    -- DIRECCION: ve todo + valida institucional, sin crear/editar (Johan 17-11-2026).
    ('DIRECCION', 'GENERAL',        'LEER'),
    ('DIRECCION', 'GENERAL',        'VALIDAR'),
    ('DIRECCION', 'FAMILIA',        'LEER'),
    ('DIRECCION', 'RETIRO',         'LEER'),
    ('DIRECCION', 'SALUD',          'LEER'),
    ('DIRECCION', 'PIE',            'LEER'),
    ('DIRECCION', 'JUDICIAL',       'LEER'),
    ('DIRECCION', 'AUTORIZACIONES', 'LEER'),
    ('DIRECCION', 'AUTORIZACIONES', 'VALIDAR'),
    ('DIRECCION', 'DOCUMENTOS',     'LEER'),
    ('DIRECCION', 'DOCUMENTOS',     'VALIDAR'),
    ('DIRECCION', 'MEDICACION',     'LEER'),
    ('DIRECCION', 'GENERAL',        'EXPORTAR'),
    ('DIRECCION', 'DOCUMENTOS',     'EXPORTAR'),

    -- INSPECTORIA: ve todo + edita datos del estudiante, sin crear ni validar (Johan 17-11-2026).
    ('INSPECTORIA', 'GENERAL',        'LEER'),
    ('INSPECTORIA', 'GENERAL',        'EDITAR'),
    ('INSPECTORIA', 'FAMILIA',        'LEER'),
    ('INSPECTORIA', 'FAMILIA',        'EDITAR'),
    ('INSPECTORIA', 'RETIRO',         'LEER'),
    ('INSPECTORIA', 'RETIRO',         'EDITAR'),
    ('INSPECTORIA', 'SALUD',          'LEER'),
    ('INSPECTORIA', 'PIE',            'LEER'),
    ('INSPECTORIA', 'JUDICIAL',       'LEER'),
    ('INSPECTORIA', 'AUTORIZACIONES', 'LEER'),
    ('INSPECTORIA', 'DOCUMENTOS',     'LEER'),
    ('INSPECTORIA', 'MEDICACION',     'LEER'),

    -- EQUIPO_PIE: solo ver (Johan: no genera ni edita).
    ('EQUIPO_PIE', 'GENERAL',       'LEER'),
    ('EQUIPO_PIE', 'FAMILIA',       'LEER'),
    ('EQUIPO_PIE', 'PIE',           'LEER'),
    ('EQUIPO_PIE', 'DOCUMENTOS',    'LEER'),

    -- TENS: contactos, salud pertinente y medicación.
    -- GENERAL:LEER sólo abre el listado de alumnos (nombre, curso, estado); los
    -- bloques sensibles exigen su propia área, así que no amplía lo que ve.
    ('TENS', 'GENERAL',             'LEER'),
    ('TENS', 'FAMILIA',             'LEER'),
    ('TENS', 'SALUD',               'LEER'),
    ('TENS', 'MEDICACION',          'LEER'),
    ('TENS', 'MEDICACION',          'CREAR'),
    ('TENS', 'DOCUMENTOS',          'LEER'),

    -- DOCENTE: sólo datos mínimos. GENERAL:LEER abre la consulta con identificación,
    -- curso y estado; sin FAMILIA/SALUD/RETIRO/PIE el servidor no le entrega el
    -- resto del expediente (S-02).
    ('DOCENTE', 'GENERAL',          'LEER'),

    -- UTP: ver todo, solo lectura + exportar (Johan 17-11-2026).
    ('UTP', 'GENERAL',        'LEER'),
    ('UTP', 'FAMILIA',        'LEER'),
    ('UTP', 'RETIRO',         'LEER'),
    ('UTP', 'SALUD',          'LEER'),
    ('UTP', 'PIE',            'LEER'),
    ('UTP', 'JUDICIAL',       'LEER'),
    ('UTP', 'AUTORIZACIONES', 'LEER'),
    ('UTP', 'DOCUMENTOS',     'LEER'),
    ('UTP', 'MEDICACION',     'LEER'),
    ('UTP', 'GENERAL',        'EXPORTAR'),
    ('UTP', 'DOCUMENTOS',     'EXPORTAR'),

    -- CONVIVENCIA: solo lo necesario (sin salud/PIE/judicial/medicación).
    ('CONVIVENCIA', 'GENERAL',        'LEER'),
    ('CONVIVENCIA', 'FAMILIA',        'LEER'),
    ('CONVIVENCIA', 'RETIRO',         'LEER'),
    ('CONVIVENCIA', 'AUTORIZACIONES', 'LEER'),
    ('CONVIVENCIA', 'DOCUMENTOS',     'LEER'),

    -- ADMIN_TI: cuentas y auditoría, sin lectura clínica ni judicial.
    ('ADMIN_TI', 'ADMIN',           'ADMINISTRAR'),
    ('ADMIN_TI', 'ADMIN',           'LEER'),
    ('ADMIN_TI', 'AUDITORIA',       'LEER')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;

COMMIT;

-- =====================================================================
-- PENDIENTE: primera cuenta ADMIN_TI
--
-- No hay registro abierto y la contraseña se guarda con hash de ASP.NET Core
-- Identity, así que no conviene sembrarla desde SQL. Se crea una sola vez
-- desde la aplicación (o con un comando puntual que use PasswordHasher) y se
-- enlaza con:
--
--   UPDATE funcionario SET sso_sujeto = '<id del usuario de Identity>'
--   WHERE correo = '<correo del administrador>';
--
-- Falta definir el nombre y correo del primer administrador.
-- =====================================================================
