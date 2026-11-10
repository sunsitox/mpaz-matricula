-- PARTE 3 / 3: DATOS iniciales. PostgreSQL 14+.
-- Ejecutar después de BD y PL, conectado a la misma base.
-- Perfiles institucionales e historial técnico EF; permisos desactivados por defecto.
-- No incluye estudiantes, usuarios ni datos ficticios. Año escolar no fijado.
BEGIN;
SET LOCAL search_path TO mpaz_matricula, public;

INSERT INTO rol (codigo, nombre) VALUES
    ('SECRETARIA',  'Secretaría / matrícula'),
    ('DIRECCION',   'Dirección'),
    ('INSPECTORIA', 'Inspectoría'),
    ('EQUIPO_PIE',  'Equipo PIE'),
    ('TENS',        'TENS'),
    ('DOCENTE',     'Docentes'),
    ('ADMIN_TI',    'Administración TI')
ON CONFLICT (codigo) DO NOTHING;

-- Cambiar FALSE a TRUE sólo con aprobación del colegio.
DO $permisos$
BEGIN
IF FALSE THEN
INSERT INTO rol_permiso (rol_id, area, accion)
SELECT r.id, p.area, p.accion
FROM rol r
JOIN (VALUES
    -- SECRETARIA: crea y mantiene la matrícula y los datos que ella captura.
    ('SECRETARIA', 'GENERAL',       'LEER'),
    ('SECRETARIA', 'GENERAL',       'CREAR'),
    ('SECRETARIA', 'GENERAL',       'EDITAR'),
    ('SECRETARIA', 'FAMILIA',       'LEER'),
    ('SECRETARIA', 'FAMILIA',       'CREAR'),
    ('SECRETARIA', 'FAMILIA',       'EDITAR'),
    ('SECRETARIA', 'RETIRO',        'LEER'),
    ('SECRETARIA', 'RETIRO',        'CREAR'),
    ('SECRETARIA', 'RETIRO',        'EDITAR'),
    ('SECRETARIA', 'AUTORIZACIONES','LEER'),
    ('SECRETARIA', 'AUTORIZACIONES','CREAR'),
    ('SECRETARIA', 'AUTORIZACIONES','EDITAR'),
    ('SECRETARIA', 'DOCUMENTOS',    'LEER'),
    ('SECRETARIA', 'DOCUMENTOS',    'CREAR'),
    ('SECRETARIA', 'DOCUMENTOS',    'EDITAR'),
    ('SECRETARIA', 'GENERAL',       'EXPORTAR'),
    ('SECRETARIA', 'DOCUMENTOS',    'EXPORTAR'),
    ('SECRETARIA', 'SALUD',         'LEER'),
    ('SECRETARIA', 'SALUD',         'CREAR'),
    ('SECRETARIA', 'SALUD',         'EDITAR'),
    ('SECRETARIA', 'JUDICIAL',      'LEER'),
    ('SECRETARIA', 'JUDICIAL',      'CREAR'),
    ('SECRETARIA', 'JUDICIAL',      'EDITAR'),

    -- DIRECCION: consulta institucional y valida lo formalizado.
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
    ('DIRECCION', 'GENERAL',        'EXPORTAR'),
    ('DIRECCION', 'DOCUMENTOS',     'EXPORTAR'),

    -- INSPECTORIA: contacto y retiro, sin diagnósticos.
    ('INSPECTORIA', 'GENERAL',      'LEER'),
    ('INSPECTORIA', 'FAMILIA',      'LEER'),
    ('INSPECTORIA', 'RETIRO',       'LEER'),
    ('INSPECTORIA', 'RETIRO',       'EDITAR'),

    -- EQUIPO_PIE: datos mínimos y apoyos.
    ('EQUIPO_PIE', 'GENERAL',       'LEER'),
    ('EQUIPO_PIE', 'FAMILIA',       'LEER'),
    ('EQUIPO_PIE', 'PIE',           'LEER'),
    ('EQUIPO_PIE', 'PIE',           'EDITAR'),
    ('EQUIPO_PIE', 'DOCUMENTOS',    'LEER'),

    -- TENS: contactos, salud pertinente y medicación.
    -- GENERAL:LEER sólo abre el listado de alumnos (nombre, curso, estado); los
    -- bloques sensibles exigen su propia área, así que no amplía lo que ve.
    ('TENS', 'GENERAL',             'LEER'),
    ('TENS', 'FAMILIA',             'LEER'),
    ('TENS', 'SALUD',               'LEER'),
    ('TENS', 'MEDICACION',          'LEER'),
    ('TENS', 'MEDICACION',          'CREAR'),
    ('TENS', 'DOCUMENTOS',          'LEER'),

    -- DOCENTE: sólo datos mínimos. GENERAL:LEER abre la consulta con identificación,
    -- curso y estado; sin FAMILIA/SALUD/RETIRO/PIE el servidor no le entrega el
    -- resto del expediente (S-02).
    ('DOCENTE', 'GENERAL',          'LEER'),

    -- ADMIN_TI: cuentas y auditoría, sin lectura clínica ni judicial.
    ('ADMIN_TI', 'ADMIN',           'ADMINISTRAR'),
    ('ADMIN_TI', 'ADMIN',           'LEER'),
    ('ADMIN_TI', 'AUDITORIA',       'LEER')
) AS p(codigo, area, accion) ON p.codigo = r.codigo
ON CONFLICT (rol_id, area, accion) DO NOTHING;
END IF;
END $permisos$;

INSERT INTO anio_escolar (anio)
SELECT DISTINCT anio FROM matricula_anual
ON CONFLICT (anio) DO NOTHING;

UPDATE anio_escolar
SET activo_matricula = TRUE
WHERE anio = (SELECT max(anio) FROM matricula_anual)
  AND NOT EXISTS (SELECT 1 FROM anio_escolar WHERE activo_matricula);

-- Metadatos técnicos de EF Core: la estructura Identity ya fue creada en BD.
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
('20261104042752_IdentidadInicial', '10.0.13'),
('20261107022143_EsquemaDedicado', '10.0.13')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
