-- =====================================================================
-- 07 · search_path fijo en las funciones del esquema
--
-- Las funciones de trigger usan nombres de tabla sin calificar
-- (p. ej. fn_auditar_cambio inserta en historial_cambios). Sin un
-- search_path fijo en la función, cualquier sesión sin el search_path
-- del portal (restauraciones, otros clientes) rompe el trigger.
-- Fijarlo en la función lo hace independiente de la sesión y además
-- evita secuestros de search_path.
--
-- Seguro de re-ejecutar: sólo ALTERA atributos, no toca datos.
-- =====================================================================

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
