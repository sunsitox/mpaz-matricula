-- =====================================================================
-- Datos de prueba (parte 2) · Portal de Matrícula 2027
-- Escuela Escritora Marcela Paz · base local mpaz-matricula
--
-- Complementa "datos mock.sql" (estudiantes, matrículas y su ficha base)
-- y deja TODAS las tablas del DDL con datos ficticios:
--
--   0. Completar apoderados, contactos, retiros, salud y PIE
--   1. funcionario · funcionario_rol · rol_permiso
--   2. plantilla_autorizacion
--   3. documento (fichas 2026, escaneos, orden médica, doc judicial,
--      cédula, certificado de nacimiento, certificado PIE)
--   4. ficha_version (fichas cerradas con folio MP-2026-xxxxx)
--   5. autorizacion · autorizacion_alcance (otorgada, rechazada y pendiente)
--   6. prescripcion · administracion_medicamento (caso de asma con TENS)
--   7. requisito_documental de las matrículas 2027
--   8. registros_acceso (muestra de auditoría)
--   9. folio_secuencia se llena sola con el trigger de ficha_version.
--
-- TODOS los datos son FICTICIOS (desarrollo local). Las plantillas y la
-- matriz de roles son propuestas de ejemplo: el DDL pide validarlas con
-- Dirección antes de usarlas en producción.
--
-- Idempotente: se puede correr otra vez sin duplicar filas.
-- Uso:
--   psql "postgresql://postgres:CLAVE@localhost:5432/mpaz-matricula" \
--        -v ON_ERROR_STOP=1 -f "datos mock - tablas de gestion y expediente.sql"
-- =====================================================================

begin;

-- ---------------------------------------------------------------------
-- 0. COMPLETAR LA FICHA DE LOS ESTUDIANTES
--    · un padre para los tres casos que solo tenían madre
--    · apoderado secundario donde faltaba
--    · dos contactos de emergencia por estudiante
--    · al menos dos personas autorizadas a retirar por estudiante
--    · situaciones médicas variadas, contacto sanitario y PIE
-- ---------------------------------------------------------------------

-- 0a. Padres faltantes (personas ficticias, RUN con dígito válido).
insert into persona (identificador, nombres, apellidos, telefono, correo, domicilio, escolaridad)
select normalizar_rut(v.run), v.nombres, v.apellidos, v.telefono, v.correo, v.domicilio, v.escolaridad
from (values
    ('13.123.456-2', 'Jorge Andrés', 'Sandoval Riquelme', '+56 9 6123 7788', 'jorge.sandoval@correo.cl',
     'Calle Freire 2210, Villa Las Acacias', 'Educación media técnico-profesional'),
    ('17.123.456-5', 'Patricio Emilio', 'Núñez Salas', '+56 9 6234 8899', 'patricio.nunez@correo.cl',
     'Calle Balmaceda 1890, Depto 12', 'Educación media'),
    ('18.123.456-3', 'Cristóbal Ignacio', 'Morales Fuentes', '+56 9 6345 9900', 'cristobal.morales@correo.cl',
     'SIN INFORMAR', 'Educación media')
) as v(run, nombres, apellidos, telefono, correo, domicilio, escolaridad)
on conflict (tipo_identificador, identificador) do nothing;

-- 0b. Relaciones padre (y apoderado secundario) que faltaban.
insert into estudiante_persona (estudiante_id, persona_id, parentesco, tipo_apoderado, tiene_restriccion)
select e.id, p.id, 'PADRE', 'SECUNDARIO', null
from (values
    ('22.334.455-0', '13.123.456-2'),
    ('23.667.788-5', '17.123.456-5'),
    ('25.889.900-8', '18.123.456-3')
) as v(est_run, per_run)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
join persona    p on p.identificador = normalizar_rut(v.per_run)
where not exists (select 1 from estudiante_persona ep
                  where ep.estudiante_id = e.id and ep.persona_id = p.id
                    and ep.vigente_hasta is null)
  and not exists (select 1 from estudiante_persona ep2
                  where ep2.estudiante_id = e.id and ep2.parentesco = 'PADRE'
                    and ep2.vigente_hasta is null);

-- 0c. El padre de Tomás Vicente queda como apoderado secundario.
update estudiante_persona ep
set tipo_apoderado = 'SECUNDARIO'
from estudiante e, persona p
where ep.estudiante_id = e.id
  and ep.persona_id = p.id
  and ep.vigente_hasta is null
  and ep.parentesco = 'PADRE'
  and ep.tipo_apoderado is null
  and e.identificador = normalizar_rut('23.001.122-2')
  and p.identificador = normalizar_rut('13.889.900-4');

-- 0d. Contactos de emergencia: completar hasta dos por estudiante.
insert into contacto_emergencia (estudiante_id, orden, nombre, telefono, vinculo)
select e.id, v.orden, v.nombre, v.telefono, v.vinculo
from (values
    ('22.334.455-0', 2, 'Jorge Andrés Sandoval Riquelme', '+56 9 6123 7788', 'padre'),
    ('23.667.788-5', 2, 'Patricio Emilio Núñez Salas',    '+56 9 6234 8899', 'padre'),
    ('22.998.877-8', 2, 'Carla Herrera Peña',             '+56 9 6444 7171', 'tía'),
    ('24.778.899-9', 1, 'Francisca Ignacia Fuentes Soto', '+56 9 6999 1122', 'madre'),
    ('24.778.899-9', 2, 'Camila Escobar Rivas',           '+56 9 6111 8383', 'tía'),
    ('25.889.900-8', 1, 'Rocío Tapia Vega',               '+56 9 6333 5566', 'madre'),
    ('25.889.900-8', 2, 'Cristóbal Ignacio Morales Fuentes','+56 9 6345 9900','padre')
) as v(est_run, orden, nombre, telefono, vinculo)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
where not exists (select 1 from contacto_emergencia ce
                  where ce.estudiante_id = e.id and ce.orden = v.orden
                    and ce.vigente_hasta is null);

