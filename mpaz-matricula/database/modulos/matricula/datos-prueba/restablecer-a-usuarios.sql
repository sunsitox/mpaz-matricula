-- Restablecer la base a su estado inicial: sólo usuarios.
-- PostgreSQL 14+. Ejecutar conectado a la base de desarrollo/pruebas:
--   psql -X -v ON_ERROR_STOP=1 -U postgres -d mpaz_matricula -f restablecer-a-usuarios.sql
--
-- Borra TODO el movimiento (estudiantes, personas, matrículas, fichas,
-- documentos, autorizaciones, prescripciones, salud, apoyos, restricciones,
-- plantillas, catálogos, años, folios y auditoría) y conserva únicamente:
--   funcionario, funcionario_rol, rol, rol_permiso (usuarios y sus accesos),
--   las tablas de Identity (cuentas de inicio de sesión) y el historial EF.
-- Después hay que volver a configurar el año escolar y los cursos desde el
-- portal, y recrear las plantillas de autorización si se necesitan.
-- TRUNCATE no dispara los triggers que bloquean DELETE: es el método
-- previsto para este vaciado. Requiere permisos administrativos.
-- NUNCA en producción.
BEGIN;
SET LOCAL search_path TO mpaz_matricula, public;

TRUNCATE TABLE
    administracion_medicamento,
    antecedente_apoyo,
    antecedente_salud,
    anio_escolar,
    autorizacion_alcance,
    autorizacion,
    comuna,
    contacto_emergencia,
    curso,
    documento,
    estudiante_persona,
    estudiante,
    ficha_version,
    folio_secuencia,
    historial_cambios,
    matricula_anual,
    persona,
    plantilla_autorizacion,
    prescripcion,
    registros_acceso,
    requisito_documental,
    restriccion_familiar,
    retiro_autorizado,
    situacion_medica
RESTART IDENTITY CASCADE;

-- Comprobación: sólo deben quedar filas en usuarios, roles y cuentas.
SELECT 'funcionario' AS tabla, count(*) AS filas FROM funcionario
UNION ALL SELECT 'funcionario_rol', count(*) FROM funcionario_rol
UNION ALL SELECT 'rol', count(*) FROM rol
UNION ALL SELECT 'rol_permiso', count(*) FROM rol_permiso
UNION ALL SELECT 'AspNetUsers', count(*) FROM "AspNetUsers"
UNION ALL SELECT 'estudiante', count(*) FROM estudiante
UNION ALL SELECT 'matricula_anual', count(*) FROM matricula_anual
UNION ALL SELECT 'ficha_version', count(*) FROM ficha_version
UNION ALL SELECT 'documento', count(*) FROM documento
ORDER BY 1;

COMMIT;
