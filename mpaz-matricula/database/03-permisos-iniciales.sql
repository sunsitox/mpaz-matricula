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

    -- DIRECCION: consulta institucional y valida lo formalizado.
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
    ('DIRECCION', 'GENERAL',        'EXPORTAR'),
    ('DIRECCION', 'DOCUMENTOS',     'EXPORTAR'),

    -- INSPECTORIA: contacto y retiro, sin diagnósticos.
    ('INSPECTORIA', 'GENERAL',      'LEER'),
    ('INSPECTORIA', 'FAMILIA',      'LEER'),
    ('INSPECTORIA', 'RETIRO',       'LEER'),
    ('INSPECTORIA', 'RETIRO',       'EDITAR'),

    -- EQUIPO_PIE: datos mínimos y apoyos.
    ('EQUIPO_PIE', 'GENERAL',       'LEER'),
    ('EQUIPO_PIE', 'FAMILIA',       'LEER'),
    ('EQUIPO_PIE', 'PIE',           'LEER'),
    ('EQUIPO_PIE', 'PIE',           'EDITAR'),
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
