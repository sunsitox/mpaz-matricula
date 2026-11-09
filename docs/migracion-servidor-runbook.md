# Runbook de migración al servidor · Portal de Matrícula MPAZ

**Estado:** preparado localmente; no ejecutado en `mpaz-server`  
**Servidor objetivo:** Ubuntu 24.04 (`mpaz-server`)  
**Aplicación:** ASP.NET Core 10 self-contained `linux-x64`  
**Origen privado:** `http://127.0.0.1:5100`

Este runbook complementa [deployment.md](deployment.md). No autoriza por sí solo a modificar el servidor compartido ni Cloudflare.

## 1. Condiciones de entrada

No iniciar la ventana de migración hasta completar:

- [ ] [Colegio] Autorizar fecha, responsable y tiempo de indisponibilidad.
- [ ] [Equipo] Coordinar con el responsable del sistema RFID. No modificar nginx, Gunicorn, puerto 8000 ni sus respaldos.
- [ ] [Colegio] Confirmar hostname, por ejemplo `matriculas.<dominio>`.
- [ ] [Colegio] Confirmar administrador inicial y matriz de permisos aprobada.
- [ ] [Equipo] Consolidar en Git el traslado de `project/` a `mpaz-matricula/` y definir el commit/tag exacto del release.
- [ ] [José] Corregir o aceptar expresamente los pendientes funcionales del checklist antes de usar datos reales.
- [ ] [Colegio] Definir destino de respaldo externo cifrado.

> El artefacto puede instalarse para una prueba privada, pero la aplicación no debe publicarse ni recibir datos reales mientras estos puntos sigan abiertos.

## 2. Artefactos preparados

Generar desde la raíz del repositorio:

```bash
bash mpaz-matricula/deploy/build-release.sh
```

El script produce en `artifacts/`:

- `mpaz-matriculas-<UTC>-linux-x64.tar.gz`;
- el checksum `.sha256`;
- aplicación self-contained sin `appsettings.Development.json`, PDB, datos mock ni credenciales conocidas;
- `efbundle` para las tablas de ASP.NET Core Identity;
- `01-ddl-institucional.sql`, fuente de verdad del esquema `mpaz_matricula`;
- `90-migrar-bases-existentes.sql`, para mover una base existente a ese esquema;
- unidad systemd y ejemplo de variables.

No incluir `database/datos-prueba/` ni ejecutar `03-permisos-iniciales.sql` sin aprobación institucional.

## 3. Preflight de solo lectura

Ejecutar primero sin instalar ni reiniciar servicios:

```bash
hostnamectl
lsb_release -a
uname -m
uptime
free -h
df -h / /srv/mpaz-data
findmnt /srv/mpaz-data
systemctl is-active mpaz-rfid nginx cloudflared
systemctl status mpaz-rfid nginx cloudflared --no-pager
ss -lntp | grep -E ':(80|8000|5100|5432)\b' || true
command -v psql || true
command -v pg_dump || true
fc-list | head
```

Criterios de salida:

- arquitectura `x86_64`;
- `/srv/mpaz-data` montado y con espacio;
- RFID, nginx y cloudflared sin fallas;
- puerto 5100 libre;
- registrar versiones y salidas relevantes en `ficha-servidor-mpaz-server.md`.

Si `/srv/mpaz-data` no está montado, **detener el procedimiento**.

## 4. Respaldo previo

Antes de modificar el servidor:

1. Registrar estado de servicios y configuración existente.
2. Respaldar solo los archivos que se vayan a tocar.
3. Si PostgreSQL o una base previa ya existen, generar un dump antes de aplicar cualquier cambio:

```bash
sudo install -d -m 0700 /srv/mpaz-data/backups/matriculas
sudo -u postgres pg_dump --format=custom --file \
  /srv/mpaz-data/backups/matriculas/pre-migracion-$(date -u +%Y%m%dT%H%M%SZ).dump \
  matricula
```

Un dump en el mismo servidor no sustituye la copia externa cifrada.

## 5. Instalación base

En una ventana autorizada:

```bash
sudo apt update
sudo apt install postgresql postgresql-client fontconfig fonts-dejavu-core
```

No instalar SDK .NET, Node, Docker ni otro nginx. La publicación es self-contained.

Crear identidad y rutas aisladas:

