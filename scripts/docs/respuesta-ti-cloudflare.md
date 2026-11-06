# Respuesta a TI · publicación del portal en `empaz.cl`

Documento para responder al equipo del colegio sobre la conexión del Portal de
Matrícula con Cloudflare. Basado en el estado real del servidor y del código al
6 de noviembre de 2026.

## 1. Las ocho preguntas del equipo TI

| N.º | Pregunta | Respuesta |
| --- | --- | --- |
| 1 | Lenguaje y framework | ASP.NET Core 10 (`net10.0`), MVC con vistas Razor; Entity Framework Core con proveedor Npgsql y QuestPDF para los PDF |
| 2 | Puerto de la aplicación | `5100`, escuchando **solo en loopback** (`http://127.0.0.1:5100`) |
| 3 | Dirección local | `http://127.0.0.1:5100` en el servidor; LAN `192.168.50.233`, Tailscale `100.93.143.61` |
| 4 | Base de datos | PostgreSQL 16, local en `127.0.0.1:5432`, base `mpaz_matricula`, rol exclusivo `mpaz_app` |
| 5 | Ubicación | Servidor físico del colegio (`mpaz-server`, Ubuntu 24.04), aislado del RFID con usuario de sistema propio |
| 6 | HTTPS, sesiones y autenticación | TLS termina en Cloudflare; ver detalle abajo |
| 7 | Rutas o endpoints | No se expone API pública; sólo rutas MVC internas |
| 8 | Almacenamiento de documentos | Hoy no se guardan archivos: los PDF se generan en memoria bajo demanda |

### Detalle del punto 6

```text
TLS                Cloudflare (el origen no abre HTTPS local)
Encabezados        X-Forwarded-For / X-Forwarded-Proto aceptados SOLO desde loopback
Cookies            Secure + HttpOnly + SameSite=Lax, 8 h con renovación deslizante
HSTS               activo en producción
Data Protection    claves persistentes en /var/lib/mpaz-matriculas/keys
Login              ASP.NET Core Identity con hash; bloqueo tras 5 intentos por 15 min
Contraseñas        mínimo 10 caracteres, con mayúscula, minúscula y dígito
Autorización       roles y permisos resueltos desde el DDL institucional
Auditoría          registros de acceso en la tabla de auditoría
```

El portal no abre sesión propia: **Cloudflare Access es una capa adicional**, no un
reemplazo del login. Un usuario sin cuenta en el portal no entra aunque pase Access.

### Detalle del punto 7

```text
/Cuenta/Login        inicio de sesión
/Cuenta/Salida       cierre de sesión
/Ficha/...           asistente de matrícula (7 pasos) y PDF
/Funcionarios/...    administración de cuentas
/Home/Index          portada del panel
```

### Detalle del punto 8

Los PDF se generan al momento y se descargan; no se escriben en disco ni en
objetos. La base guarda sólo metadatos (27 documentos en los datos de prueba).
Si más adelante se cargan escaneos, habrá que definir almacenamiento de archivos
y respaldo antes de habilitarlo.

## 2. Confirmación de los tres puntos

| Punto | Respuesta |
| --- | --- |
| Dominio | `empaz.cl` existe y ya está en Cloudflare. **No hace falta cambiar nameservers**: agregar el hostname del túnel crea sólo un registro CNAME y no toca MX, SPF, DKIM ni DMARC |
| Tecnología | ASP.NET Core 10, no PHP, Python ni Node.js |
| Servidor | Sí, el mismo servidor físico Ubuntu 24.04 que hoy usa MPAZ RFID |

## 3. Configuración propuesta

```text
https://matricula.empaz.cl
        │
        ▼
Cloudflare Access (solo funcionarios)
        │
        ▼
Túnel mpaz-web  (dedicado, no el del RFID)
        │
        ▼
http://127.0.0.1:5100   ·   Kestrel, servicio systemd mpaz-matriculas
        │
        ▼
PostgreSQL 16 en 127.0.0.1:5432
```

