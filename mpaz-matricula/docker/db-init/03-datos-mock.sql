SET search_path TO mpaz_matricula, public;

-- =====================================================================
-- Alumnos de prueba · Portal de Matrícula 2027
-- Generado desde alumnos-mockup-2027.json (10 casos ficticios).
--
-- Sólo para bases de desarrollo o demostración: nunca en producción.
--
-- Uso (con el DDL aplicado en el esquema mpaz_matricula):
--   psql -d <base> -v ON_ERROR_STOP=1 -f "datos mock.sql"
--
-- Idempotente: se puede correr dos veces sin duplicar estudiantes, personas
-- ni matrículas. Todo ocurre en una transacción. Requiere las funciones
-- normalizar_rut y rut_valido del DDL.
-- =====================================================================

begin;

-- La ficha no pide comuna, pero el esquema la admite.
insert into comuna (nombre) values ('San Bernardo') on conflict (nombre) do nothing;

-- Cursos usados por los casos de prueba.
insert into curso (nombre) values
    ('1° A'),
    ('2° A'),
    ('2° B'),
    ('3° A'),
    ('3° B'),
    ('4° A'),
    ('4° C'),
    ('5° A'),
    ('5° C'),
    ('6° A'),
    ('6° B'),
    ('7° A'),
    ('7° B'),
    ('8° A'),
    ('8° B')
on conflict (nombre) do nothing;

-- 1. Estudiantes (Bloque A). El RUN se normaliza con la función del DDL.
insert into estudiante (tipo_identificador, identificador, nombres, apellido_paterno,
                        apellido_materno, fecha_nacimiento, nacionalidad, domicilio,
                        comuna_id, sale_solo_final_jornada)
select 'RUN', normalizar_rut(v.run), v.nombres, v.ap_pat, v.ap_mat, v.nac::date,
       v.nacionalidad, v.domicilio, (select id from comuna where nombre = 'San Bernardo'),
       v.sale_solo
from (values
    ('20.114.567-8', 'Martina Ignacia', 'Aravena', 'Cortes', '2019-04-18', 'Chilena', 'Los Aromos 1234, Villa Los Aromos', FALSE),
    ('21.456.789-K', 'Benjamín Alonso', 'Fuentes', 'Riquelme', '2018-09-02', 'Chilena', 'Pasaje El Roble 892, Población San José', FALSE),
    ('22.334.455-0', 'Emilia Paz', 'Sandoval', 'Muñoz', '2017-11-27', 'Chilena', 'Calle Freire 2210, Villa Las Acacias', FALSE),
    ('23.001.122-2', 'Tomás Vicente', 'Sepúlveda', 'Lagos', '2016-06-14', 'Chilena', 'Av. Ochagavía 1455, Block 12 Depto 41', TRUE),
    ('24.455.667-1', 'Antonia Belén', 'Contreras', 'Silva', '2015-02-09', 'Chilena', 'Los Nogales 567, Población México', FALSE),
    ('25.123.456-6', 'Matías Ignacio', 'Vergara', 'Pizarro', '2014-12-03', 'Chilena', 'Pasaje Los Tilos 318, Villa El Bosque', TRUE),
    ('23.667.788-5', 'Javiera Esperanza', 'Núñez', 'Cáceres', '2013-08-21', 'Chilena', 'Calle Balmaceda 1890, Depto 12', FALSE),
    ('24.778.899-9', 'Vicente Tomás', 'Escobar', 'Fuentes', '2020-10-05', 'Chilena', 'Pasaje Las Lilas 77, Villa Los Jardines', FALSE),
    ('22.998.877-8', 'Catalina Ignacia', 'Rojas', 'Herrera', '2013-03-30', 'Chilena', 'Av. Santa Rosa 7321, Casa 4', FALSE),
    ('25.889.900-8', 'Agustín Alonso', 'Morales', 'Tapia', '2018-01-16', 'Chilena', 'SIN INFORMAR', NULL)
) as v(run, nombres, ap_pat, ap_mat, nac, nacionalidad, domicilio, sale_solo)
on conflict (tipo_identificador, identificador) do nothing;