```bash
sudo useradd --system --home /var/lib/mpaz-matriculas \
  --shell /usr/sbin/nologin mpaz-matriculas
sudo install -d -o mpaz-matriculas -g mpaz-matriculas -m 0750 \
  /opt/mpaz-matriculas/releases /var/lib/mpaz-matriculas/keys
sudo install -d -o root -g mpaz-matriculas -m 0750 /etc/mpaz-matriculas
sudo install -d -o postgres -g postgres -m 0700 \
  /srv/mpaz-data/backups/matriculas
```

No mover el directorio de datos de PostgreSQL durante la primera instalación. Ese cambio requiere un procedimiento y respaldo independientes.

## 6. Base de datos

Generar una contraseña aleatoria fuera del historial. No pegarla en documentación o chat.

Como administrador PostgreSQL:

```sql
CREATE ROLE mpaz_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT PASSWORD '<secreto>';
CREATE DATABASE mpaz_matricula OWNER mpaz_app ENCODING 'UTF8';
REVOKE CONNECT ON DATABASE mpaz_matricula FROM PUBLIC;
GRANT CONNECT ON DATABASE mpaz_matricula TO mpaz_app;
```

Aplicar primero el DDL institucional y después la migración de Identity, usando una conexión administrativa controlada:

```bash
sudo -u postgres psql --set ON_ERROR_STOP=on \
  --dbname mpaz_matricula \
  --file /opt/mpaz-matriculas/releases/<release>/database/01-ddl-institucional.sql

/opt/mpaz-matriculas/releases/<release>/efbundle \
  --connection 'Host=127.0.0.1;Port=5432;Database=mpaz_matricula;Username=<rol_migrador>;Password=<secreto>'
```

El DDL crea el esquema `mpaz_matricula` con todas las tablas institucionales; `efbundle` crea allí mismo las tablas de Identity y el historial de migraciones. No ejecutar la aplicación antes de ambos pasos.

Si la base ya existía con las tablas institucionales en `public` y las de Identity en `auth`, ejecutar antes de `efbundle`:

```bash
sudo -u postgres psql --set ON_ERROR_STOP=on \
  --dbname mpaz_matricula \
  --file /opt/mpaz-matriculas/releases/<release>/database/90-migrar-bases-existentes.sql
```

El script es idempotente, conserva los datos y deja todo en `mpaz_matricula`.

Aplicar y verificar los privilegios mínimos preparados:

```bash
sudo -u postgres psql --set ON_ERROR_STOP=on \
  --dbname mpaz_matricula \
  --file /opt/mpaz-matriculas/releases/<release>/database/04-privilegios-aplicacion.sql
```

La aplicación puede seleccionar e insertar/actualizar en `mpaz_matricula`, administrar sus tablas de Identity en el mismo esquema y no es propietaria de los objetos. No recibe `DELETE` general: la única excepción actual es `situacion_medica`, porque `RegistroFicha` reemplaza esas filas mediante `DELETE` y luego `INSERT`. El script revoca expresamente modificación de las tablas solo-inserción y termina con consultas `has_table_privilege` para comprobar el resultado.

## 7. Instalar un release

Copiar el `.tar.gz` y `.sha256` a una ubicación temporal mediante el canal SSH/Tailscale autorizado. Verificar antes de extraer:

```bash
sha256sum -c mpaz-matriculas-<UTC>-linux-x64.tar.gz.sha256
```

Instalar con un nombre inmutable:

```bash
release=$(date -u +%Y%m%dT%H%M%SZ)
sudo install -d -o mpaz-matriculas -g mpaz-matriculas -m 0750 \
  "/opt/mpaz-matriculas/releases/$release"
sudo tar -xzf mpaz-matriculas-<UTC>-linux-x64.tar.gz \
  --strip-components=1 -C "/opt/mpaz-matriculas/releases/$release"
sudo chown -R mpaz-matriculas:mpaz-matriculas \
  "/opt/mpaz-matriculas/releases/$release"
sudo chmod 0750 "/opt/mpaz-matriculas/releases/$release/app/MarcelaPaz.Web" \
  "/opt/mpaz-matriculas/releases/$release/efbundle"
```

El enlace debe apuntar al subdirectorio `app`:

```bash
sudo ln -sfn "/opt/mpaz-matriculas/releases/$release/app" \
  /opt/mpaz-matriculas/current
```