| Campo en Cloudflare | Valor |
| --- | --- |
| Tunnel name | `mpaz-rfid-tunnel` (fase 1) · `mpaz-web` (fase 2) |
| Subdomain | `matricula` |
| Domain | `empaz.cl` |
| Service type | `HTTP` |
| Service URL | `127.0.0.1:5100` |
| Access | Self-hosted, política deny por defecto + correos autorizados |

El puerto **no** es 8080: el portal escucha en 5100 y no requiere abrir puertos en
el router ni en UFW, porque el túnel inicia la conexión desde el servidor.

### Orden de los pasos

1. Confirmar que `Seguridad__ExigirInicioSesion=true` en el servicio y reiniciarlo.
2. Configurar Zero Trust en la cuenta (team domain) para poder usar Access.
3. Crear la aplicación de **Access** para `matricula.empaz.cl` con los correos autorizados.
4. Agregar el **Public Hostname** `matricula` → `http://127.0.0.1:5100` al túnel actual.
5. Probar HTTPS, Access, login y descarga de PDF.

Los pasos 2 y 3 antes del 4 evitan que el hostname quede público mientras se configura.

### Fase 2: túnel dedicado `mpaz-web`

Solo cuando el colegio tenga cuenta propia. El `cloudflared` actual pertenece al
sistema de asistencia y se administra por token.
Para no modificarlo, el túnel nuevo se instala como unidad independiente:

```bash
sudo install -d -m 0700 /etc/cloudflared-mpaz
printf 'TUNNEL_TOKEN=%s\n' '<token-de-mpaz-web>' | sudo tee /etc/cloudflared-mpaz/tunnel.env >/dev/null
sudo chmod 600 /etc/cloudflared-mpaz/tunnel.env

sudo tee /etc/systemd/system/cloudflared-mpaz-web.service >/dev/null <<'EOF'
[Unit]
Description=Cloudflare Tunnel mpaz-web (Portal de Matricula)
After=network-online.target
Wants=network-online.target

[Service]
User=cloudflared
EnvironmentFile=/etc/cloudflared-mpaz/tunnel.env
ExecStart=/usr/bin/cloudflared --no-autoupdate --metrics 127.0.0.1:20243 tunnel run --token ${TUNNEL_TOKEN}
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable --now cloudflared-mpaz-web
systemctl status cloudflared-mpaz-web --no-pager
```

Comprobaciones: `systemctl is-active cloudflared mpaz-rfid nginx` debe seguir igual
que antes, y el túnel nuevo debe aparecer **Healthy** en el panel.

## 4. Lo que necesitamos del colegio

| Dato | Para qué |
| --- | --- |
| Estado de la zona `empaz.cl` | Confirmar que está activa y con DNS administrado en Cloudflare |
| Correos o grupo autorizado | Política de Cloudflare Access |
| Quién administra la cuenta | Permisos para crear túnel y Access |
| Decisión sobre `asistencia.empaz.cl` | El mismo túnel `mpaz-web` puede publicar también el RFID más adelante |

## 5. Diferencias con la propuesta inicial del chat

| Propuesta inicial | Ajuste |
| --- | --- |
| Puerto 8080 | Es 5100, sólo loopback |
| Un túnel para todo | Túnel `mpaz-web` dedicado, separado del de asistencia |
| Access como seguridad principal | Access + login propio con roles y permisos |
| Base centralizada para todos los sistemas | Por ahora base exclusiva del portal; la integración con RFID es etapa IV |

## 6. Reglas de trabajo en la cuenta del colegio

La zona `empaz.cl` está en la cuenta de Cloudflare del responsable del RFID. Mientras
no haya autorización explícita, el trabajo es **solo de lectura**.

### Se puede revisar sin cambiar nada

