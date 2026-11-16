-- Módulo auth · datos de prueba: siembra persona desde funcionario.
-- Conserva los UUID de sso_sujeto para que los `sub` ya emitidos sigan valiendo.
-- Sólo desarrollo. Nunca en producción.

INSERT INTO mpaz_auth.persona (id, correo, nombre, activo)
SELECT sso_sujeto::uuid, lower(correo), nombre, activo
FROM mpaz_matricula.funcionario
WHERE sso_sujeto IS NOT NULL
ON CONFLICT (id) DO UPDATE SET
    correo = EXCLUDED.correo,
    nombre = EXCLUDED.nombre,
    activo = EXCLUDED.activo;