## 8. Secretos y systemd

Crear `/etc/mpaz-matriculas/mpaz-matriculas.env` desde el ejemplo, con permisos `0640`, propietario `root` y grupo `mpaz-matriculas`. Valores obligatorios:

- `ASPNETCORE_ENVIRONMENT=Production`;
- `ASPNETCORE_URLS=http://127.0.0.1:5100`;
- `AllowedHosts=<hostname exacto>`;
- `DataProtection__KeysPath=/var/lib/mpaz-matriculas/keys`;
- `Seguridad__ExigirInicioSesion=true`;
- `ConnectionStrings__Default=<cadena del rol mpaz_app>`.

`Seguridad__ContrasenaInicial` es temporal: usarla únicamente si se cargaron funcionarios sin cuenta Identity, verificar los accesos y retirarla del archivo inmediatamente después. La misma clave no debe compartirse entre funcionarios en producción; el administrador debe crear cuentas con claves individuales desde la aplicación.

Instalar la unidad:

```bash
sudo install -o root -g root -m 0644 \
  /opt/mpaz-matriculas/releases/<release>/mpaz-matriculas.service \
  /etc/systemd/system/mpaz-matriculas.service
sudo systemctl daemon-reload
sudo systemctl enable --now mpaz-matriculas
```

## 9. Validación privada antes de Cloudflare

```bash
systemctl status mpaz-matriculas --no-pager
journalctl -u mpaz-matriculas -n 100 --no-pager
ss -lntp | grep ':5100'
curl --fail --show-error --head http://127.0.0.1:5100/Cuenta/Login
curl --fail --show-error --head \
  -H 'Host: <hostname>' \
  -H 'X-Forwarded-Proto: https' \
  http://127.0.0.1:5100/Cuenta/Login
```

Comprobar además:

- el proceso escucha únicamente en `127.0.0.1:5100`;
- login obligatorio y rechazo de credenciales incorrectas;
- rol `ADMIN_TI` y denegación para un usuario sin permiso;
- creación/búsqueda de una ficha de prueba autorizada;
- generación y descarga del PDF con las fuentes instaladas;
- reinicio del servicio sin perder la sesión por rotación accidental de claves;
- ausencia de contraseñas, tokens y datos sensibles en `journalctl`;
- respaldo `pg_dump` y restauración en una base temporal.

No usar datos de estudiantes reales para la prueba de humo.

## 10. Cloudflare

Solo después de aprobar la validación privada:

1. Crear primero la aplicación Cloudflare Access con denegación por defecto.
2. Autorizar únicamente a funcionarios/grupos definidos.
3. Activar validación de Access en el túnel.
4. Crear el hostname hacia `http://localhost:5100`.
5. Probar HTTPS, redirecciones, cookies seguras, login, permisos y logs desde el hostname real.

No abrir el puerto 5100 en UFW y no modificar nginx para esta primera publicación.

## 11. Reversa

### Falla de aplicación

```bash
sudo systemctl stop mpaz-matriculas
sudo ln -sfn /opt/mpaz-matriculas/releases/<release-anterior>/app \
  /opt/mpaz-matriculas/current
sudo systemctl start mpaz-matriculas
```

Verificar estado, logs y HTTP local. Las claves Data Protection quedan fuera del release y se conservan.

### Falla de publicación

Desactivar o retirar primero el hostname de Cloudflare Tunnel/Access. Esto no afecta RFID ni nginx.

### Falla de base de datos

No ejecutar automáticamente una migración `Down`. Detener la aplicación, conservar el dump de la base fallida y restaurar el dump previo en una base nueva o vaciada durante una ventana aprobada. Confirmar integridad antes de volver a enlazar la aplicación.

## 12. Criterio de cierre

La migración se considera terminada únicamente cuando:

- [ ] servicio estable tras reinicio;
- [ ] aplicación solo en loopback;
- [ ] DDL y migración `auth` aplicados;
- [ ] cuenta administradora individual verificada;
- [ ] permisos aprobados y comprobados;
- [ ] prueba de PDF completada;
- [ ] respaldo y restauración temporal completados;
- [ ] RFID, nginx y cloudflared existentes siguen saludables;
- [ ] Cloudflare Access protege el hostname antes de exponerlo;
- [ ] inventario del servidor y registro del release actualizados.