-- 0e. Retiros autorizados: al menos dos por estudiante.
insert into retiro_autorizado (estudiante_id, nombre, identificador, vinculo)
select e.id, v.nombre, normalizar_rut(v.run), v.vinculo
from (values
    ('22.334.455-0', 'Jorge Andrés Sandoval Riquelme', '13.123.456-2', 'padre'),
    ('23.667.788-5', 'Patricio Emilio Núñez Salas',    '17.123.456-5', 'padre'),
    ('24.778.899-9', 'Francisca Ignacia Fuentes Soto', '20.556.677-5', 'madre'),
    ('24.778.899-9', 'Cristóbal Escobar Rivas',        '12.345.678-5', 'abuelo'),
    ('25.889.900-8', 'Rocío Tapia Vega',               '18.889.900-5', 'madre'),
    ('25.889.900-8', 'Cristóbal Ignacio Morales Fuentes','18.123.456-3','padre')
) as v(est_run, nombre, run, vinculo)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
where not exists (select 1 from retiro_autorizado ra
                  where ra.estudiante_id = e.id
                    and ra.identificador = normalizar_rut(v.run)
                    and ra.vigente_hasta is null);

-- 0f. Situaciones médicas variadas (el caso de asma ya viene del mock).
insert into situacion_medica (estudiante_id, tipo, descripcion, en_tratamiento)
select e.id, v.tipo, v.descripcion, v.tratamiento
from (values
    ('21.456.789-K', 'ALERGIA',  'Alergia a frutos secos (maní y nueces)', FALSE),
    ('22.334.455-0', 'CONDICION','Rinitis alérgica estacional', FALSE),
    ('24.455.667-1', 'CONDICION','Epilepsia focal controlada con medicación', TRUE),
    ('22.998.877-8', 'ALERGIA',  'Alergia a la penicilina', FALSE)
) as v(est_run, tipo, descripcion, tratamiento)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
where not exists (select 1 from situacion_medica s
                  where s.estudiante_id = e.id
                    and s.descripcion = v.descripcion
                    and s.vigente_hasta is null);

-- 0g. Antecedente de salud: marcar situación médica y contacto sanitario.
update antecedente_salud s
set tiene_situacion_medica = true,
    necesidades_atencion = v.necesidades,
    contacto_sanitario_nombre = v.san_nombre,
    contacto_sanitario_telefono = v.san_tel
from (values
    ('21.456.789-K', 'Evitar frutos secos en colaciones, cumpleaños y salidas. Avisar a inspectoría si presenta hinchazón o dificultad respiratoria.',
     'Paola Ximena Riquelme Díaz (madre)', '+56 9 6789 0123'),
    ('22.334.455-0', 'Antihistamínico indicado por la familia en primavera. Autorizado a salir del aula si hay crisis de estornudos.',
     'Daniela Soledad Muñoz Pérez (madre)', '+56 9 6123 9876'),
    ('24.455.667-1', 'Registro de crisis y protocolo de la familia en secretaría. Evitar luces estroboscópicas en actos escolares.',
     'Óscar Iván Contreras Bustos (padre)', '+56 9 6222 3344'),
    ('25.123.456-6', 'Usa inhalador de rescate; el colegio mantiene uno en secretaría por acuerdo con la familia.',
     'Andrea Paz Pizarro Rojas (madre)', '+56 9 6555 6677')
) as v(run, necesidades, san_nombre, san_tel)
join estudiante e on e.identificador = normalizar_rut(v.run)
where s.estudiante_id = e.id
  and (s.tiene_situacion_medica is distinct from true
       or s.necesidades_atencion is distinct from v.necesidades
       or s.contacto_sanitario_nombre is distinct from v.san_nombre);

-- 0h. PIE: un caso más con apoyo declarado y diagnóstico.
update antecedente_apoyo a
set participa_pie = 'SI',
    apoyos_informados = 'Apoyo fonoaudiológico en aula de recursos, una vez por semana (datos de prueba).',
    tiene_diagnostico = true
from estudiante e
where a.estudiante_id = e.id
  and e.identificador = normalizar_rut('21.456.789-K')
  and a.participa_pie is distinct from 'SI';

-- 0i. El padre de Vicente Tomás queda como apoderado secundario.
update estudiante_persona ep
set tipo_apoderado = 'SECUNDARIO'
from estudiante e
where ep.estudiante_id = e.id
  and ep.vigente_hasta is null
  and ep.parentesco = 'PADRE'
  and ep.tipo_apoderado is null
  and e.identificador = normalizar_rut('24.778.899-9');

-- 0j. Situaciones médicas para los casos que quedaban sin una.
insert into situacion_medica (estudiante_id, tipo, descripcion, en_tratamiento)
select e.id, v.tipo, v.descripcion, v.tratamiento
from (values
    ('23.001.122-2', 'CONDICION', 'Migraña con aura; requiere reposo en enfermería', FALSE),
    ('23.667.788-5', 'ALERGIA',   'Dermatitis atópica (alergia de contacto a jabones perfumados)', FALSE),
    ('24.778.899-9', 'CONDICION', 'Sibilancias con resfríos (asma leve informada en 2024)', TRUE),
    ('25.889.900-8', 'ALERGIA',   'Alergia al huevo', FALSE)
) as v(est_run, tipo, descripcion, tratamiento)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
where not exists (select 1 from situacion_medica s
                  where s.estudiante_id = e.id
                    and s.descripcion = v.descripcion
                    and s.vigente_hasta is null);

