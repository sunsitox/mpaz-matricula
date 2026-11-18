-- =====================================================================
-- MPAZ Web · Matrícula 2027 — Roles UTP / CONVIVENCIA + TENS completo
-- Johan 17-11-2026: UTP ve todo sin editar ni generar; CONVIVENCIA solo ve
-- lo necesario para su función; TENS contactos + salud + medicación.
--
-- - UTP: ver todo (LEER todas las áreas) + EXPORTAR, sin CREAR/EDITAR/VALIDAR.
-- - CONVIVENCIA: solo necesario (GENERAL/FAMILIA/RETIRO/AUTORIZACIONES/
--   DOCUMENTOS:LEER), sin SALUD/PIE/JUDICIAL/MEDICACION ni escritura.
-- - TENS: GENERAL/FAMILIA/SALUD/MEDICACION/DOCUMENTOS:LEER +
--   MEDICACION:CREAR (registra dosis; el trigger valida rol TENS activo,
--   orden VIGENTE validada y autorización otorgada vigente).
--
-- Re-ejecutable. No toca SECRETARIA/DIRECCION/INSPECTORIA/PIE/DOCENTE/ADMIN_TI.
-- Uso en producción de medicación exige protocolo TENS aprobado por el colegio.
-- =====================================================================

BEGIN;

-- Nuevos perfiles (Johan 17-11-2026).
INSERT INTO rol (codigo, nombre) VALUES
    ('UTP',         'UTP'),
    ('CONVIVENCIA', 'Convivencia Escolar')
ON CONFLICT (codigo) DO NOTHING;

-- UTP: ver todo, solo lectura + exportar.
INSERT INTO rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM rol r
JOIN (VALUES
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
    ('UTP', 'DOCUMENTOS',     'EXPORTAR')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;

-- CONVIVENCIA: solo lo necesario para su función.
INSERT INTO rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM rol r
JOIN (VALUES
    ('CONVIVENCIA', 'GENERAL',        'LEER'),
    ('CONVIVENCIA', 'FAMILIA',        'LEER'),
    ('CONVIVENCIA', 'RETIRO',         'LEER'),
    ('CONVIVENCIA', 'AUTORIZACIONES', 'LEER'),
    ('CONVIVENCIA', 'DOCUMENTOS',     'LEER')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;

-- TENS completo (sin VALIDAR/EDITAR/EXPORTAR).
INSERT INTO rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM rol r
JOIN (VALUES
    ('TENS', 'GENERAL',    'LEER'),
    ('TENS', 'FAMILIA',    'LEER'),
    ('TENS', 'SALUD',      'LEER'),
    ('TENS', 'MEDICACION', 'LEER'),
    ('TENS', 'MEDICACION', 'CREAR'),
    ('TENS', 'DOCUMENTOS', 'LEER')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;

-- Blindaje escritura: UTP / CONVIVENCIA / TENS no generan ni validan.
DELETE FROM rol_permiso
USING rol
WHERE rol_permiso.rol_id = rol.id
  AND rol.codigo IN ('UTP', 'CONVIVENCIA')
  AND rol_permiso.accion IN ('CREAR', 'EDITAR', 'VALIDAR', 'ADMINISTRAR');

DELETE FROM rol_permiso
USING rol
WHERE rol_permiso.rol_id = rol.id
  AND rol.codigo = 'TENS'
  AND rol_permiso.accion IN ('EDITAR', 'VALIDAR', 'ADMINISTRAR', 'EXPORTAR');

COMMIT;