- Zona `empaz.cl` → Overview: estado (`Active`) y nameservers.
- DNS → Records: presencia de MX y TXT (SPF, DKIM, DMARC).
- Networks → Tunnels: nombre, ID y estado del túnel existente.
- Access → Applications: aplicaciones y políticas ya creadas.
- Members: quién más administra la cuenta.

### No tocar

- El túnel y los hostnames del sistema de asistencia.
- Registros DNS, nameservers ni el correo institucional.
- Políticas de Access de aplicaciones existentes.
- Tokens y API keys de la cuenta.
- Configuración de SSL/TLS, WAF o caché de la zona: afecta al RFID.

### Cómo crear lo nuevo sin sorpresas

Crear `mpaz-web` y su hostname es **aditivo**: no modifica lo existente, pero queda
registrado en la auditoría de la cuenta y puede disparar notificaciones al dueño.
Por eso conviene acordarlo antes y anotar quién autorizó.

| Fecha | Autorizó | Acción |
| --- | --- | --- |
| pendiente | responsable de la cuenta | crear túnel `mpaz-web` y hostname `matricula.empaz.cl` |

A mediano plazo conviene que el colegio tenga su propia cuenta institucional (o que
la zona se transfiera), para que el portal no dependa de una cuenta personal.

### Decisión del 6-11-2026: no operar dentro de la cuenta del colega

José no hará cambios en la cuenta de `Tap.tapia@duocuc.cl`. El trabajo queda **en
pausa** hasta que el responsable lo invite como miembro.

| Aspecto | Decisión |
| --- | --- |
| Cambios en la cuenta del colega | No se realizan |
| Vía elegida | Invitación como miembro: *Manage Account → Members → Invite* |
| Rol solicitado | Permisos de **Tunnel** y **Access**, sin Super Administrator |
| Mientras tanto | Sólo lectura, y únicamente si él lo autoriza |
| Alternativa sin invitación | Él crea la ruta y la app de Access, o entrega el token de un túnel `mpaz-web` |

Mensaje sugerido para pedirla:

> Hola, para publicar el portal de matrícula en `matricula.empaz.cl` necesito crear una
> ruta en el túnel y una aplicación de Access. No quiero tocar tu configuración: ¿me
> puedes invitar a la cuenta como miembro con permisos de Tunnel y Access? Así todo
> queda bajo tu supervisión y sin compartir credenciales.

## 7. Pendientes técnicos antes de publicar

- [ ] Confirmar estado de la zona `empaz.cl` y revisar registros de correo.
- [ ] Crear Access y el hostname `matricula.empaz.cl`.
- [ ] Instalar el túnel `mpaz-web` con la unidad propia.
- [ ] Cambiar `AllowedHosts` a `matricula.empaz.cl`.
- [ ] Instalar `fontconfig` y `fonts-dejavu-core` para los PDF.
- [ ] Probar el login real con un usuario mock.
- [ ] Retirar el Quick Tunnel temporal y rotar las claves de prueba.
- [ ] **En espera:** invitación del responsable a su cuenta de Cloudflare (*Members → Invite*).

## 8. Estado observado en el panel · 6 de noviembre de 2026

**Estado del trabajo: en pausa.** No se modifica nada en la cuenta del responsable
hasta recibir la invitación como miembro.

Zona `empaz.cl`: plan **Free**, DNS **Full**, con tráfico reciente (79 mil peticiones
en 24 h), es decir, **activa**. No hay que cambiar nameservers.

| Registro | Tipo | Destino | Proxy |
| --- | --- | --- | --- |
| `asistencia.empaz.cl` | CNAME de túnel | `mpaz-rfid-tunnel` | Proxied |
| `empaz.cl` (prioridad 1) | MX | `smtp.google.com` | DNS only |
| `empaz.cl` | TXT | SPF: `v=spf1 include:_spf.google.com ~all` | DNS only |
| `empaz.cl` | TXT | `google-site-verification=...` | DNS only |