-- 0k. Antecedente de salud de esos casos, con contacto sanitario.
update antecedente_salud s
set tiene_situacion_medica = true,
    necesidades_atencion = v.necesidades,
    contacto_sanitario_nombre = v.san_nombre,
    contacto_sanitario_telefono = v.san_tel
from (values
    ('23.001.122-2', 'Avisar a inspectoría y permitir reposo en enfermería con luz baja si presenta dolor de cabeza con visión borrosa.',
     'Verónica del Carmen Lagos Rivas (madre)', '+56 9 6345 1122'),
    ('23.667.788-5', 'Usar jabón sin perfume y evitar cremas de terceros. Avisar a la familia si aparece brote.',
     'Claudia Marcela Cáceres Vera (madre)', '+56 9 6888 9900'),
    ('24.778.899-9', 'Prestar atención a sibilancias cuando hay resfrío; avisar a la familia si hay dificultad para respirar.',
     'Francisca Ignacia Fuentes Soto (madre)', '+56 9 6999 1122'),
    ('25.889.900-8', 'Evitar preparaciones con huevo en celebraciones y colaciones; avisar a inspectoría ante reacción.',
     'Rocío Tapia Vega (madre)', '+56 9 6333 5566'),
    ('22.998.877-8', 'Alergia declarada a la penicilina: registrar en ficha de enfermería y avisar antes de cualquier tratamiento.',
     'Marisol Herrera Peña (madre)', '+56 9 6111 3344')
) as v(run, necesidades, san_nombre, san_tel)
join estudiante e on e.identificador = normalizar_rut(v.run)
where s.estudiante_id = e.id
  and (s.tiene_situacion_medica is distinct from true
       or s.necesidades_atencion is distinct from v.necesidades
       or s.contacto_sanitario_nombre is distinct from v.san_nombre);

-- ---------------------------------------------------------------------
-- 1. FUNCIONARIOS + ROLES + MATRIZ DE PERMISOS (ficticios)
-- ---------------------------------------------------------------------
insert into funcionario (nombre, correo, sso_sujeto, activo) values
    ('María Teresa Lagos Sandoval',    'mteresa.lagos@mpaz.cl',       'mock:11111111-1', true),
    ('Cecilia Andrea Fernández Rojas', 'directora.fernandez@mpaz.cl', 'mock:14123456-0', true),
    ('Iván Patricio Mendoza Castillo', 'inspectoria.mendoza@mpaz.cl', 'mock:15123456-9', true),
    ('Patricia Elena Salgado Rojas',   'tens.salgado@mpaz.cl',        'mock:16123456-7', true),
    ('Claudia del Pilar Mena Oses',    'pie.mena@mpaz.cl',            'mock:19123456-1', true),
    ('Sebastián Ignacio Ramírez Porte','docente.ramirez@mpaz.cl',    'mock:20123456-7', true),
    ('Rodrigo Andrés Palma Verdugo',   'admin.palma@mpaz.cl',         'mock:20012345-K', true)
on conflict (correo) do nothing;

-- Desde los cambios los triggers de auditoría registran al funcionario.
select set_config('app.funcionario_id', (
    select id::text from funcionario where correo = 'mteresa.lagos@mpaz.cl'), false);

insert into funcionario_rol (funcionario_id, rol_id, desde)
select f.id, r.id, date '2026-01-02'
from (values
    ('mteresa.lagos@mpaz.cl',       'SECRETARIA'),
    ('directora.fernandez@mpaz.cl', 'DIRECCION'),
    ('inspectoria.mendoza@mpaz.cl', 'INSPECTORIA'),
    ('tens.salgado@mpaz.cl',        'TENS'),
    ('pie.mena@mpaz.cl',            'EQUIPO_PIE'),
    ('docente.ramirez@mpaz.cl',     'DOCENTE'),
    ('admin.palma@mpaz.cl',         'ADMIN_TI')
) as v(correo, rol)
join funcionario f on f.correo = v.correo
join rol r on r.codigo = v.rol
on conflict do nothing;

-- Matriz de ejemplo (propuesta; validar con Dirección antes de producción).
insert into rol_permiso (rol_id, area, accion)
select r.id, v.area, v.accion
from (values
    ('SECRETARIA',  'GENERAL',        'LEER'),
    ('SECRETARIA',  'GENERAL',        'CREAR'),
    ('SECRETARIA',  'GENERAL',        'EDITAR'),
    ('SECRETARIA',  'GENERAL',        'EXPORTAR'),
    ('SECRETARIA',  'FAMILIA',        'LEER'),
    ('SECRETARIA',  'FAMILIA',        'CREAR'),
    ('SECRETARIA',  'FAMILIA',        'EDITAR'),
    ('SECRETARIA',  'RETIRO',         'LEER'),
    ('SECRETARIA',  'RETIRO',         'CREAR'),
    ('SECRETARIA',  'RETIRO',         'EDITAR'),
    ('SECRETARIA',  'DOCUMENTOS',     'LEER'),
    ('SECRETARIA',  'DOCUMENTOS',     'CREAR'),
    ('SECRETARIA',  'DOCUMENTOS',     'EDITAR'),
    ('SECRETARIA',  'DOCUMENTOS',     'EXPORTAR'),
    ('SECRETARIA',  'AUDITORIA',      'LEER'),
    ('INSPECTORIA', 'GENERAL',        'LEER'),
    ('INSPECTORIA', 'RETIRO',         'LEER'),
    ('INSPECTORIA', 'RETIRO',         'EDITAR'),
    ('INSPECTORIA', 'RETIRO',         'VALIDAR'),
    ('INSPECTORIA', 'DOCUMENTOS',     'LEER'),
    ('INSPECTORIA', 'JUDICIAL',       'LEER'),
    ('TENS',        'SALUD',          'LEER'),
    ('TENS',        'SALUD',          'VALIDAR'),
    ('TENS',        'MEDICACION',     'LEER'),
    ('TENS',        'MEDICACION',     'VALIDAR'),
    ('TENS',        'MEDICACION',     'ADMINISTRAR'),
    ('EQUIPO_PIE',  'PIE',            'LEER'),
    ('EQUIPO_PIE',  'PIE',            'CREAR'),
    ('EQUIPO_PIE',  'PIE',            'EDITAR'),
    ('EQUIPO_PIE',  'PIE',            'VALIDAR'),
    ('EQUIPO_PIE',  'DOCUMENTOS',     'LEER'),
    ('DIRECCION',   'GENERAL',        'LEER'),
    ('DIRECCION',   'FAMILIA',        'LEER'),
    ('DIRECCION',   'RETIRO',         'LEER'),
    ('DIRECCION',   'SALUD',          'LEER'),
    ('DIRECCION',   'PIE',            'LEER'),
    ('DIRECCION',   'JUDICIAL',       'LEER'),
    ('DIRECCION',   'AUTORIZACIONES', 'LEER'),
    ('DIRECCION',   'MEDICACION',     'LEER'),
    ('DIRECCION',   'DOCUMENTOS',     'LEER'),
    ('DIRECCION',   'AUDITORIA',      'LEER'),
    ('DOCENTE',     'GENERAL',        'LEER'),
    ('ADMIN_TI',    'AUDITORIA',      'LEER'),
    ('ADMIN_TI',    'ADMIN',          'ADMINISTRAR')
) as v(rol, area, accion)
join rol r on r.codigo = v.rol
on conflict do nothing;