-- 2. Personas adultas (madre, padre, apoderados), sin duplicar por RUN.
insert into persona (identificador, nombres, apellidos, telefono, correo, domicilio, escolaridad)
select normalizar_rut(v.run), v.nombres, v.apellidos, v.telefono, v.correo, v.domicilio, v.escolaridad
from (values
    ('15.887.654-K', 'Carolina Andrea', 'Aravena Soto', '+56 9 6123 4567', 'carolina.aravena@correo.cl', 'Los Aromos 1234, Villa Los Aromos', 'Educación media'),
    ('16.754.321-9', 'Rodrigo Esteban', 'Cortes Mena', '+56 9 6234 5678', 'rodrigo.cortes@correo.cl', 'Los Aromos 1234, Villa Los Aromos', 'Educación media técnico-profesional'),
    ('17.234.567-0', 'Paola Ximena', 'Riquelme Díaz', '+56 9 6789 0123', 'paola.riquelme@correo.cl', 'Pasaje El Roble 892, Población San José', 'Educación universitaria'),
    ('18.456.789-K', 'Cristian Andrés', 'Fuentes Olivares', '+56 9 6890 1234', 'cristian.fuentes@correo.cl', 'Av. Colón 445, Depto 302', 'Educación media'),
    ('19.123.456-1', 'Daniela Soledad', 'Muñoz Pérez', '+56 9 6123 9876', 'daniela.munoz@correo.cl', 'Calle Freire 2210, Villa Las Acacias', 'Educación media'),
    ('14.567.890-0', 'Verónica del Carmen', 'Lagos Rivas', '+56 9 6345 1122', 'veronica.lagos@correo.cl', 'Av. Ochagavía 1455, Block 12 Depto 41', 'Técnico de nivel superior'),
    ('13.889.900-4', 'Héctor Manuel', 'Sepúlveda Ortiz', '+56 9 6456 2233', NULL, 'Av. Ochagavía 1455, Block 12 Depto 41', 'Educación básica'),
    ('15.223.344-2', 'Karen Alejandra', 'Silva Núñez', '+56 9 6111 2233', NULL, 'Pasaje Nuevo 45', 'Educación media'),
    ('16.987.654-1', 'Óscar Iván', 'Contreras Bustos', '+56 9 6222 3344', 'oscar.contreras@correo.cl', 'Los Nogales 567, Población México', 'Educación media técnico-profesional'),
    ('17.112.233-3', 'Mónica', 'Contreras Bustos', '+56 9 6333 4455', 'monica.contreras@correo.cl', NULL, NULL),
    ('18.665.544-3', 'Andrea Paz', 'Pizarro Rojas', '+56 9 6555 6677', 'andrea.pizarro@correo.cl', 'Pasaje Los Tilos 318, Villa El Bosque', 'Educación universitaria'),
    ('17.667.788-0', 'Luis Alberto', 'Vergara Soto', '+56 9 6666 7788', 'luis.vergara@correo.cl', 'Pasaje Los Tilos 318, Villa El Bosque', 'Educación media'),
    ('19.445.566-6', 'Claudia Marcela', 'Cáceres Vera', '+56 9 6888 9900', 'claudia.caceres@correo.cl', 'Calle Balmaceda 1890, Depto 12', 'Educación media'),
    ('20.556.677-5', 'Francisca Ignacia', 'Fuentes Soto', '+56 9 6999 1122', 'francisca.fuentes@correo.cl', 'Pasaje Las Lilas 77, Villa Los Jardines', 'Educación universitaria'),
    ('19.778.899-2', 'Nicolás', 'Escobar Rivas', '+56 9 6000 2233', NULL, 'Pasaje Las Lilas 77, Villa Los Jardines', 'Educación media'),
    ('16.665.544-7', 'Marisol', 'Herrera Peña', '+56 9 6111 3344', NULL, 'Av. Santa Rosa 7321, Casa 4', 'Educación básica'),
    ('15.556.677-9', 'Gonzalo', 'Rojas Medina', '+56 9 6222 4455', 'gonzalo.rojas@correo.cl', 'Av. Santa Rosa 7321, Casa 4', 'Educación media'),
    ('18.889.900-5', 'Rocío', 'Tapia Vega', '+56 9 6333 5566', NULL, NULL, NULL)
) as v(run, nombres, apellidos, telefono, correo, domicilio, escolaridad)
on conflict (tipo_identificador, identificador) do nothing;

-- 3. Relaciones estudiante–persona con vigencia (madre, padre, apoderados).
insert into estudiante_persona (estudiante_id, persona_id, parentesco, vinculo,
                                tipo_apoderado, tiene_restriccion)