- No existe A, AAAA ni CNAME para `empaz.cl` ni `www`: el sitio público no está publicado.
- **No hay DMARC.** Cloudflare lo advierte. No se toca: se informa al colegio porque
  afecta la suplantación del correo institucional.
- Túnel existente: `mpaz-rfid-tunnel`, `Healthy`, 1 réplica, versión 2026.9.3, 1 ruta publicada.
- **Zero Trust sin configurar**: hay que crear el equipo (team domain) antes de usar Access.
- Cuenta: `Tap.tapia@duocuc.cl`, cuenta personal del colega, no institucional.

### Decisión recomendada

Agregar el hostname `matricula` al túnel `mpaz-rfid-tunnel`:

- no requiere token ni instalar nada;
- no reinicia el servicio del RFID ni toca su ruta `asistencia.empaz.cl`;
- se revierte borrando esa única ruta desde el panel.

`mpaz-web` queda como fase 2, cuando exista una cuenta institucional del colegio.

### Bloqueo crítico antes de agregar la ruta

`Seguridad:ExigirInicioSesion` vale **false** por defecto en `Program.cs`. Si el
servicio actual no lo define en `true`, el portal queda navegable **sin login** en
cuanto la ruta exista. Verificar y corregir antes de publicar.

## 9. Paso a paso en el panel · fase 1

Todo es aditivo y reversible. Requiere la autorización del responsable de la cuenta.
Agregar una ruta **no reinicia** el conector ni afecta `asistencia.empaz.cl`.

### 9.1 Zero Trust, una sola vez

1. Zero Trust → *Get started*.
2. Team name: `mpaz` → queda `mpaz.cloudflareaccess.com`.
3. Plan **Free**: cubre hasta 50 usuarios y no pide tarjeta.
4. Login method: **One-time PIN** por correo, sin integrar Google Workspace todavía.

### 9.2 Aplicación de Access

1. Access → Applications → *Add an application* → **Self-hosted**.
2. Nombre: `Portal de Matrícula MPAZ`.
3. Session duration: **8 hours** (igual que la cookie del portal).
4. Application domain: `matricula` + `empaz.cl`.
5. Policy `Funcionarios MPAZ`: Action **Allow**, Include → *Emails* o
   *Email domain* `empaz.cl`.
6. Guardar. Cloudflare avisará que el DNS aún no existe: es esperado y se resuelve
   en el paso siguiente.

### 9.3 Ruta en el túnel existente

1. Networks → Tunnels → `mpaz-rfid-tunnel` → **Public Hostname** → *Add*.
2. Subdomain `matricula`, Domain `empaz.cl`, Path vacío.
3. Service type **HTTP**, URL `127.0.0.1:5100`.
4. Save. El túnel debe seguir **Healthy** con 1 réplica.
5. Comprobar que `asistencia.empaz.cl` responde igual que antes.

### 9.4 Verificación

```text
https://matricula.empaz.cl → pantalla de Access (correo + PIN)
                          → login del portal
                          → panel de matrícula
```

Si Access funciona pero el portal no carga, el problema está en el origen
(`127.0.0.1:5100`), no en Cloudflare.

### 9.5 Reversa

Borrar la Public Hostname `matricula` y, opcionalmente, la aplicación de Access.
No tocar la ruta `asistencia.empaz.cl`, los registros MX/TXT ni el túnel.

## 10. Datos que necesito para ejecutarlo

| Dato | Valor por defecto propuesto | Para qué |
| --- | --- | --- |
| Correos autorizados | — | Política de Access |
| ¿Lista o dominio completo? | lista corta | Evitar abrir el acceso a todo `@empaz.cl` |
| Team name | `mpaz` | Dominio de Zero Trust |
| Duración de sesión | 8 h | Coherencia con la cookie del portal |
| Autorización del responsable | mensaje escrito | Registro en la tabla de cambios |
| Permiso para revisar el servicio | — | Confirmar `ExigirInicioSesion=true` antes de publicar |

Solo el primero es imprescindible; el resto puede quedar en los valores propuestos.