-- ---------------------------------------------------------------------
-- 2. PLANTILLAS DE AUTORIZACIÓN (texto ficticio para pruebas)
-- ---------------------------------------------------------------------
insert into plantilla_autorizacion (tipo, version, texto, aprobado_por, vigente_desde)
values
    ('IMAGEN', 1,
     '[FICTICIA] Autorizo que la Escuela Marcela Paz capte, use y publique imágenes de mi hijo/a en: captación interna, sitio institucional, redes sociales y otros medios. Puedo aceptar o rechazar cada canal por separado.',
     'Dirección (datos de prueba)', date '2026-01-02'),
    ('CONVIVENCIA', 1,
     '[FICTICIA] Autorizo que el equipo de convivencia escolar acompañe a mi hijo/a en: apoyo socioemocional, mediación ante conflictos e intervención de apoyo PIE, según el protocolo institucional.',
     'Dirección (datos de prueba)', date '2026-01-02'),
    ('MEDICACION', 1,
     '[FICTICIA] Autorizo que la TENS administre el medicamento prescrito por el profesional de salud según la orden médica adjunta y la dosificación indicada en la ficha.',
     'Dirección (datos de prueba)', date '2026-01-02')
on conflict (tipo, version) do nothing;

-- ---------------------------------------------------------------------
-- 3. DOCUMENTOS (archivos ficticios: rutas y hash de ejemplo)
-- ---------------------------------------------------------------------
-- 3a. PDF y escaneo de la ficha de cada matrícula 2026 FIRMADA.
insert into documento (estudiante_id, matricula_id, tipo, sensibilidad, nombre_original,
                       mime_type, tamano_bytes, ruta_almacenamiento, hash_sha256,
                       estado_analisis, subido_por)
select e.id, m.id, 'FICHA_PDF', 'GENERAL',
       'Ficha 2026 ' || e.identificador || '.pdf',
       'application/pdf',
       110000 + e.id * 137,
       'mock/2026/ficha-' || e.identificador || '.pdf',
       md5(e.identificador) || md5(e.identificador || 'pdf-ficha-2026'),
       'LIMPIO',
       (select id from funcionario where correo = 'mteresa.lagos@mpaz.cl')
from matricula_anual m
join estudiante e on e.id = m.estudiante_id
where m.anio = 2026 and m.estado = 'FIRMADA'
  and not exists (select 1 from documento d
                  where d.ruta_almacenamiento = 'mock/2026/ficha-' || e.identificador || '.pdf');

insert into documento (estudiante_id, matricula_id, tipo, sensibilidad, nombre_original,
                       mime_type, tamano_bytes, ruta_almacenamiento, hash_sha256,
                       estado_analisis, subido_por)
select e.id, m.id, 'FICHA_FIRMADA', 'GENERAL',
       'Ficha 2026 firmada ' || e.identificador || '.pdf',
       'application/pdf',
       125000 + e.id * 189,
       'mock/2026/ficha-firmada-' || e.identificador || '.pdf',
       md5(e.identificador) || md5(e.identificador || 'pdf-ficha-firmada-2026'),
       'LIMPIO',
       (select id from funcionario where correo = 'mteresa.lagos@mpaz.cl')
from matricula_anual m
join estudiante e on e.id = m.estudiante_id
where m.anio = 2026 and m.estado = 'FIRMADA'
  and not exists (select 1 from documento d
                  where d.ruta_almacenamiento = 'mock/2026/ficha-firmada-' || e.identificador || '.pdf');

-- 3b. Documentos de respaldo: cédulas, certificados, orden médica y
--     resolución judicial.
insert into documento (estudiante_id, tipo, sensibilidad, nombre_original,
                       mime_type, tamano_bytes, ruta_almacenamiento, hash_sha256,
                       estado_analisis, subido_por)
select e.id, v.tipo, v.sensibilidad, v.nombre, v.mime, v.bytes, v.ruta,
       md5(e.identificador) || md5(v.ruta), 'LIMPIO',
       (select id from funcionario where correo = v.subido_por)
