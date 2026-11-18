-- =====================================================================
-- 12. MAT-17 · Alertas de vencimiento sobre bases ya instaladas.
--
-- Cambios sobre la versión anterior de la vista:
--   1. Incluye las autorizaciones con estado VENCIDA. Motivo: al abrir la
--      ficha de un alumno, RegistroFicha.MarcarVencidasAsync cambia
--      OTORGADA → VENCIDA; con el filtro anterior (estado = 'OTORGADA') la
--      alerta desaparecía del contador justo al consultarla.
--   2. No lista las autorizaciones ya reemplazadas: si hay otra OTORGADA
--      vigente del mismo tipo y estudiante registrada después, la fila
--      anterior queda como historia y no debe seguir apareciendo como alerta.
--      La comparación usa fecha y, si falta, el id de la fila: una
--      autorización con vigente_desde y fecha_firma nulos también se excluye.
--      Sólo aplica a imagen y convivencia; la medicación admite una
--      autorización por medicamento y sus filas no se reemplazan entre sí.
--   3. Acota los vencimientos a 90 días hacia atrás. Pasado ese plazo la
--      alerta deja de listarse; el registro sigue en la ficha y en el
--      historial. Sin este límite las vencidas se acumulan para siempre.
--
-- Las columnas son las mismas, así que CREATE OR REPLACE VIEW basta y el paso
-- es idempotente. Aditivo: no borra ni modifica datos.
-- =====================================================================

BEGIN;
SET search_path TO mpaz_matricula, public;

CREATE OR REPLACE VIEW v_alertas_vencimiento AS
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

COMMIT;
