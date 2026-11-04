# Ficha del servidor mpaz-server

**Fecha de auditoría:** 29 de octubre de 2026  
**Autor:** José Miguel  
**Propósito:** mapeo técnico del servidor del colegio como antecedente para el posterior despliegue del sistema de fichas escolares.

## Resumen

`mpaz-server` es un PC de escritorio con Ubuntu 24.04 que ya está en uso con el sistema de asistencia RFID del colegio, y tiene recursos de sobra para sumar una base de datos y la aplicación de fichas escolares.

Actualmente no tiene una base de datos, .NET ni Docker instalados. Comparte cuenta de usuario y servicios con el proyecto RFID, que pertenece a otro colega, por lo que cualquier instalación debe coordinarse con él.

Esta ficha resume una auditoría de solo lectura realizada el 29 de octubre de 2026. No se instaló ni modificó nada en el servidor.

## Hardware y almacenamiento

El equipo tiene capacidad suficiente para una base de datos y una aplicación web, y su carga actual es casi nula.

| Elemento | Detalle |
| --- | --- |
| Equipo | HP EliteDesk 705 G4 MT (PC de escritorio, no servidor de rack) |
| Sistema | Ubuntu 24.04.5 LTS, kernel 7.0.0-34-generic |
| CPU | 16 hilos |
| RAM | 15 GB (12 GB disponibles) y 4 GB de swap |
| Disco del sistema | NVMe de 476 GB, 27 GB usados (6 %) |
| Disco de datos | 1,8 TB (ext4) montado en `/srv/mpaz-data` por UUID, con opción `nofail` |
| Actividad | 19 días encendido, carga promedio 0,05 |

El disco de datos ocupa solo 3 MB, pero ya contiene las carpetas `backups`, `reportes` y `respaldos_mpaz_rfid` del sistema RFID. No se observa RAID: cada disco es independiente y todo reside en un solo equipo.

## Acceso y usuarios

Los accesos observados utilizan la misma cuenta, `mpaz-server`, que tiene permisos `sudo` completos; no se observaron cuentas individuales por persona.

- Los accesos recientes por SSH provienen de dispositivos de Tailscale, no de direcciones públicas.
- Uno de esos dispositivos aparece como `Umbra` (`100.92.246.74`), no pertenece al autor de este proyecto y no está confirmado quién lo utiliza. En el panel de Tailscale del colega que creó el servicio figuran dos dispositivos: el servidor y `Umbra`.
- SSH acepta contraseña (`PasswordAuthentication yes`). El usuario `root` solo puede ingresar mediante llave.
- Existe una sesión gráfica de GNOME abierta desde el 15 de octubre en el propio equipo.

## Red y firewall

El firewall está activo y solo permite conexiones entrantes a SSH (22) y nginx (80); los demás puertos entrantes están denegados.

| Interfaz | Dirección | Uso |
| --- | --- | --- |
| `eno1` (cable) | `192.168.50.233/24` | Red local |
| `tailscale0` | `100.93.143.61` | Acceso remoto mediante Tailscale |
| `wlp7s0` (wifi) | Sin conexión | Apagada |

- Los puertos 22 y 80 están abiertos a cualquier origen que alcance el equipo, incluidas la red local y Tailscale.
- No existe HTTPS local: nginx solo escucha en el puerto 80. Hay un servicio Cloudflare Tunnel activo sin `config.yml` local, por lo que probablemente se administra desde el panel de Cloudflare.
- El equipo tiene salida a internet (prueba con `curl` exitosa) y utiliza los repositorios oficiales de Ubuntu, sin proxy.
- No hay servicios escuchando en los puertos 5432 ni 3306.

## Software y servicios

El servidor tiene nginx, Python 3, Tailscale y Cloudflare Tunnel; no tiene PostgreSQL, MySQL, MariaDB, .NET, Docker, Node, Java ni PHP.