from (values
    ('20.114.567-8',  'CEDULA_IDENTIDAD',       'GENERAL',  'Cédula de identidad apoderada (anverso).jpg', 'image/jpeg',      340000, 'mock/general/cedula-20.114.567-8.jpg',        'mteresa.lagos@mpaz.cl'),
    ('20.114.567-8',  'CERTIFICADO_NACIMIENTO', 'GENERAL',  'Certificado de nacimiento.pdf',               'application/pdf', 620000, 'mock/general/nacimiento-20.114.567-8.pdf',    'mteresa.lagos@mpaz.cl'),
    ('21.456.789-K',  'CEDULA_IDENTIDAD',       'GENERAL',  'Cédula de identidad apoderada (anverso).jpg', 'image/jpeg',      345000, 'mock/general/cedula-21.456.789-K.jpg',        'mteresa.lagos@mpaz.cl'),
    ('21.456.789-K',  'CERTIFICADO_PIE',        'PIE',      'Diagnóstico fonoaudiológico.pdf',             'application/pdf', 410000, 'mock/pie/diagnostico-21.456.789-K.pdf',       'pie.mena@mpaz.cl'),
    ('23.667.788-5',  'CERTIFICADO_PIE',        'PIE',      'Informe de especialista (TDAH).pdf',          'application/pdf', 380000, 'mock/pie/informe-23.667.788-5.pdf',           'pie.mena@mpaz.cl'),
    ('24.455.667-1',  'CEDULA_IDENTIDAD',       'GENERAL',  'Cédula de identidad apoderada (anverso).jpg', 'image/jpeg',      355000, 'mock/general/cedula-24.455.667-1.jpg',        'mteresa.lagos@mpaz.cl'),
    ('24.455.667-1',  'DOCUMENTO_JUDICIAL',     'JUDICIAL', 'Resolución judicial (copia).pdf',             'application/pdf', 900000, 'mock/judicial/resolucion-24.455.667-1.pdf',   'inspectoria.mendoza@mpaz.cl'),
    ('25.123.456-6',  'ORDEN_MEDICA',           'SALUD',    'Orden médica salbutamol.pdf',                 'application/pdf', 280000, 'mock/salud/orden-medica-25.123.456-6.pdf',    'tens.salgado@mpaz.cl'),
    ('25.123.456-6',  'RESPALDO_SALUD',         'SALUD',    'Informe médico bronquial.pdf',                'application/pdf', 300000, 'mock/salud/informe-25.123.456-6.pdf',         'tens.salgado@mpaz.cl')
) as v(run, tipo, sensibilidad, nombre, mime, bytes, ruta, subido_por)
join estudiante e on e.identificador = normalizar_rut(v.run)
where not exists (select 1 from documento d where d.ruta_almacenamiento = v.ruta);

-- ---------------------------------------------------------------------
-- 4. FICHAS CERRADAS 2026 (ficha_version) con folio del año 2026.
--    2027 se deja sin ficha: la genera la app con funcionario registrado.
-- ---------------------------------------------------------------------
insert into ficha_version (matricula_id, numero_version, estado, datos_snapshot,
                           generada_por, pdf_documento_id, firmante_persona_id, fecha_firma,
                           escaneo_documento_id, escaneo_subido_por, escaneo_subido_en,
                           ubicacion_original_fisico, estado_validacion,
                           validado_por, validado_en, observacion_validacion)
select
    m.id,
    1,
    'VIGENTE',
    jsonb_build_object('anio', 2026, 'origen', 'datos mock.sql',
                       'nota', 'copia congelada ficticia de la ficha'),
    (select id from funcionario where correo = 'mteresa.lagos@mpaz.cl'),
    dp.id,
    mad.persona_id,
    date '2026-02-27',
    dd.id,
    (select id from funcionario where correo = 'mteresa.lagos@mpaz.cl'),
    now() - interval '20 days',
    'Archivo 2026 · gaveta ' || chr(65 + (e.id % 5)) || '-' || lpad(e.id::text, 3, '0'),
    case when e.identificador in
             ('20114567-8','21456789-K','22334455-0','23001122-2','24455667-1','22998877-8')
         then 'VALIDADA' else 'PENDIENTE' end,
    (select id from funcionario where correo = 'directora.fernandez@mpaz.cl'),
    '2026-03-05 09:00-03'::timestamptz,
    'Validación ficticia: original físico revisado contra el escaneo.'
from matricula_anual m
join estudiante e on e.id = m.estudiante_id
left join documento dp on dp.estudiante_id = e.id and dp.tipo = 'FICHA_PDF'
                      and dp.matricula_id = m.id
left join documento dd on dd.estudiante_id = e.id and dd.tipo = 'FICHA_FIRMADA'
                      and dd.matricula_id = m.id
left join estudiante_persona mad on mad.estudiante_id = e.id
     and mad.parentesco = 'MADRE' and mad.vigente_hasta is null
where m.anio = 2026 and m.estado = 'FIRMADA'
  and not exists (select 1 from ficha_version fv where fv.matricula_id = m.id);

-- ---------------------------------------------------------------------
-- 5. AUTORIZACIONES: imagen y convivencia para todos, con un caso
--    RECHAZADO (imagen) y uno PENDIENTE (convivencia); medicación para
--    el caso con orden médica.
-- ---------------------------------------------------------------------
insert into autorizacion (estudiante_id, tipo, plantilla_id, estado, proposito,
                          firmante_persona_id, fecha_firma, ficha_version_id,
                          vigente_desde, vigente_hasta)
select
    e.id,
    t.tipo,
    pl.id,
    t.estado,
    t.proposito,
    case when t.estado = 'OTORGADA' then mad.persona_id end,
    case when t.estado = 'OTORGADA' then date '2026-02-27' end,
    fv.id,
    case when t.estado = 'OTORGADA' then date '2026-03-02' end,
    case when t.estado = 'OTORGADA' then date '2027-12-31' end
