-- El colegio va de pre-kínder a 8º básico. Los cursos de 1° a 8° ya existían;
-- este paso agrega los dos niveles de educación parvularia para que la ficha
-- pueda matricular en ellos. Idempotente: se puede aplicar más de una vez.

insert into curso (nombre) values
    ('PK A'),
    ('PK B'),
    ('K A'),
    ('K B')
on conflict (nombre) do nothing;

select 'Cursos de parvularia disponibles' as paso, count(*) as cursos
from curso
where nombre like 'PK %' or nombre like 'K %';
