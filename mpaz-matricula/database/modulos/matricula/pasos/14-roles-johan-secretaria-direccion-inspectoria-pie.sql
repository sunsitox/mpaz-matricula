-- =====================================================================
-- MPAZ Web · Matrícula 2027 — Ajuste roles Johan 18-11-2026
-- SECRETARIA / DIRECCION / INSPECTORIA / EQUIPO_PIE completamente
-- funcionando. TENS / DOCENTE / ADMIN_TI sin cambios.
--
-- Regla aplicada:
-- - SECRETARIA: crea, edita, formaliza (CREAR/EDITAR), sin VALIDAR.
-- - DIRECCION: ve todo (LEER todas las áreas) + VALIDAR + EXPORTAR,
--   sin CREAR/EDITAR (no genera ni edita matrículas).
-- - INSPECTORIA: ve todo (LEER todas las áreas) + edita datos del
--   estudiante (GENERAL:EDITAR, FAMILIA:EDITAR, RETIRO:EDITAR),
--   sin CREAR (no genera matrículas) y sin VALIDAR.
-- - EQUIPO_PIE: solo ver (LEER GENERAL/FAMILIA/PIE/DOCUMENTOS),
--   sin CREAR/EDITAR/VALIDAR (Johan 17-11-2026).
--
-- Re-ejecutable e idempotente. No borra TENS/DOCENTE/ADMIN_TI.
-- Ejecutar:
--   psql -U <usuario> -d <base> -f 14-roles-johan-secretaria-direccion-inspectoria-pie.sql
-- =====================================================================

BEGIN;

-- SECRETARIA: matriz completa crear/editar + exportar, sin validar.
INSERT INTO rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM rol r
JOIN (VALUES
    ('SECRETARIA', 'GENERAL',        'LEER'),
    ('SECRETARIA', 'GENERAL',        'CREAR'),
    ('SECRETARIA', 'GENERAL',        'EDITAR'),
    ('SECRETARIA', 'FAMILIA',        'LEER'),
    ('SECRETARIA', 'FAMILIA',        'CREAR'),
    ('SECRETARIA', 'FAMILIA',        'EDITAR'),
    ('SECRETARIA', 'RETIRO',         'LEER'),
    ('SECRETARIA', 'RETIRO',         'CREAR'),
    ('SECRETARIA', 'RETIRO',         'EDITAR'),
    ('SECRETARIA', 'AUTORIZACIONES', 'LEER'),
    ('SECRETARIA', 'AUTORIZACIONES', 'CREAR'),
    ('SECRETARIA', 'AUTORIZACIONES', 'EDITAR'),
    ('SECRETARIA', 'DOCUMENTOS',     'LEER'),
    ('SECRETARIA', 'DOCUMENTOS',     'CREAR'),
    ('SECRETARIA', 'DOCUMENTOS',     'EDITAR'),
    ('SECRETARIA', 'GENERAL',        'EXPORTAR'),
    ('SECRETARIA', 'DOCUMENTOS',     'EXPORTAR'),
    ('SECRETARIA', 'SALUD',          'LEER'),
    ('SECRETARIA', 'SALUD',          'CREAR'),
    ('SECRETARIA', 'SALUD',          'EDITAR'),
    ('SECRETARIA', 'JUDICIAL',       'LEER'),
    ('SECRETARIA', 'JUDICIAL',       'CREAR'),
    ('SECRETARIA', 'JUDICIAL',       'EDITAR')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;

-- DIRECCION: ver todo + validar + exportar, sin crear/editar.
INSERT INTO rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM rol r
JOIN (VALUES
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
    ('DIRECCION', 'DOCUMENTOS',     'EXPORTAR')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;

-- INSPECTORIA: ver todo + editar datos del estudiante, sin crear ni validar.
INSERT INTO rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM rol r
JOIN (VALUES
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
    ('INSPECTORIA', 'MEDICACION',     'LEER')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;

-- EQUIPO_PIE: solo ver (Johan: no genera ni edita).
INSERT INTO rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM rol r
JOIN (VALUES
    ('EQUIPO_PIE', 'GENERAL',    'LEER'),
    ('EQUIPO_PIE', 'FAMILIA',    'LEER'),
    ('EQUIPO_PIE', 'PIE',        'LEER'),
    ('EQUIPO_PIE', 'DOCUMENTOS', 'LEER')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;

-- Quitar edición a EQUIPO_PIE: solo lectura.
DELETE FROM rol_permiso
USING rol
WHERE rol_permiso.rol_id = rol.id
  AND rol.codigo = 'EQUIPO_PIE'
  AND (rol_permiso.accion = 'CREAR'
    OR rol_permiso.accion = 'EDITAR'
    OR rol_permiso.accion = 'VALIDAR'
    OR rol_permiso.accion = 'ADMINISTRAR'
    OR rol_permiso.accion = 'EXPORTAR');

-- Blindaje: DIRECCION e INSPECTORIA no generan matrículas.
DELETE FROM rol_permiso
USING rol
WHERE rol_permiso.rol_id = rol.id
  AND rol.codigo IN ('DIRECCION', 'INSPECTORIA')
  AND rol_permiso.accion = 'CREAR';

-- Blindaje: solo SECRETARIA confirma/anula (GENERAL:CREAR ya lo exige el
-- servidor); DIRECCION solo valida, INSPECTORIA solo edita, PIE solo lee.
-- No se tocan TENS / DOCENTE / ADMIN_TI ni se crean UTP / CONVIVENCIA.

COMMIT;