from estudiante e
cross join (values
    ('IMAGEN',      'OTORGADA',  'Uso de imagen del estudiante, canales según alcance.'),
    ('CONVIVENCIA', 'OTORGADA',  'Acompañamiento del equipo de convivencia escolar.')
) as t(tipo, estado, proposito)
join plantilla_autorizacion pl on pl.tipo = t.tipo and pl.version = 1
left join estudiante_persona mad on mad.estudiante_id = e.id
     and mad.parentesco = 'MADRE' and mad.vigente_hasta is null
left join matricula_anual m2 on m2.estudiante_id = e.id and m2.anio = 2026
left join ficha_version fv on fv.matricula_id = m2.id and fv.numero_version = 1
where not exists (select 1 from autorizacion a
                  where a.estudiante_id = e.id and a.tipo = t.tipo)
  and not (t.tipo = 'IMAGEN'      and e.identificador = normalizar_rut('22.998.877-8'))
  and not (t.tipo = 'CONVIVENCIA' and e.identificador = normalizar_rut('24.778.899-9'));

-- Caso RECHAZADO: la familia no autoriza uso de imagen en redes ni otros.
insert into autorizacion (estudiante_id, tipo, plantilla_id, estado, proposito,
                          ficha_version_id)
select e.id, 'IMAGEN', pl.id, 'RECHAZADA',
       'La familia rechazó el uso de imagen para el ciclo 2027.',
       fv.id
from estudiante e
join plantilla_autorizacion pl on pl.tipo = 'IMAGEN' and pl.version = 1
left join matricula_anual m2 on m2.estudiante_id = e.id and m2.anio = 2026
left join ficha_version fv on fv.matricula_id = m2.id and fv.numero_version = 1
where e.identificador = normalizar_rut('22.998.877-8')
  and not exists (select 1 from autorizacion a
                  where a.estudiante_id = e.id and a.tipo = 'IMAGEN');

-- Caso PENDIENTE: falta la firma de la familia para convivencia.
insert into autorizacion (estudiante_id, tipo, plantilla_id, estado, proposito,
                          ficha_version_id)
select e.id, 'CONVIVENCIA', pl.id, 'PENDIENTE',
       'Pendiente de firma del apoderado para el ciclo 2027.',
       fv.id
from estudiante e
join plantilla_autorizacion pl on pl.tipo = 'CONVIVENCIA' and pl.version = 1
left join matricula_anual m2 on m2.estudiante_id = e.id and m2.anio = 2026
left join ficha_version fv on fv.matricula_id = m2.id and fv.numero_version = 1
where e.identificador = normalizar_rut('24.778.899-9')
  and not exists (select 1 from autorizacion a
                  where a.estudiante_id = e.id and a.tipo = 'CONVIVENCIA');

-- Autorización de medicación (sólo el estudiante con orden médica).
insert into autorizacion (estudiante_id, tipo, plantilla_id, estado, proposito,
                          firmante_persona_id, fecha_firma, ficha_version_id,
                          vigente_desde, vigente_hasta)
select e.id, 'MEDICACION', pl.id, 'OTORGADA',
       'Administración de salbutamol según orden médica vigente.',
       mad.persona_id, date '2026-03-01', fv.id,
       date '2026-03-02', date '2027-12-31'
from estudiante e
join plantilla_autorizacion pl on pl.tipo = 'MEDICACION' and pl.version = 1
left join estudiante_persona mad on mad.estudiante_id = e.id
     and mad.parentesco = 'MADRE' and mad.vigente_hasta is null
left join matricula_anual m2 on m2.estudiante_id = e.id and m2.anio = 2026
left join ficha_version fv on fv.matricula_id = m2.id and fv.numero_version = 1
where e.identificador = normalizar_rut('25.123.456-6')
  and not exists (select 1 from autorizacion a
                  where a.estudiante_id = e.id and a.tipo = 'MEDICACION');

-- Alcances por canal (sólo sobre autorizaciones OTORGADAS).
insert into autorizacion_alcance (autorizacion_id, alcance, otorgado)
select a.id, 'Captación interna', true
from autorizacion a
where a.tipo = 'IMAGEN' and a.estado = 'OTORGADA'
  and not exists (select 1 from autorizacion_alcance aa
                  where aa.autorizacion_id = a.id and aa.alcance = 'Captación interna');

insert into autorizacion_alcance (autorizacion_id, alcance, otorgado)
select a.id, 'Sitio institucional', true
from autorizacion a
where a.tipo = 'IMAGEN' and a.estado = 'OTORGADA'
  and not exists (select 1 from autorizacion_alcance aa
                  where aa.autorizacion_id = a.id and aa.alcance = 'Sitio institucional');

insert into autorizacion_alcance (autorizacion_id, alcance, otorgado)
select a.id, 'Redes sociales', (a.estudiante_id % 2) = 1
from autorizacion a
where a.tipo = 'IMAGEN' and a.estado = 'OTORGADA'
  and not exists (select 1 from autorizacion_alcance aa
                  where aa.autorizacion_id = a.id and aa.alcance = 'Redes sociales');

insert into autorizacion_alcance (autorizacion_id, alcance, otorgado)
select a.id, 'Otros medios', false
from autorizacion a
where a.tipo = 'IMAGEN' and a.estado = 'OTORGADA'
  and not exists (select 1 from autorizacion_alcance aa
                  where aa.autorizacion_id = a.id and aa.alcance = 'Otros medios');

insert into autorizacion_alcance (autorizacion_id, alcance, otorgado)
select a.id, 'Apoyo socioemocional', true
from autorizacion a
where a.tipo = 'CONVIVENCIA' and a.estado = 'OTORGADA'
  and not exists (select 1 from autorizacion_alcance aa
                  where aa.autorizacion_id = a.id and aa.alcance = 'Apoyo socioemocional');

