-- =====================================================================
-- MPAZ · Paso 24 — Notas de retiro: escriben UTP y Convivencia
--
-- Decisión del colegio (19-11-2026): las notas de retiro (pasos/17) las
-- registran UTP y Convivencia Escolar (LEER/CREAR/EDITAR); Secretaría e
-- Inspectoría sólo las leen (LEER). Dirección sigue en LEER.
--
-- Idempotente. No toca datos. Ejecutar después del paso 23.
-- =====================================================================

SET search_path = mpaz_matricula, public;

-- UTP y Convivencia: escriben y leen.
INSERT INTO mpaz_matricula.rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM mpaz_matricula.rol r
JOIN (VALUES
    ('UTP', 'RETIRO', 'CREAR'),
    ('UTP', 'RETIRO', 'EDITAR'),
    ('CONVIVENCIA', 'RETIRO', 'CREAR'),
    ('CONVIVENCIA', 'RETIRO', 'EDITAR')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;

-- Secretaría e Inspectoría: sólo lectura.
DELETE FROM mpaz_matricula.rol_permiso
WHERE area = 'RETIRO'
  AND accion IN ('CREAR', 'EDITAR', 'VALIDAR')
  AND rol_id IN (
      SELECT id FROM mpaz_matricula.rol WHERE codigo IN ('SECRETARIA', 'INSPECTORIA')
  );