| Servicio | Qué hace | Dónde escucha |
| --- | --- | --- |
| nginx | Un solo sitio (`mpaz-rfid`, `server_name _`): todo el puerto 80 se dirige a Gunicorn | `0.0.0.0:80` |
| `mpaz-rfid` | API de asistencia escolar, Gunicorn con 2 workers; proyecto de otro colega | `127.0.0.1:8000` |
| Servidor de pruebas | Python (`servidor_vista_previa.py`, carpeta `pruebas_mpaz`), activo hace 2 días | `127.0.0.1:8765` |
| `cloudflared` | Cloudflare Tunnel | Conexión saliente; métricas en `127.0.0.1:20241` |
| `tailscaled` | Acceso remoto | Interfaz `tailscale0` |
| `mpaz-cierre-jornadas` | Tarea diaria del sistema RFID | Todos los días a las 17:00 |

`mpaz-rfid` se ejecuta con el usuario `mpaz-server`. Su código se encuentra en `/home/mpaz-server/Proyectos` y utiliza un esquema de releases por directorio, con un archivo de variables de entorno en `/etc/mpaz-rfid.env`.

También hay servicios de escritorio (GNOME, CUPS, Bluetooth y Avahi) que un servidor no necesita. Las actualizaciones automáticas están activas y quedan paquetes pendientes, entre ellos `cloudflared`.

## Respaldos existentes

No se encontró ningún respaldo programado: no hay tareas cron y el único timer propio es el cierre diario de jornadas del sistema RFID.

- Existen copias manuales o de despliegue del sistema RFID en `/srv/mpaz-data/respaldos_mpaz_rfid` (carpetas del 27 de octubre, antes y después de publicar una versión), accesibles solo para `root`.
- También existe `/srv/mpaz-data/backups/rfid`, cuyo contenido no fue revisado.
- Todas las copias están en el mismo disco del mismo equipo, por lo que no protegen ante una falla del disco o del PC.

## Riesgos y recomendaciones

Antes de instalar la base de datos y la aplicación, conviene resolver los siguientes puntos, ordenados de mayor a menor importancia.

1. **Cuenta compartida con `sudo`.** Cada persona debería tener su propio usuario y cada aplicación debería ejecutarse con un usuario de sistema sin `sudo`. En la situación actual, si una aplicación se ve comprometida, el atacante podría obtener acceso a la cuenta administradora.
2. **Respaldos.** Programar respaldos de la base de datos y conservar una copia fuera de este equipo. Los respaldos actuales residen en el mismo disco.
3. **SSH con contraseña.** Migrar a llaves SSH, desactivar el acceso por contraseña y limitar el puerto 22 a Tailscale, cuidando no perder el acceso durante los cambios del firewall.
4. **nginx compartido.** El sitio del sistema RFID atiende todo el puerto 80 como comodín. Un sitio nuevo requiere ajustar la configuración de servidor predeterminado, lo que implica editar el archivo existente con la autorización del colega responsable.
5. **Disco de datos con `nofail`.** Si el disco no se monta al iniciar, un servicio podría escribir en el SSD del sistema. PostgreSQL debería depender del montaje de `/srv/mpaz-data`.
6. **Sin política de datos del colegio.** Como no hay información digitalizada, el equipo debe definir medidas mínimas: acceso por roles, HTTPS, contraseñas almacenadas mediante hash seguro, respaldo diario con copia externa y registro de accesos. Se trata de datos de menores y conviene revisar las exigencias de la normativa chilena sobre datos personales, actualmente en proceso de cambio.
7. **Servicios de escritorio.** GNOME, CUPS, Bluetooth y Avahi consumen recursos y amplían la superficie de ataque; podrían desactivarse posteriormente.

## Pendientes por confirmar

Los siguientes puntos dependen principalmente de decisiones y coordinaciones con personas responsables:

- [ ] Confirmar con el colega del sistema RFID qué es `Umbra` (`100.92.246.74`) y quién lo utiliza.
- [ ] Acordar reglas de convivencia en el servidor: carpetas, puertos, nginx y responsables de reiniciar servicios.
- [ ] Definir cómo se publicará la aplicación de fichas: solo en la red local, mediante Tailscale o a través de un subdominio.
- [ ] Consultar al colegio o al docente sobre políticas de respaldo y protección de datos.
- [ ] Revisar el contenido de `/srv/mpaz-data/backups/rfid` y confirmar si existe algún respaldo automático con otro nombre.

## Limitaciones de la auditoría

No se verificaron el modelo exacto de CPU, el contenido de las carpetas de respaldo ni las demás cuentas de usuario del sistema.
