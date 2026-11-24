#!/usr/bin/env bash
# Paquete de despliegue limpio del Portal de Matrícula MPAZ.
# - Publica en Release (framework-dependent, Linux x64).
# - Quita appsettings.Development.json, PDB y cualquier App_Data de prueba.
# - Crea el tarball y su SHA-256 en artifacts/deploy/.
# Uso (Git Bash): bash scripts/publicar-paquete.sh
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MARCA="$(date -u +%Y%m%dT%H%M%SZ)"
DESTINO="$RAIZ/artifacts/deploy/mpaz-matriculas-$MARCA-linux-x64"
TARBALL="$RAIZ/artifacts/deploy/mpaz-matriculas-$MARCA-linux-x64.tar.gz"
SUMA="$TARBALL.sha256"

mkdir -p "$RAIZ/artifacts/deploy"

dotnet publish "$RAIZ/mpaz-matricula/MarcelaPaz.Web/MarcelaPaz.Web.csproj" \
  -c Release --no-restore -o "$DESTINO" >/dev/null

# Limpieza: nada de desarrollo, símbolos ni datos locales en el paquete.
rm -f "$DESTINO/appsettings.Development.json"
find "$DESTINO" -name '*.pdb' -delete
find "$DESTINO" -name '*.xml' -path '*/App_Data/*' -delete
rm -rf "$DESTINO/App_Data"

tar -czf "$TARBALL" -C "$RAIZ/artifacts/deploy" "$(basename "$DESTINO")"

# sha256sum en Windows puede entregar la ruta con barras; normalizar a nombre.
( cd "$RAIZ/artifacts/deploy" && sha256sum "$(basename "$TARBALL")" > "$(basename "$SUMA")" )

echo "Paquete: $TARBALL"
echo "Suma:    $SUMA"
echo "Tamaño:  $(du -h "$TARBALL" | cut -f1)"
echo "Contenido (primeras 15 líneas):"
tar -tzf "$TARBALL" | head -15
