-- El colegio tiene dos cursos por nivel: A y B, de pre-kínder a 8º básico.
-- Este paso completa el catálogo (20 cursos) y retira la letra C que traían
-- los datos de ejemplo, moviendo antes sus matrículas a la letra B del mismo
-- nivel. Idempotente: se puede aplicar más de una vez.

insert into curso (nombre)
select niveles.nivel || ' ' || letras.letra
from (values ('PK'), ('K'), ('1°'), ('2°'), ('3°'), ('4°'), ('5°'), ('6°'), ('7°'), ('8°')) as niveles(nivel)
cross join (values ('A'), ('B')) as letras(letra)
on conflict (nombre) do nothing;

-- Las matrículas de 4° C y 5° C pasan a la letra B del mismo nivel.
update matricula_anual m
set curso_id = destino.id
from curso origen
join curso destino on destino.nombre = replace(origen.nombre, ' C', ' B')
where m.curso_id = origen.id
  and origen.nombre like '% C';

-- Ya sin matrículas asociadas, la letra C sale del catálogo.
delete from curso c
where c.nombre like '% C'
  and not exists (select 1 from matricula_anual m where m.curso_id = c.id);

select 'Cursos del colegio' as paso, count(*) as cursos
from curso;