insert into autorizacion_alcance (autorizacion_id, alcance, otorgado)
select a.id, 'Mediación escolar', true
from autorizacion a
where a.tipo = 'CONVIVENCIA' and a.estado = 'OTORGADA'
  and not exists (select 1 from autorizacion_alcance aa
                  where aa.autorizacion_id = a.id and aa.alcance = 'Mediación escolar');

insert into autorizacion_alcance (autorizacion_id, alcance, otorgado)
select a.id, 'Intervención apoyo PIE', (a.estudiante_id % 2) = 1
from autorizacion a
where a.tipo = 'CONVIVENCIA' and a.estado = 'OTORGADA'
  and not exists (select 1 from autorizacion_alcance aa
                  where aa.autorizacion_id = a.id and aa.alcance = 'Intervención apoyo PIE');

-- ---------------------------------------------------------------------
-- 6. PRESCRIPCIÓN Y ADMINISTRACIÓN DE MEDICAMENTOS (caso asma)
-- ---------------------------------------------------------------------
insert into prescripcion (estudiante_id, autorizacion_id, tipo_autorizacion, medicamento,
                          dosis_cantidad, dosis_unidad, via, frecuencia, horario,
                          precauciones, medico_nombre, orden_documento_id,
                          vigente_desde, vigente_hasta, estado,
                          validada_por, validada_en, tens_responsable_id)
select
    a.estudiante_id,
    a.id,
    'MEDICACION',
    'Salbutamol inhalador (aerosol)',
    2.0,
    'puff',
    'Inhalación',
    'Cada 8 horas cuando presente crisis',
    'Entre 08:00 y 16:00 según necesidad informada por la familia.',
    'Verificar técnica de inhalación y registrar incidencias.',
    'Dra. Fernanda Oyarzún (datos de prueba)',
    (select d.id from documento d
      where d.estudiante_id = a.estudiante_id and d.tipo = 'ORDEN_MEDICA' limit 1),
    date '2026-03-02',
    date '2027-12-31',
    'VIGENTE',
    (select id from funcionario where correo = 'tens.salgado@mpaz.cl'),
    '2026-03-03 16:30-03'::timestamptz,
    (select id from funcionario where correo = 'tens.salgado@mpaz.cl')
from autorizacion a
where a.tipo = 'MEDICACION' and a.estado = 'OTORGADA'
  and not exists (select 1 from prescripcion p where p.autorizacion_id = a.id);

-- Dos administraciones registradas por la TENS (el trigger valida el ciclo).
insert into administracion_medicamento (prescripcion_id, administrado_en,
                                        dosis_cantidad, dosis_unidad, tens_id,
                                        incidencias, observaciones)
select p.id, v.cuando::timestamptz, 2.0, 'puff',
       (select id from funcionario where correo = 'tens.salgado@mpaz.cl'),
       v.incidencia, v.nota
from prescripcion p
cross join (values
    ('2026-04-15 10:30:00-03', null,                    'Exposición a polvo en el patio provocó tos; sin complicaciones.'),
    ('2026-04-22 10:15:00-03', 'Tos persistente',       'Deportivo; respuesta favorable a la intervención. Se avisó a la familia.')
) as v(cuando, incidencia, nota)
where p.estado = 'VIGENTE'
  and not exists (select 1 from administracion_medicamento am
                  where am.prescripcion_id = p.id
                    and am.administrado_en = v.cuando::timestamptz);

-- ---------------------------------------------------------------------
-- 7. REQUISITOS DOCUMENTALES de las matrículas 2027
-- ---------------------------------------------------------------------
insert into requisito_documental (matricula_id, tipo_documento, estado, documento_id,
                                  observacion, registrado_por)
select m.id, 'CEDULA_IDENTIDAD',
       case when d.id is null then 'PENDIENTE' else 'RECIBIDO' end, d.id, null,
       (select id from funcionario where correo = 'mteresa.lagos@mpaz.cl')
from matricula_anual m
left join documento d on d.estudiante_id = m.estudiante_id and d.tipo = 'CEDULA_IDENTIDAD'
where m.anio = 2027
on conflict (matricula_id, tipo_documento) do nothing;

insert into requisito_documental (matricula_id, tipo_documento, estado, documento_id,
                                  observacion, registrado_por)
select m.id, 'CERTIFICADO_NACIMIENTO',
       case when d.id is null then 'PENDIENTE' else 'RECIBIDO' end, d.id, null,
       (select id from funcionario where correo = 'mteresa.lagos@mpaz.cl')
from matricula_anual m
left join documento d on d.estudiante_id = m.estudiante_id and d.tipo = 'CERTIFICADO_NACIMIENTO'
where m.anio = 2027
on conflict (matricula_id, tipo_documento) do nothing;

insert into requisito_documental (matricula_id, tipo_documento, estado, documento_id,
                                  observacion, registrado_por)
select m.id, 'PODER_TERCERO', 'NO_APLICA', null,
       'El apoderado es la madre o el padre; no se requiere poder.',
       (select id from funcionario where correo = 'mteresa.lagos@mpaz.cl')
from matricula_anual m
where m.anio = 2027
on conflict (matricula_id, tipo_documento) do nothing;

insert into requisito_documental (matricula_id, tipo_documento, estado, documento_id,
                                  observacion, registrado_por)
select m.id, 'ORDEN_MEDICA',
       case when d.id is null then 'NO_APLICA' else 'RECIBIDO' end, d.id,
       case when d.id is null then null else 'Vigente hasta 2027-12-31 (datos de prueba).' end,
       (select id from funcionario where correo = 'tens.salgado@mpaz.cl')
from matricula_anual m
left join documento d on d.estudiante_id = m.estudiante_id and d.tipo = 'ORDEN_MEDICA'
where m.anio = 2027
on conflict (matricula_id, tipo_documento) do nothing;

