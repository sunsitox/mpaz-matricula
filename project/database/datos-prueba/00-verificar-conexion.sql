-- Comprobación de conexión y esquema · Portal de Matrícula 2027
-- No modifica datos: sólo consulta.
--
-- Uso en el servidor del colegio:
--   psql "postgresql://USUARIO:CLAVE@localhost:5432/matricula_2027" -f 00-verificar-conexion.sql
--
-- Desde el PC de desarrollo, sin psql instalado, sirve igual:
--   dotnet ef database update --project MarcelaPaz.Infrastructure --startup-project MarcelaPaz.Web

\echo '== Servidor =='
select version();

\echo '== Base, usuario y zona horaria =='
select current_database() as base,
       current_user      as usuario,
       now()             as momento,
       current_setting('TimeZone') as zona;

\echo '== Tablas del esquema public =='
select table_name
from information_schema.tables
where table_schema = 'public'
  and table_type = 'BASE TABLE'
order by table_name;

\echo '== Conteo de filas por tabla (las que existan) =='
do $$
declare
    t      record;
    total  bigint;
begin
    for t in
        select table_name
        from information_schema.tables
        where table_schema = 'public' and table_type = 'BASE TABLE'
        order by table_name
    loop
        execute format('select count(*) from public.%I', t.table_name) into total;
        raise notice '% : % filas', rpad(t.table_name, 32), total;
    end loop;
end
$$;

\echo '== Fin. Si no hay errores, la conexión y el esquema responden. =='
