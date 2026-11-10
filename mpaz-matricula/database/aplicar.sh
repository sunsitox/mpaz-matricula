#!/usr/bin/env bash
# =====================================================================
# MPAZ Web · Aplicación ordenada del esquema (una base, un esquema por módulo)
#
# Uso:
#   ./aplicar.sh <base> [--modulo NOMBRE]... [--todos] [opciones]
#
# Aplica primero los pasos comunes (comun/pasos), después los pasos de cada
# módulo indicado (modulos/<nombre>/pasos), en orden numérico, y al final
# las tablas de Identity (comun/02-identidad.sql).
#
# El orden importa: 02 corre ÚLTIMO porque otros pasos pre-sellan el
# historial de migraciones de EF y, si 02 va antes, no crea nada.
#
# Opciones:
#   --modulo NOMBRE    módulo a aplicar (repetible; por defecto matricula)
#   --todos            aplica todos los módulos con pasos/ (sin plantilla)
#   --host HOST        servidor PostgreSQL        (por defecto localhost)
#   --puerto PUERTO    puerto                     (por defecto 5432)
#   --usuario USUARIO  usuario administrador      (por defecto postgres)
#   --clave-app CLAVE  contraseña de mpaz_app     (sólo si hay que crearlo)
#   --con-permisos     aplica la matriz propuesta de permisos (03)
#   --sin-privilegios  no aplica los privilegios del rol (04)
#   --sin-identidad    omite las tablas de Identity (comun/02), por ejemplo si
#                      se aplican con el bundle de EF Core
#   --neon             adapta a Neon: omite el rol mpaz_app (00 y 04, Neon no
#                      permite CREATE ROLE por SQL) y deja el search_path por
#                      defecto en la base en vez del rol
#   --datos ARCHIVO    carga datos de prueba después del esquema
#
# Ejemplos:
#   ./aplicar.sh mpaz_matricula --clave-app 'secreto' --con-permisos
#   ./aplicar.sh mpaz --todos --neon            # base única con todo
#   ./aplicar.sh mpaz_verify --modulo matricula --clave-app 'secreto'
#   ./aplicar.sh mpaz --modulo asistencia --modulo notas --neon
#
# La contraseña del administrador se toma de PGPASSWORD o de ~/.pgpass.
# =====================================================================
set -euo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PSQL="${PSQL:-psql}"

BASE=""
MODULOS=()
TODOS=0
HOST="localhost"
PUERTO="5432"
USUARIO="postgres"
CLAVE_APP=""
PERMISOS=0
PRIVILEGIOS=1
IDENTIDAD=1
NEON=0
DATOS=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --modulo)          MODULOS+=("$2"); shift 2 ;;
        --todos)           TODOS=1; shift ;;
        --host)            HOST="$2"; shift 2 ;;
        --puerto)          PUERTO="$2"; shift 2 ;;
        --usuario)         USUARIO="$2"; shift 2 ;;
        --clave-app)       CLAVE_APP="$2"; shift 2 ;;
        --con-permisos)    PERMISOS=1; shift ;;
        --sin-privilegios) PRIVILEGIOS=0; shift ;;
        --sin-identidad)   IDENTIDAD=0; shift ;;
        --neon)            NEON=1; shift ;;
        --datos)           DATOS="$2"; shift 2 ;;
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

if [[ "$TODOS" -eq 1 ]]; then
    MODULOS=()
    for d in "$AQUI"/modulos/*/; do
        nombre=$(basename "$d")
        [[ "$nombre" == "_plantilla-modulo" ]] && continue
        [[ -d "$d/pasos" ]] || continue
        MODULOS+=("$nombre")
    done
fi

if [[ "${#MODULOS[@]}" -eq 0 ]]; then
    MODULOS=(matricula)
fi

for m in "${MODULOS[@]}"; do
    if [[ ! -d "$AQUI/modulos/$m/pasos" ]]; then
        echo "No existe el módulo '$m' en modulos/." >&2
        exit 2
    fi
done

if ! command -v "$PSQL" >/dev/null 2>&1; then
    echo "No se encontró psql. Instale el cliente de PostgreSQL o defina PSQL." >&2
    exit 1
fi

ejecutar() {
    local archivo="$1"
    shift
    echo "→ $(realpath --relative-to="$AQUI" "$archivo")"
    "$PSQL" -h "$HOST" -p "$PUERTO" -U "$USUARIO" -d "$BASE" \
        -v ON_ERROR_STOP=1 -v base="$BASE" "$@" -f "$archivo" -q
}

echo "Base: $BASE ($HOST:$PUERTO, usuario $USUARIO) · módulos: ${MODULOS[*]}"

if [[ "$NEON" -eq 0 ]]; then
    if [[ -z "$CLAVE_APP" ]]; then
        echo "Aviso: sin --clave-app el rol mpaz_app se crea sin contraseña." >&2
    fi
    ejecutar "$AQUI/comun/pasos/00-rol-aplicacion.sql" -v clave_app="${CLAVE_APP:-}"
else
    echo "Modo Neon: se omite el rol mpaz_app (00 y 04)."
    "$PSQL" -h "$HOST" -p "$PUERTO" -U "$USUARIO" -d "$BASE" \
        -v ON_ERROR_STOP=1 -q \
        -c "ALTER DATABASE \"$BASE\" SET search_path = mpaz_matricula, public;"
fi

for m in "${MODULOS[@]}"; do
    echo "Módulo: $m"
    for paso in "$AQUI/modulos/$m"/pasos/*.sql; do
        nombre=$(basename "$paso")
        # 03 requiere aprobación explícita.
        case "$nombre" in
            03-*) [[ "$PERMISOS" -eq 1 ]] || continue ;;
            *privilegio*) [[ "$PRIVILEGIOS" -eq 1 && "$NEON" -eq 0 ]] || continue ;;
        esac
        ejecutar "$paso"
    done
done

# Identity al final: otros pasos pre-sellan el historial de EF y, si este
# paso va antes, detecta el sello y no crea las tablas.
if [[ "$IDENTIDAD" -eq 1 ]]; then
    ejecutar "$AQUI/comun/pasos/02-identidad.sql"
fi

if [[ -n "$DATOS" ]]; then
    ejecutar "$DATOS"
fi

echo "Esquema aplicado en $BASE (módulos: ${MODULOS[*]})."