select e.id, p.id, v.parentesco, v.vinculo, v.apoderado, v.restriccion
from (values
    ('20.114.567-8', '15.887.654-K', 'MADRE', NULL, 'PRINCIPAL', FALSE),
    ('20.114.567-8', '16.754.321-9', 'PADRE', NULL, 'SECUNDARIO', NULL),
    ('21.456.789-K', '17.234.567-0', 'MADRE', NULL, 'PRINCIPAL', FALSE),
    ('21.456.789-K', '18.456.789-K', 'PADRE', NULL, 'SECUNDARIO', NULL),
    ('22.334.455-0', '19.123.456-1', 'MADRE', NULL, 'PRINCIPAL', FALSE),
    ('23.001.122-2', '14.567.890-0', 'MADRE', NULL, 'PRINCIPAL', FALSE),
    ('23.001.122-2', '13.889.900-4', 'PADRE', NULL, NULL, NULL),
    ('24.455.667-1', '15.223.344-2', 'MADRE', NULL, NULL, TRUE),
    ('24.455.667-1', '16.987.654-1', 'PADRE', NULL, 'PRINCIPAL', NULL),
    ('24.455.667-1', '17.112.233-3', 'OTRO', 'tía', 'SECUNDARIO', NULL),
    ('25.123.456-6', '18.665.544-3', 'MADRE', NULL, 'PRINCIPAL', FALSE),
    ('25.123.456-6', '17.667.788-0', 'PADRE', NULL, 'SECUNDARIO', NULL),
    ('23.667.788-5', '19.445.566-6', 'MADRE', NULL, 'PRINCIPAL', FALSE),
    ('24.778.899-9', '20.556.677-5', 'MADRE', NULL, 'PRINCIPAL', FALSE),
    ('24.778.899-9', '19.778.899-2', 'PADRE', NULL, NULL, NULL),
    ('22.998.877-8', '16.665.544-7', 'MADRE', NULL, 'PRINCIPAL', FALSE),
    ('22.998.877-8', '15.556.677-9', 'PADRE', NULL, 'SECUNDARIO', NULL),
    ('25.889.900-8', '18.889.900-5', 'MADRE', NULL, 'PRINCIPAL', NULL)
) as v(est_run, per_run, parentesco, vinculo, apoderado, restriccion)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
join persona    p on p.identificador = normalizar_rut(v.per_run)
on conflict do nothing;

-- 4. Contactos de emergencia (Bloque C).
insert into contacto_emergencia (estudiante_id, orden, nombre, telefono, vinculo)
select e.id, v.orden, v.nombre, v.telefono, v.vinculo
from (values
    ('20.114.567-8', 1, 'Marta Soto Vergara', '+56 9 6345 6789', 'abuela'),
    ('20.114.567-8', 2, 'Felipe Aravena Soto', '+56 9 6456 7890', 'tío'),
    ('21.456.789-K', 1, 'Silvia Díaz Rojas', '+56 9 6901 2345', 'abuela'),
    ('21.456.789-K', 2, 'Marcela Fuentes Olivares', '+56 9 6012 3456', 'tía'),
    ('22.334.455-0', 1, 'Rosa Pérez Fuentes', '+56 9 6234 8765', 'abuela'),
    ('23.001.122-2', 1, 'Patricia Rivas Soto', '+56 9 6567 3344', 'abuela'),
    ('23.001.122-2', 2, 'Diego Sepúlveda Ortiz', '+56 9 6678 4455', 'tío'),
    ('24.455.667-1', 1, 'Mónica Contreras Bustos', '+56 9 6333 4455', 'tía'),
    ('24.455.667-1', 2, 'Iván Contreras Rojas', '+56 9 6444 5566', 'abuelo'),
    ('25.123.456-6', 1, 'Andrea Paz Pizarro Rojas', '+56 9 6555 6677', 'madre'),
    ('25.123.456-6', 2, 'Sergio Pizarro Rojas', '+56 9 6777 8899', 'tío'),
    ('23.667.788-5', 1, 'Claudia Marcela Cáceres Vera', '+56 9 6888 9900', 'madre'),
    ('22.998.877-8', 1, 'Gonzalo Rojas Medina', '+56 9 6222 4455', 'padre')
) as v(est_run, orden, nombre, telefono, vinculo)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
on conflict do nothing;

