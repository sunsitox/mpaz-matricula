-- =====================================================================
-- MPAZ · Paso 27 — ADMIN_TI sin acceso a alumnos
--
-- Administración TI gestiona cuentas y auditoría; por diseño no entra al
-- listado ni a la ficha de estudiantes (matriz docs/roles-permisas-actuales.md
-- §5.6). En producción el rol tenía GENERAL:LEER y FAMILIA:LEER, que abrían
-- /Alumnos y el detalle. Este paso los quita y conserva ADMIN y AUDITORIA.
--
-- Idempotente: en bases nuevas no hay filas que borrar (0 filas afectadas).
-- =====================================================================

SET search_path = mpaz_matricula, public;

DELETE FROM mpaz_matricula.rol_permiso rp
USING mpaz_matricula.rol r
WHERE rp.rol_id = r.id
  AND r.codigo = 'ADMIN_TI'
  AND NOT (rp.area = 'ADMIN' OR rp.area = 'AUDITORIA');
