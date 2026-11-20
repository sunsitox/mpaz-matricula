-- =====================================================================
-- MPAZ · Paso 23 — Pre-kínder y Kínder como cursos separados, sin letra
--
-- Decisión del colegio (20-11-2026): «Parvularia» era un nombre genérico
-- para generalizar; se deja como Pre-kínder y Kínder, dos cursos neutros
-- (sin letra A/B, que queda reservada de 1º a 8º básico).
--
-- Reemplaza al curso único «Parvularia» del paso 18. Las matrículas que
-- estuvieran en «Parvularia» se mueven a «Pre-kínder» (no se pierde el
-- vínculo; en dev eran 0). Idempotente.
-- =====================================================================

insert into curso (nombre) values ('Pre-kínder'), ('Kínder')
on conflict (nombre) do nothing;

update matricula_anual m
set curso_id = destino.id
from curso origen
join curso destino on destino.nombre = 'Pre-kínder'
where m.curso_id = origen.id
  and origen.nombre = 'Parvularia';

delete from curso c
where c.nombre = 'Parvularia'
  and not exists (select 1 from matricula_anual m where m.curso_id = c.id);

select nombre from curso where nombre in ('Pre-kínder', 'Kínder', 'Parvularia')
order by nombre;
