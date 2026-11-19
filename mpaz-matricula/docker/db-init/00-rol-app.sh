#!/bin/bash
# Entrega final · Portal de Matrícula MPAZ
#
# Crea el rol de la aplicación con la clave de .env. Antes estaba en un .sql con
# una contraseña fija: rotarla en .env rompía la conexión sin explicación.
#
# Se ejecuta sólo en el primer arranque (volumen de datos vacío). Si más adelante
# se cambia CLAVE_APP, el rol conserva la anterior: actualícela con
#   ALTER ROLE mpaz_app PASSWORD 'nueva';
# o recree el volumen (perdiendo los datos).

set -euo pipefail

: "${CLAVE_APP:?Defina CLAVE_APP en .env}"
: "${POSTGRES_USER:?Falta POSTGRES_USER}"
: "${POSTGRES_DB:?Falta POSTGRES_DB}"

psql -v ON_ERROR_STOP=1 \
     --username "$POSTGRES_USER" \
     --dbname "$POSTGRES_DB" \
     -v clave="$CLAVE_APP" \
     -v base="$POSTGRES_DB" <<'SQL'
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mpaz_app') THEN
        CREATE ROLE mpaz_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE;
    END IF;
END
$$;

ALTER ROLE mpaz_app WITH LOGIN PASSWORD :'clave';

GRANT CONNECT ON DATABASE :"base" TO mpaz_app;
REVOKE CONNECT ON DATABASE :"base" FROM PUBLIC;

ALTER ROLE mpaz_app IN DATABASE :"base" SET search_path = mpaz_matricula, public;
ALTER DATABASE :"base" SET search_path = mpaz_matricula, public;
SQL
