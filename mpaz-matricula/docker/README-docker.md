# Entrega final con Docker · Portal de Matrícula MPAZ

Empaquetado reproducible: la aplicación corre en su propia imagen y PostgreSQL 16
se inicializa solo con el DDL institucional, el esquema `auth` de Identity, los
datos mock y los permisos mínimos del rol de aplicación.

## Requisito

Docker Engine 24+ con el plugin `compose` en el equipo donde se ejecutará:

```bash
docker --version
docker compose version
```

No se necesita .NET ni PostgreSQL instalados en el host.

## Puesta en marcha

```bash
cd mpaz-matricula/docker
cp .env.example .env
docker compose up -d --build
docker compose ps
```

La base se carga **solo la primera vez**, cuando el volumen `datos-db` está vacío.
Orden de carga: `00-rol-app.sql` → `01-ddl-institucional.sql` →
`02-identidad-auth.sql` → `03-datos-mock.sql` → `04-datos-gestion-expediente.sql`
→ `05-privilegios-app.sql`.

La aplicación queda en:

```text
http://127.0.0.1:5101
```

## Credenciales de prueba

| Elemento | Valor |
| --- | --- |
| Base | `mpaz_matricula` |
| Rol de la aplicación | `mpaz_app` / `mpaz_app_2027` |
| Superusuario PostgreSQL | `postgres` / `postgres_mpaz_2027` |
| Cuenta de funcionario | `mteresa.lagos@mpaz.cl` / `Mpaz2027segura` |

Todas se definen en `.env` y se pueden cambiar ahí. Si cambias `CLAVE_APP`,
actualiza también `db-init/00-rol-app.sql` antes del primer arranque.

## Exposición pública

El contenedor publica solo en loopback. Para Internet, apuntar el túnel de
Cloudflare a `http://127.0.0.1:5101` con Cloudflare Access delante.

El inicio de sesión usa cookie `Secure`, así que debe probarse por HTTPS
(dominio del colegio o túnel), no por `http://IP:5101`.

## Operación

```bash
# registros
docker compose logs -f app
docker compose logs -f db

# reiniciar solo la aplicación
docker compose restart app

# respaldo de la base
docker compose exec -T db pg_dump -U postgres -F c mpaz_matricula > respaldo.dump

# restaurar en una base limpia
docker compose exec -T db createdb -U postgres mpaz_matricula_restaurada
docker compose exec -T db pg_restore -U postgres -d mpaz_matricula_restaurada < respaldo.dump

# volver a sembrar desde cero (BORRA los datos del volumen)
docker compose down -v
docker compose up -d --build
```

## Contenido del release

```text
docker/
├── Dockerfile              imagen multi-stage (SDK 10 → runtime ASP.NET 10)
├── docker-compose.yml      app + PostgreSQL 16 con healthchecks
├── .env.example            credenciales visibles y editables
├── README-docker.md        este documento
└── db-init/                scripts cargados en el primer arranque
```

La aplicación **no** aplica migraciones al arrancar: el esquema queda
determinado por estos scripts, igual que en el servidor del colegio.

## Limitaciones conocidas

- Los datos mock son ficticios; sirven para revisar pantallas y flujos.
- El PDF necesita las fuentes del sistema; la imagen `aspnet:10.0` ya incluye
  fuentes DejaVu, pero conviene probar la generación dentro del contenedor.
- `Seguridad__ContrasenaInicial` existe solo para sembrar las cuentas mock;
  retirarla cuando el colegio cree sus propias cuentas.