-- 5. Personas autorizadas a retirar (Bloque C).
insert into retiro_autorizado (estudiante_id, nombre, identificador, vinculo)
select e.id, v.nombre, normalizar_rut(v.run), v.vinculo
from (values
    ('20.114.567-8', 'Carolina Andrea Aravena Soto', '15.887.654-K', 'madre'),
    ('20.114.567-8', 'Rodrigo Esteban Cortes Mena', '16.754.321-9', 'padre'),
    ('20.114.567-8', 'Cristian Andrés Fuentes Olivares', '18.456.789-K', 'tía'),
    ('21.456.789-K', 'Paola Ximena Riquelme Díaz', '17.234.567-0', 'madre'),
    ('21.456.789-K', 'Cristian Andrés Fuentes Olivares', '18.456.789-K', 'padre'),
    ('22.334.455-0', 'Daniela Soledad Muñoz Pérez', '19.123.456-1', 'madre'),
    ('23.001.122-2', 'Verónica del Carmen Lagos Rivas', '14.567.890-0', 'madre'),
    ('23.001.122-2', 'Héctor Manuel Sepúlveda Ortiz', '13.889.900-4', 'padre'),
    ('24.455.667-1', 'Óscar Iván Contreras Bustos', '16.987.654-1', 'padre'),
    ('24.455.667-1', 'Mónica Contreras Bustos', '17.112.233-3', 'tía'),
    ('25.123.456-6', 'Andrea Paz Pizarro Rojas', '18.665.544-3', 'madre'),
    ('25.123.456-6', 'Luis Alberto Vergara Soto', '17.667.788-0', 'padre'),
    ('25.123.456-6', 'Abuela por identificar (12.998.877-0)', '12.998.877-0', 'abuela'),
    ('23.667.788-5', 'Claudia Marcela Cáceres Vera', '19.445.566-6', 'madre'),
    ('22.998.877-8', 'Marisol Herrera Peña', '16.665.544-7', 'madre'),
    ('22.998.877-8', 'Gonzalo Rojas Medina', '15.556.677-9', 'padre')
) as v(est_run, nombre, run, vinculo)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
on conflict do nothing;

-- 6. Matrícula anual: 2026 como historial y 2027 como borrador a renovar.
insert into matricula_anual (estudiante_id, anio, curso_id, tipo, estado, observaciones)
select e.id, v.anio, c.id, v.tipo, v.estado, v.obs
from (values
    ('20.114.567-8', 2026, '1° A', 'NUEVA', 'FIRMADA', NULL),
    ('20.114.567-8', 2027, '2° A', 'RENOVACION', 'BORRADOR', NULL),
    ('21.456.789-K', 2026, '2° B', 'NUEVA', 'FIRMADA', NULL),
    ('21.456.789-K', 2027, '3° B', 'RENOVACION', 'BORRADOR', NULL),
    ('22.334.455-0', 2026, '3° A', 'NUEVA', 'FIRMADA', NULL),
    ('22.334.455-0', 2027, '4° A', 'RENOVACION', 'BORRADOR', NULL),
    ('23.001.122-2', 2026, '4° C', 'NUEVA', 'FIRMADA', NULL),
    ('23.001.122-2', 2027, '5° C', 'RENOVACION', 'BORRADOR', NULL),
    ('24.455.667-1', 2026, '5° A', 'NUEVA', 'FIRMADA', NULL),
    ('24.455.667-1', 2027, '6° A', 'RENOVACION', 'BORRADOR', NULL),
    ('25.123.456-6', 2026, '6° B', 'NUEVA', 'FIRMADA', NULL),
    ('25.123.456-6', 2027, '7° B', 'RENOVACION', 'BORRADOR', NULL),
    ('23.667.788-5', 2026, '7° A', 'NUEVA', 'FIRMADA', NULL),
    ('23.667.788-5', 2027, '8° A', 'RENOVACION', 'BORRADOR', NULL),
    ('24.778.899-9', 2027, '1° A', 'NUEVA', 'BORRADOR', NULL),
    ('22.998.877-8', 2026, '8° B', 'NUEVA', 'FIRMADA', NULL),
    ('22.998.877-8', 2027, '8° B', 'RENOVACION', 'BORRADOR', 'Retirada durante 2026; reingresa en 2027.'),
    ('25.889.900-8', 2026, '2° A', 'NUEVA', 'FIRMADA', NULL),
    ('25.889.900-8', 2027, '3° A', 'RENOVACION', 'BORRADOR', NULL)
) as v(est_run, anio, curso, tipo, estado, obs)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
left join curso c on c.nombre = v.curso
on conflict do nothing;

