-- Pre-kínder y Kínder son pocos alumnos y el colegio los matricula juntos en
-- un solo curso, «Parvularia», que no lleva letra. Este paso crea ese curso y
-- mueve a él las matrículas que estaban en PK A, PK B, K A y K B. Idempotente:
-- se puede aplicar más de una vez.

insert into curso (nombre) values ('Parvularia')
on conflict (nombre) do nothing;

update matricula_anual m
set curso_id = destino.id
from curso origen
join curso destino on destino.nombre = 'Parvularia'
where m.curso_id = origen.id
  and origen.nombre in ('PK A', 'PK B', 'K A', 'K B');

-- Sin matrículas asociadas, los cursos por letra de parvularia salen del catálogo.
delete from curso c
where c.nombre in ('PK A', 'PK B', 'K A', 'K B')
  and not exists (select 1 from matricula_anual m where m.curso_id = c.id);

select 'Curso de parvularia' as paso, count(*) as cursos
from curso
where nombre = 'Parvularia';
