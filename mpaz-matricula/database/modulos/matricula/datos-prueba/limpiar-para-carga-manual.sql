-- Limpiar los datos de prueba para cargar alumnos reales a mano.
-- PostgreSQL 14+. Ejecutar conectado a la base del portal:
--   psql -X -v ON_ERROR_STOP=1 -d <base> -f limpiar-para-carga-manual.sql
--
-- Borra TODO el movimiento (estudiantes, personas, apoderados, matrículas,
-- fichas, documentos, autorizaciones, salud, apoyos, restricciones, medicación,
-- auditoría y folios) y CONSERVA lo que la operación diaria necesita:
--   · curso            — los 20 cursos (PK A … 8° B) quedan listos para elegir
--   · comuna           — catálogo de comunas
--   · anio_escolar     — el año activo tal como está configurado
--   · plantilla_autorizacion — textos vigentes de las autorizaciones
--   · funcionario, funcionario_rol, rol, rol_permiso — usuarios y accesos
--   · tablas de Identity (AspNet*) y el historial de EF
--
-- Después de ejecutarlo se entra al portal y se registra cada alumno desde
-- cero (matrícula nueva o renovación), sin datos ficticios de por medio.
--
-- TRUNCATE no dispara los triggers que bloquean DELETE: es el método previsto
-- para este vaciado. Requiere permisos administrativos y no se puede deshacer:
-- hacer respaldo antes. NUNCA en producción con datos reales ya cargados.
BEGIN;
SET LOCAL search_path TO mpaz_matricula, public;

TRUNCATE TABLE
    administracion_medicamento,
    antecedente_apoyo,
    antecedente_salud,
    autorizacion_alcance,
    autorizacion,
    contacto_emergencia,
    documento,
    estudiante_persona,
    estudiante,
    ficha_version,
    folio_secuencia,
    historial_cambios,
    matricula_anual,
    persona,
    prescripcion,
    registros_acceso,
    requisito_documental,
    restriccion_familiar,
    retiro_autorizado,
    situacion_medica
RESTART IDENTITY CASCADE;

-- Comprobación: el movimiento en cero y los catálogos intactos.
SELECT 'estudiante' AS tabla, count(*) AS filas FROM estudiante
UNION ALL SELECT 'matricula_anual', count(*) FROM matricula_anual
UNION ALL SELECT 'ficha_version', count(*) FROM ficha_version
UNION ALL SELECT 'documento', count(*) FROM documento
UNION ALL SELECT 'persona', count(*) FROM persona
UNION ALL SELECT 'curso (se conserva)', count(*) FROM curso
UNION ALL SELECT 'comuna (se conserva)', count(*) FROM comuna
UNION ALL SELECT 'anio_escolar (se conserva)', count(*) FROM anio_escolar
UNION ALL SELECT 'funcionario (se conserva)', count(*) FROM funcionario
UNION ALL SELECT 'AspNetUsers (se conserva)', count(*) FROM "AspNetUsers"
ORDER BY 1;

COMMIT;
