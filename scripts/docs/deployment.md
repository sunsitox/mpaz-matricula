# Plan de despliegue · Portal de Matrícula Marcela Paz

**Estado:** propuesta técnica local; no aplicada al servidor  
**Fecha:** 6 de noviembre de 2026  
**Servidor objetivo:** `mpaz-server` (Ubuntu 24.04)  
**Antecedente:** [ficha-servidor-mpaz-server.md](ficha-servidor-mpaz-server.md)

## 1. Decisión recomendada

Desplegar la aplicación ASP.NET Core en el servidor local del colegio, aislada del sistema RFID existente:

```text
Internet
  │ HTTPS
  ▼
Cloudflare (DNS/TLS y, para personal, Access)
  │ Cloudflare Tunnel existente
  ▼
http://127.0.0.1:5100
  │ Kestrel, servicio systemd propio
  ▼
Portal de Matrícula ── PostgreSQL en localhost:5432
```

La primera publicación debe usar un **subdominio exclusivo**, por ejemplo `matriculas.<dominio-del-colegio>`, y una ruta nueva del túnel que apunte a `http://localhost:5100`.

Esta opción evita modificar inicialmente el `server` comodín de nginx que hoy pertenece al sistema RFID. El puerto 5100 debe escuchar solo en loopback; no se debe abrir en UFW ni exponer Kestrel directamente a la red local o Internet.

## 2. Distribución propuesta en el servidor

| Elemento | Ruta o valor recomendado | Motivo |
| --- | --- | --- |
| Usuario de servicio | `mpaz-matriculas` sin inicio de sesión ni `sudo` | No compartir la cuenta administradora ni el usuario del RFID |
| Versiones publicadas | `/opt/mpaz-matriculas/releases/<fecha>/` | Separar binarios de datos y permitir reversión |
| Versión activa | `/opt/mpaz-matriculas/current` (enlace a un release) | Cambio de versión controlado |
| Configuración/secretos | `/etc/mpaz-matriculas/` con acceso restringido | La cadena PostgreSQL no debe quedar en el repositorio ni en el release |
| Claves de Data Protection | `/var/lib/mpaz-matriculas/keys/` | Mantener válidas cookies y tokens tras reinicios |
| Logs | `journald`, identificador propio de systemd | Operación y auditoría separadas del RFID |
| Kestrel | `http://127.0.0.1:5100` | Origen privado para cloudflared; no requiere puerto entrante |
| Base de datos | PostgreSQL en `127.0.0.1:5432`, base y rol exclusivos | No exponer PostgreSQL a la red |
| Datos PostgreSQL | En `/srv/mpaz-data`, solo después de asegurar el montaje | Aprovechar el disco de datos sin escribir accidentalmente en el SSD |
| Respaldos locales | `/srv/mpaz-data/backups/matriculas/` | Separarlos de los respaldos RFID |
| Copia externa | Destino externo cifrado por definir | Un respaldo en el mismo PC no cubre falla o robo del equipo |

El servicio systemd debe depender del montaje de `/srv/mpaz-data` y de PostgreSQL, ejecutarse como `mpaz-matriculas`, usar `ASPNETCORE_ENVIRONMENT=Production`, definir `ASPNETCORE_URLS=http://127.0.0.1:5100` y cargar los secretos desde un archivo protegido. No se deben incluir contraseñas directamente en el archivo `.service` si puede evitase.

Se puede publicar como aplicación **self-contained para `linux-x64`**, evitando instalar el runtime global de .NET 10, o instalar el runtime compatible y usar una publicación framework-dependent. En ambos casos hay que validar primero el artefacto en Ubuntu.

## 3. Software que debe instalarse

La instalación debe hacerse solo durante una ventana autorizada y después de actualizar el inventario del servidor. No se ejecutaron estos comandos durante esta revisión.

### Obligatorio

| Componente | Paquete o forma de instalación | Uso |
| --- | --- | --- |
| PostgreSQL Server | `postgresql` desde los repositorios de Ubuntu 24.04 | Base de datos local de la aplicación |
| Cliente PostgreSQL | `postgresql-client` | Incluye `psql`, `pg_dump` y `pg_restore` para administración y respaldos |
| Fuentes y configuración tipográfica | `fontconfig` y `fonts-dejavu-core` | Fuentes básicas para los PDF generados con QuestPDF |
| Aplicación publicada | Artefacto `linux-x64` generado fuera del servidor | Ejecutables, ensamblados y archivos estáticos del portal |

La aplicación usa `net10.0`. Para ejecutarla se debe elegir **una** de estas modalidades:

- **Recomendada: publicación self-contained `linux-x64`.** El artefacto incluye el runtime de .NET 10 y no requiere instalar SDK ni runtime global. Ocupa más espacio, pero reduce dependencias y permite desplegar una versión comprobada.
- **Alternativa: publicación framework-dependent.** Instalar `aspnetcore-runtime-10.0` desde el repositorio oficial de paquetes de Microsoft para Ubuntu 24.04. El runtime de ASP.NET Core incluye el runtime base necesario; no hace falta instalar ambos por separado.

Antes de decidir, el artefacto debe probarse en Ubuntu 24.04 incluyendo generación y descarga de PDF. Las dependencias nativas del runtime deben provenir de paquetes firmados y mantenidos para esa versión de Ubuntu.

### Ya disponible o incluido en Ubuntu

- `systemd` y `journald`: forman parte del sistema; se usará un servicio propio para la aplicación.
- `cloudflared`: ya aparece instalado y activo en el mapeo; no debe reinstalarse ni reemplazarse. Solo se agregaría una ruta desde el panel autorizado.
- `nginx`: ya está instalado para RFID. La primera versión del portal no depende de él y no debe instalar otra instancia ni modificarlo sin coordinación.
- UFW/OpenSSH/Tailscale: son infraestructura existente; el despliegue no requiere reinstalarlos ni abrir el puerto 5100.

### Solo para administración o contingencia

- `curl`: útil para comprobar localmente `http://127.0.0.1:5100` si no estuviera instalado.
- `rsync`: útil si el procedimiento aprobado copia releases mediante SSH; no es requisito de ejecución.
- Herramienta o agente de respaldo externo: se definirá después de elegir el destino cifrado fuera del servidor.

### No instalar en producción

- El SDK completo `dotnet-sdk-10.0`, Visual Studio, Node.js y npm no son necesarios para ejecutar esta aplicación. La compilación y `dotnet publish` deben realizarse fuera del servidor.
- Docker no es necesario para el diseño recomendado.
- No se necesita otro nginx, Apache ni un PostgreSQL expuesto mediante contenedores.
- Las herramientas de Entity Framework (`dotnet-ef`) no deben quedar instaladas globalmente. Si una migración las requiere, debe ejecutarse como una etapa controlada con una herramienta versionada o un artefacto de migración probado.

### Comprobaciones posteriores a la instalación

Sin incluir contraseñas en el historial de comandos, verificar como mínimo:

```text
psql --version
pg_dump --version
systemctl status postgresql
fc-list

# Solo para despliegue framework-dependent:
dotnet --list-runtimes
```

La versión instalada y la salida relevante deben incorporarse al inventario del servidor. No debe considerarse terminada la instalación hasta probar conexión local a PostgreSQL, arranque del servicio, generación de PDF y un respaldo/restauración de prueba.

## 4. PostgreSQL

Crear una instancia local con:

- base exclusiva, por ejemplo `mpaz_matricula`;
- rol de aplicación exclusivo `mpaz_app`, sin privilegios de superusuario;
- escucha únicamente en loopback;
- contraseña aleatoria entregada mediante `ConnectionStrings__Default` desde configuración protegida;
- migraciones y DDL ejecutados en una etapa de despliegue controlada, no automáticamente en cada arranque;
- respaldo diario con `pg_dump`, prueba periódica de restauración y una copia cifrada fuera del equipo.

Si el directorio de datos queda en `/srv/mpaz-data`, PostgreSQL y la aplicación deben tener una dependencia explícita del punto de montaje. El montaje usa actualmente `nofail`: sin esa dependencia, un arranque sin el disco podría escribir datos en el SSD bajo la misma ruta.

## 5. Cloudflare Tunnel

### Viabilidad

Sí, el túnel existente puede servir más adelante la URL del proyecto. El inventario del 29 de octubre confirma que `cloudflared` está activo y que no existe un `config.yml` local; esto indica que probablemente es un túnel administrado remotamente desde Cloudflare Zero Trust.

Cloudflare permite agregar al mismo túnel una ruta de aplicación publicada:

```text
Hostname público: matriculas.<dominio-del-colegio>
Servicio de origen: http://localhost:5100
```

La configuración se realiza en el panel que administra el túnel. Se necesita confirmar quién controla esa cuenta y qué dominio está asociado. No hace falta abrir el puerto 5100 en el firewall, porque `cloudflared` inicia la conexión saliente.

### Protección recomendada

Para el portal actual, que es exclusivo para funcionarios:

1. Crear primero una aplicación de **Cloudflare Access** con política de denegación por defecto y usuarios/grupos autorizados.
2. Habilitar **Protect with Access** para que `cloudflared` valide el token en el origen.
3. Después crear la ruta pública del túnel.
4. Mantener además el inicio de sesión y los permisos propios de ASP.NET Core; Access es una capa adicional, no su reemplazo.

Cloudflare advierte que, si se crea la ruta del túnel antes que la aplicación Access, el hostname queda disponible públicamente. Para una futura sección destinada a apoderados habrá que diseñar una política distinta: Access puede ser inadecuado para usuarios externos y no resuelve por sí solo la autenticación del portal.

### Uso de nginx

No se recomienda integrar la primera versión con el nginx actual, porque el bloque `server_name _` del RFID captura todo el puerto 80 y cualquier cambio requiere coordinación con su responsable. Si en el futuro se necesita acceso directo dentro de la LAN, se puede agregar un virtual host con nombre propio que reenvíe a `127.0.0.1:5100`, después de acordarlo y probar `nginx -t`.

## 6. Bloqueos antes de cualquier publicación

La aplicación todavía **no está lista para exponerse**, aunque sea mediante Tunnel:

1. `Seguridad:ExigirInicioSesion` está en `false` en `appsettings.json`; producción debe exigir autenticación explícitamente.
2. Falta configurar encabezados reenviados de proxies para que ASP.NET Core reconozca correctamente el esquema HTTPS y la IP original sin confiar en proxies arbitrarios.
3. Falta persistir y proteger las claves de ASP.NET Core Data Protection; de otro modo, reiniciar invalida cookies y tokens.
4. La cadena de desarrollo contiene una contraseña versionada conocida; debe retirarse y rotarse antes de compartir o publicar el repositorio. Producción debe usar un secreto diferente.
5. `AllowedHosts` acepta `*`; producción debe limitarse al hostname acordado.
6. Deben existir al menos un administrador inicial controlado, roles/permisos verificados y un procedimiento de recuperación.
7. Deben comprobarse migraciones, respaldo y restauración de PostgreSQL.
8. Deben revisarse tratamiento, retención y acceso a datos sensibles de estudiantes, salud y PIE.
9. Debe acordarse con el responsable del RFID cualquier operación que afecte el servidor compartido.
10. Hay que confirmar el acceso administrativo a Cloudflare, el dominio disponible y que el túnel observado siga activo y sea el correcto.

## 7. Secuencia de despliegue propuesta

Esta secuencia requiere autorización y coordinación; **no se ejecutó** durante esta revisión.

1. Acordar responsables, dominio, ventana de trabajo, respaldo y reversión.
2. Corregir y probar localmente los bloqueos de seguridad.
3. Preparar y probar el artefacto Linux en un entorno Ubuntu equivalente.
4. Instalar PostgreSQL, su cliente y las fuentes requeridas; instalar el runtime de ASP.NET Core 10 solo si no se usa una publicación self-contained.
5. Crear usuario, directorios, PostgreSQL y secretos aislados.
6. Restaurar una base de prueba y validar migraciones.
7. Instalar el release y el servicio systemd en `127.0.0.1:5100`.
8. Verificar desde el propio servidor, sin publicar todavía.
9. Crear la aplicación Cloudflare Access y su política.
10. Añadir al túnel el hostname que apunta a `http://localhost:5100`.
11. Probar HTTPS, autenticación, permisos, carga/descarga de documentos, generación de PDF, reinicio, logs, respaldo y restauración.
12. Autorizar la puesta en producción o retirar la ruta y volver al release anterior.

## 8. Alcance de esta revisión

Se revisaron únicamente los archivos locales. La terminal SSH abierta previamente quedó autenticada por la interacción del usuario, pero no se ejecutó en ella ningún comando de auditoría, instalación o modificación para esta revisión. La información viva del túnel, el dominio, las versiones instaladas y el estado actual del servidor deben confirmarse mediante una auditoría de solo lectura autorizada antes de implementar este plan.

## Referencias

- Inventario local: [ficha-servidor-mpaz-server.md](ficha-servidor-mpaz-server.md)
- Conexión PostgreSQL del proyecto: [../../mpaz-matricula/database/CONEXION.md](../../mpaz-matricula/database/CONEXION.md)
- Cloudflare, aplicaciones publicadas por Tunnel: <https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/routing-to-tunnel/>
- Cloudflare, Access para aplicaciones self-hosted: <https://developers.cloudflare.com/cloudflare-one/access-controls/applications/http-apps/self-hosted-public-app/>
- Microsoft, ASP.NET Core detrás de nginx en Linux: <https://learn.microsoft.com/aspnet/core/host-and-deploy/linux-nginx>