-- 7. Salud (área SALUD): antecedente general y situaciones médicas.
insert into antecedente_salud (estudiante_id, tiene_situacion_medica, necesidades_atencion)
select e.id, v.tiene, v.detalle
from (values
    ('20.114.567-8', FALSE, NULL),
    ('21.456.789-K', FALSE, NULL),
    ('22.334.455-0', FALSE, NULL),
    ('23.001.122-2', FALSE, NULL),
    ('24.455.667-1', FALSE, NULL),
    ('25.123.456-6', TRUE, 'Usa inhalador de rescate; el colegio mantiene uno en secretaría por acuerdo con la familia.'),
    ('23.667.788-5', FALSE, NULL),
    ('24.778.899-9', FALSE, NULL),
    ('22.998.877-8', FALSE, 'Salida al baño autorizada por condición médica informada en 2025.'),
    ('25.889.900-8', NULL, NULL)
) as v(est_run, tiene, detalle)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
on conflict (estudiante_id) do nothing;

insert into situacion_medica (estudiante_id, tipo, descripcion, en_tratamiento)
select e.id, 'CONDICION', v.descripcion, v.tratamiento
from (values
    ('25.123.456-6', 'Asma bronquial — Crisis con esfuerzo físico y cambios de temperatura', TRUE)
) as v(est_run, descripcion, tratamiento)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
where not exists (select 1 from situacion_medica s
                  where s.estudiante_id = e.id and s.descripcion = v.descripcion);

-- 8. PIE y apoyos (área PIE).
insert into antecedente_apoyo (estudiante_id, participa_pie, apoyos_informados, tiene_diagnostico)
select e.id, v.participa, v.apoyos, v.diagnostico
from (values
    ('20.114.567-8', 'NO', NULL, NULL),
    ('21.456.789-K', 'NO', NULL, NULL),
    ('22.334.455-0', 'NO', NULL, NULL),
    ('23.001.122-2', 'NO', NULL, NULL),
    ('24.455.667-1', 'NO', NULL, NULL),
    ('25.123.456-6', 'NO', NULL, NULL),
    ('23.667.788-5', 'SI', 'Apoyo pedagógico en aula de recursos, dos veces por semana', TRUE),
    ('24.778.899-9', 'PENDIENTE', NULL, NULL),
    ('22.998.877-8', 'NO', NULL, NULL),
    ('25.889.900-8', 'PENDIENTE', NULL, NULL)
) as v(est_run, participa, apoyos, diagnostico)
join estudiante e on e.identificador = normalizar_rut(v.est_run)
on conflict (estudiante_id) do nothing;

-- 9. Restricción familiar/judicial (área JUDICIAL), sólo para el caso con restricción.
insert into restriccion_familiar (estudiante_persona_id, tipo, descripcion)
select ep.id, 'JUDICIAL', 'Restricción vigente informada por el apoderado: la madre no puede vincularse con la estudiante. Detalle en el expediente judicial.'
from estudiante_persona ep
join estudiante e on e.id = ep.estudiante_id
where e.identificador = normalizar_rut('24.455.667-1')
  and ep.parentesco = 'MADRE'
  and ep.vigente_hasta is null
  and not exists (select 1 from restriccion_familiar r where r.estudiante_persona_id = ep.id);

commit;

-- =====================================================================
-- Comprobación: estudiantes cargados con su curso 2027 y sus relaciones.
-- =====================================================================
select e.identificador as run,
       e.nombres || ' ' || e.apellido_paterno as estudiante,
       c.nombre as curso_2027, m.tipo, m.estado,
       (select count(*) from estudiante_persona ep
         where ep.estudiante_id = e.id and ep.vigente_hasta is null) as apoderados,
       (select count(*) from contacto_emergencia ce
         where ce.estudiante_id = e.id and ce.vigente_hasta is null) as emergencia,
       (select count(*) from retiro_autorizado ra
         where ra.estudiante_id = e.id and ra.vigente_hasta is null) as retiro
from estudiante e
left join matricula_anual m on m.estudiante_id = e.id and m.anio = 2027
left join curso c on c.id = m.curso_id
order by e.identificador;

-- Nota: el DDL bloquea DELETE en matrícula, ficha y documentos. Para anular
-- una matrícula de prueba se cambia su estado a ANULADA, no se borra.
