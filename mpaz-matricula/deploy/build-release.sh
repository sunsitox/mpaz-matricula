#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT="$ROOT/mpaz-matricula"
ARTIFACTS="$ROOT/artifacts"
STAGE="$ARTIFACTS/mpaz-matriculas-linux-x64"
RELEASE_NAME="mpaz-matriculas-$(date -u +%Y%m%dT%H%M%SZ)-linux-x64"
ARCHIVE="$ARTIFACTS/$RELEASE_NAME.tar.gz"

rm -rf "$STAGE"
mkdir -p "$STAGE" "$STAGE/database"

dotnet restore "$PROJECT/MarcelaPaz.Web/MarcelaPaz.Web.csproj" -r linux-x64
dotnet publish "$PROJECT/MarcelaPaz.Web/MarcelaPaz.Web.csproj" \
  -c Release -r linux-x64 --self-contained true --no-restore \
  -o "$STAGE/app"

dotnet tool restore --tool-manifest "$PROJECT/dotnet-tools.json"
dotnet ef migrations bundle \
  --project "$PROJECT/MarcelaPaz.Infrastructure" \
  --startup-project "$PROJECT/MarcelaPaz.Web" \
  --configuration Release --self-contained -r linux-x64 --force \
  -o "$STAGE/efbundle"

rm -f \
  "$STAGE/app/appsettings.Development.json" \
  "$STAGE/app/"*.pdb

cp "$PROJECT/database/"*.sql "$STAGE/database/"
cp "$PROJECT/database/aplicar.sh" "$STAGE/database/"
cp "$PROJECT/database/DESPLIEGUE.md" "$STAGE/database/"
cp "$PROJECT/deploy/mpaz-matriculas.service" "$STAGE/"
cp "$PROJECT/deploy/mpaz-matriculas.env.example" "$STAGE/"

if grep -RIlE 'Password=postgresql|Mpaz2027segura' "$STAGE" >/dev/null; then
  echo "ERROR: el artefacto contiene una credencial de desarrollo conocida." >&2
  exit 1
fi

if find "$STAGE" -type f \( -iname '*mock*' -o -iname '*credencial*' \) -print -quit | grep -q .; then
  echo "ERROR: el artefacto contiene datos o credenciales de prueba." >&2
  exit 1
fi

tar -C "$ARTIFACTS" -czf "$ARCHIVE" "$(basename "$STAGE")"
(
  cd "$ARTIFACTS"
  sha256sum "$(basename "$ARCHIVE")" > "$(basename "$ARCHIVE").sha256"
)

printf 'Release: %s\nChecksum: %s\n' "$ARCHIVE" "$ARCHIVE.sha256"
