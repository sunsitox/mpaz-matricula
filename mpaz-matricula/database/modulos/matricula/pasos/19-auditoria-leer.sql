-- =====================================================================
-- MPAZ · Paso 19 — AUDITORIA:LEER para Dirección e Inspectoría
--
-- Prerequisitos (NO se duplican aquí; aplicarlos antes, en orden):
--   · Paso 14 — roles Johan: SECRETARIA / DIRECCION / INSPECTORIA /
--     EQUIPO_PIE (lectura o edición según la matriz; PIE en solo lectura).
--   · Paso 15 — crea UTP y CONVIVENCIA (solo lectura) y recorta TENS.
--     En Neon, UTP y CONVIVENCIA quedan creados y SIN funcionarios
--     asignados: la asignación es un paso posterior, no parte de este SQL.
--   · Paso 16 — antecedente_salud.sale_al_bano (salida al baño).
--   · Paso 18 — curso único «Parvularia» (Pre-kínder y Kínder juntos,
--     sin letra A/B) y retiro de PK A/PK B/K A/K B.
--
-- Este paso aporta SOLO el delta del brief 19-11-2026 que no está en
-- los pasos anteriores (decisiones aprobadas 1–6, 8–10, 14 y 15):
--   · AUDITORIA:LEER para DIRECCION e INSPECTORIA.
--
-- Firmante del certificado (decisión 17, PENDIENTE): NO se agregan
-- columnas firmante_nombre / firmante_cargo a certificado_emitido.
-- La entidad CertificadoEmitido no las mapea y el controlador toma el
-- firmante de DatosInstitucion (appsettings) al generar el PDF, sin
-- persistirlo. Agregar columnas sin conectarlas dejaría datos huérfanos
-- y fijaría una regla de firma que el colegio aún no aprueba (quién
-- firma cuando no hay director). Cuando la decisión 17 se apruebe, un
-- paso posterior agregará las columnas Y su conexión (entidad,
-- DbContext y controlador) en un solo cambio.
--
-- Tampoco incluye (decisiones 16, 18 y 19, pendientes):
--   · fusión UTP + CONVIVENCIA en COORDINACION_EDUCATIVA (dec. 18);
--   · ficha médica del personal de 7 secciones (dec. 16);
--   · tabla observacion_situacion_familiar (dec. 19);
--   · RETIRO:CREAR/EDITAR para UTP (paso 17): UTP sigue en solo
--     lectura hasta que se confirme quién redacta las notas de retiro.
--
-- Idempotente. Ejecutar después del paso 18.
-- =====================================================================

SET search_path = mpaz_matricula, public;

BEGIN;

INSERT INTO rol_permiso (rol_id, area, accion)
SELECT r.id, 'AUDITORIA', 'LEER'
FROM rol r
WHERE r.codigo IN ('DIRECCION', 'INSPECTORIA')
ON CONFLICT (rol_id, area, accion) DO NOTHING;

COMMIT;

-- Verificación ------------------------------------------------------------
-- Si UTP o CONVIVENCIA no aparecen, falta aplicar el paso 15 antes.
SELECT r.codigo AS rol,
       EXISTS (SELECT 1 FROM rol_permiso p
               WHERE p.rol_id = r.id AND p.area = 'AUDITORIA' AND p.accion = 'LEER') AS auditoria_leer,
       (SELECT COUNT(*) FROM funcionario_rol fr WHERE fr.rol_id = r.id) AS funcionarios_asignados
FROM rol r
WHERE r.codigo IN ('DIRECCION', 'INSPECTORIA', 'UTP', 'CONVIVENCIA')
ORDER BY r.codigo;
