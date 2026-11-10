#!/usr/bin/env bash
# =====================================================================
# MPAZ Web · Aplicación ordenada del esquema
#
# Uso:
#   ./aplicar.sh <base> [--modulo NOMBRE] [opciones]
#
# Aplica primero los pasos comunes (comun/pasos) y después los pasos del
# módulo indicado (modulos/<nombre>/pasos), en orden numérico.
#
# Opciones:
#   --modulo NOMBRE    módulo a aplicar                 (por defecto matricula)
#   --host HOST        servidor PostgreSQL        (por defecto localhost)
#   --puerto PUERTO    puerto                     (por defecto 5432)
#   --usuario USUARIO  usuario administrador      (por defecto postgres)
#   --clave-app CLAVE  contraseña de mpaz_app     (sólo si hay que crearlo)
#   --con-permisos     aplica la matriz propuesta de permisos (03)
#   --sin-privilegios  no aplica los privilegios del rol (04)
#   --sin-identidad    omite las tablas de Identity (comun/02), por ejemplo si
#                      se aplican con el bundle de EF Core
#   --datos ARCHIVO    carga datos de prueba después del esquema
#   --migrar           base que YA existe: sólo corre el mantenimiento 90
#                      del módulo
#
# Ejemplos:
#   ./aplicar.sh mpaz_matricula --clave-app 'secreto' --con-permisos
#   ./aplicar.sh mpaz_verify --modulo matricula --clave-app 'secreto'
#   ./aplicar.sh mpaz_matricula --migrar
#
# La contraseña del administrador se toma de PGPASSWORD o de ~/.pgpass.
# =====================================================================
set -euo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PSQL="${PSQL:-psql}"

BASE=""
MODULO="matricula"
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
        --modulo)          MODULO="$2"; shift 2 ;;
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

R_MODULO="$AQUI/modulos/$MODULO"

if [[ ! -d "$R_MODULO/pasos" ]]; then
    echo "No existe el módulo '$MODULO' en modulos/." >&2
    exit 2
fi

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

echo "Base: $BASE ($HOST:$PUERTO, usuario $USUARIO) · módulo: $MODULO"

if [[ "$MIGRAR" -eq 1 ]]; then
    mantenimiento=$(ls "$R_MODULO"/mantenimiento/90-*.sql 2>/dev/null | head -1 || true)
    if [[ -z "$mantenimiento" ]]; then
        echo "El módulo '$MODULO' no tiene script de migración 90." >&2
        exit 2
    fi
    ejecutar "$mantenimiento"
    echo "Base existente migrada (módulo $MODULO)."
    exit 0
fi

if [[ -z "$CLAVE_APP" ]]; then
    echo "Aviso: sin --clave-app el rol mpaz_app se crea sin contraseña." >&2
fi

ejecutar "$AQUI/comun/pasos/00-rol-aplicacion.sql" -v clave_app="${CLAVE_APP:-}"
if [[ "$IDENTIDAD" -eq 1 ]]; then
    ejecutar "$AQUI/comun/pasos/02-identidad.sql"
fi

for paso in "$R_MODULO"/pasos/*.sql; do
    nombre=$(basename "$paso")
    # 00 y 02 viven en comun/; 03 requiere aprobación explícita.
    case "$nombre" in
        03-*) [[ "$PERMISOS" -eq 1 ]] || continue ;;
        *privilegio*) [[ "$PRIVILEGIOS" -eq 1 ]] || continue ;;
    esac
    # La validación de matrícula resuelve nombres sin calificar.
    if [[ "$nombre" == "06-"* ]]; then
        ejecutar "$paso" -c 'SET search_path TO mpaz_matricula, public'
    else
        ejecutar "$paso"
    fi
done

if [[ -n "$DATOS" ]]; then
    ejecutar "$DATOS"
fi

echo "Esquema aplicado en $BASE (módulo $MODULO)."
