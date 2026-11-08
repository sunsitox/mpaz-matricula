#!/usr/bin/env bash
# =====================================================================
# MPAZ Web · Aplicación ordenada del esquema
#
# Uso:
#   ./aplicar.sh <base> [opciones]
#
# Opciones:
#   --host HOST          servidor PostgreSQL        (por defecto localhost)
#   --puerto PUERTO      puerto                     (por defecto 5432)
#   --usuario USUARIO    usuario administrador      (por defecto postgres)
#   --clave-app CLAVE    contraseña de mpaz_app     (sólo si hay que crearlo)
#   --con-permisos       aplica la matriz propuesta de permisos (03)
#   --sin-privilegios    no aplica los privilegios del rol (04)
#   --datos ARCHIVO      carga datos de prueba después del esquema
#   --sin-identidad      omite las tablas de Identity (02), por ejemplo si
#                        se aplican con el bundle de EF Core
#   --migrar             base que YA existe: sólo la pasa a mpaz_matricula (90)
#
# Ejemplos:
#   ./aplicar.sh mpaz_matricula --clave-app 'secreto' --con-permisos
#   ./aplicar.sh mpaz_matricula --migrar
#
# La contraseña del administrador se toma de PGPASSWORD o de ~/.pgpass.
# =====================================================================
set -euo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PSQL="${PSQL:-psql}"

BASE=""
HOST="localhost"
PUERTO="5432"
USUARIO="postgres"
CLAVE_APP=""
PERMISOS=0
PRIVILEGIOS=1
IDENTIDAD=1
DATOS=""
MIGRAR=0

while [[ $# -gt 0 ]]; do
    case "$1" in
        --host)            HOST="$2"; shift 2 ;;
        --puerto)          PUERTO="$2"; shift 2 ;;
        --usuario)         USUARIO="$2"; shift 2 ;;
        --clave-app)       CLAVE_APP="$2"; shift 2 ;;
        --con-permisos)    PERMISOS=1; shift ;;
        --sin-privilegios) PRIVILEGIOS=0; shift ;;
        --sin-identidad)   IDENTIDAD=0; shift ;;
        --datos)           DATOS="$2"; shift 2 ;;
        --migrar)          MIGRAR=1; shift ;;
        -h|--help)
            sed -n '2,30p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        -*) echo "Opción desconocida: $1" >&2; exit 2 ;;
        *)
            if [[ -z "$BASE" ]]; then BASE="$1"; shift; else
                echo "Sobra el argumento: $1" >&2; exit 2
            fi ;;
    esac
done

if [[ -z "$BASE" ]]; then
    echo "Falta el nombre de la base. Use --help para ver el uso." >&2
    exit 2
fi

if ! command -v "$PSQL" >/dev/null 2>&1; then
    echo "No se encontró psql. Instale el cliente de PostgreSQL o defina PSQL." >&2
    exit 1
fi

ejecutar() {
    local archivo="$1"
    shift
    echo "→ $(basename "$archivo")"
    "$PSQL" -h "$HOST" -p "$PUERTO" -U "$USUARIO" -d "$BASE" \
        -v ON_ERROR_STOP=1 -v base="$BASE" "$@" -f "$archivo" -q
}

echo "Base: $BASE ($HOST:$PUERTO, usuario $USUARIO)"

if [[ "$MIGRAR" -eq 1 ]]; then
    ejecutar "$AQUI/90-migrar-bases-existentes.sql"
    echo "Base existente movida a mpaz_matricula."
    exit 0
fi

if [[ -z "$CLAVE_APP" ]]; then
    echo "Aviso: sin --clave-app el rol mpaz_app se crea sin contraseña." >&2
fi

ejecutar "$AQUI/00-rol-aplicacion.sql" -v clave_app="${CLAVE_APP:-}"
ejecutar "$AQUI/01-ddl-institucional.sql"
if [[ "$IDENTIDAD" -eq 1 ]]; then
    ejecutar "$AQUI/02-identidad.sql"
fi
if [[ "$PERMISOS" -eq 1 ]]; then
    ejecutar "$AQUI/03-permisos-iniciales.sql"
fi
if [[ "$PRIVILEGIOS" -eq 1 ]]; then
    ejecutar "$AQUI/04-privilegios-aplicacion.sql"
fi
# Año escolar configurable: debe correr después de 04 para quedar con privilegios.
ejecutar "$AQUI/05-anio-escolar.sql"
if [[ -n "$DATOS" ]]; then
    ejecutar "$DATOS"
fi

echo "Esquema aplicado en $BASE."