insert into requisito_documental (matricula_id, tipo_documento, estado, documento_id,
                                  observacion, registrado_por)
select m.id, 'RESPALDO_SALUD',
       case when d.id is null then 'NO_APLICA' else 'RECIBIDO' end, d.id,
       case when d.id is null then null else 'Informe médico adjunto (datos de prueba).' end,
       (select id from funcionario where correo = 'tens.salgado@mpaz.cl')
from matricula_anual m
left join documento d on d.estudiante_id = m.estudiante_id and d.tipo = 'RESPALDO_SALUD'
where m.anio = 2027
on conflict (matricula_id, tipo_documento) do nothing;

insert into requisito_documental (matricula_id, tipo_documento, estado, documento_id,
                                  observacion, registrado_por)
select m.id, 'CERTIFICADO_PIE',
       case when d.id is null then 'NO_APLICA' else 'RECIBIDO' end, d.id,
       case when d.id is null then null else 'Informe de especialista (datos de prueba).' end,
       (select id from funcionario where correo = 'pie.mena@mpaz.cl')
from matricula_anual m
left join documento d on d.estudiante_id = m.estudiante_id and d.tipo = 'CERTIFICADO_PIE'
where m.anio = 2027
on conflict (matricula_id, tipo_documento) do nothing;

insert into requisito_documental (matricula_id, tipo_documento, estado, documento_id,
                                  observacion, registrado_por)
select m.id, 'DOCUMENTO_JUDICIAL',
       case when d.id is null then 'PENDIENTE' else 'RECIBIDO' end, d.id,
       case when d.id is null then null
            else 'Resolución vigente informada por el apoderado (datos de prueba).' end,
       (select id from funcionario where correo = 'inspectoria.mendoza@mpaz.cl')
from matricula_anual m
left join documento d on d.estudiante_id = m.estudiante_id and d.tipo = 'DOCUMENTO_JUDICIAL'
where m.anio = 2027
  and (d.id is not null
       or exists (select 1 from restriccion_familiar rf
                  join estudiante_persona ep on ep.id = rf.estudiante_persona_id
                  where ep.estudiante_id = m.estudiante_id))
on conflict (matricula_id, tipo_documento) do nothing;

-- Enlazar la resolución judicial y quién registró la restricción del caso B.
update restriccion_familiar r
set documento_id = (select d.id from documento d
                    join estudiante_persona ep on ep.estudiante_id = d.estudiante_id
                    where ep.id = r.estudiante_persona_id
                      and d.tipo = 'DOCUMENTO_JUDICIAL' limit 1),
    registrada_por = (select id from funcionario where correo = 'inspectoria.mendoza@mpaz.cl')
where r.documento_id is null
  and exists (select 1 from documento d where d.tipo = 'DOCUMENTO_JUDICIAL');

-- ---------------------------------------------------------------------
-- 8. AUDITORÍA DE ACCESOS (muestra ficticia)
-- ---------------------------------------------------------------------
insert into registros_acceso (funcionario_id, accion, area, recurso, recurso_id,
                              estudiante_id, detalle, ip)
select
    (select id from funcionario where correo = 'mteresa.lagos@mpaz.cl'),
    v.accion, v.area, v.recurso, null, e.id,
    jsonb_build_object('descripcion', v.detalle), '127.0.0.1'::inet
from estudiante e
cross join (values
    ('LEER',      'GENERAL',    'ficha',            'Revisión de la ficha del estudiante.'),
    ('LEER',      'SALUD',      'situacion_medica', 'Consulta de situaciones médicas.'),
    ('DESCARGAR', 'DOCUMENTOS', 'documento',        'Descarga del PDF de la ficha 2026.'),
    ('BUSCAR',    'GENERAL',    'estudiante',       'Búsqueda por RUN del apoderado.')
) as v(accion, area, recurso, detalle)
where e.identificador in ('20114567-8','21456789-K','22334455-0','23001122-2','24455667-1')
  and not exists (select 1 from registros_acceso r
                  where r.funcionario_id = (select id from funcionario where correo = 'mteresa.lagos@mpaz.cl')
                    and r.accion = v.accion
                    and r.estudiante_id = e.id
                    and r.recurso = v.recurso);

-- Un intento denegado (docente consultando el área SALUD).
insert into registros_acceso (funcionario_id, accion, area, recurso, estudiante_id, detalle, ip)
select (select id from funcionario where correo = 'docente.ramirez@mpaz.cl'),
       'DENEGADO', 'SALUD', 'situacion_medica', e.id,
       jsonb_build_object('motivo', 'El rol DOCENTE no tiene permiso sobre el área SALUD.'),
       '127.0.0.1'::inet
from estudiante e
where e.identificador = normalizar_rut('25.123.456-6')
  and not exists (select 1 from registros_acceso r
                  where r.accion = 'DENEGADO' and r.estudiante_id = e.id);

commit;

-- =====================================================================
-- COMPROBACIÓN: conteo por tabla cargada por este script
-- =====================================================================
select 'funcionario' tabla,            count(*) filas from funcionario
union all select 'funcionario_rol',            count(*) from funcionario_rol
union all select 'rol_permiso',                count(*) from rol_permiso
union all select 'plantilla_autorizacion',     count(*) from plantilla_autorizacion
union all select 'documento',                  count(*) from documento
union all select 'ficha_version',              count(*) from ficha_version
union all select 'folio_secuencia',            count(*) from folio_secuencia
union all select 'autorizacion',               count(*) from autorizacion
union all select 'autorizacion_alcance',       count(*) from autorizacion_alcance
union all select 'prescripcion',               count(*) from prescripcion
union all select 'administracion_medicamento', count(*) from administracion_medicamento
union all select 'requisito_documental',       count(*) from requisito_documental
union all select 'registros_acceso',           count(*) from registros_acceso
order by 1;
