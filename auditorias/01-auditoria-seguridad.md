# Auditoría de seguridad — Portal de Matrícula 2027

**Institución:** Escuela Escritora Marcela Paz
**Sistema:** `mpaz-matricula` (ASP.NET Core MVC + PostgreSQL 16)
**Fecha del informe:** 07-11-2026
**Alcance:** aplicación web `MarcelaPaz.Web`, configuración ASP.NET Core, esquema de permisos en base de datos, empaquetado Docker y archivos de configuración versionados.
**Tipo de revisión:** análisis estático de código y configuración (revisión manual dirigida), complementado con el comportamiento observado en las pruebas E2E de la Etapa I. **No** se ejecutó prueba de penetración, escaneo de dependencias ni análisis dinámico.
**Estado del código al momento de la revisión:** Etapa I implementada (asistente de 7 pasos, folio, PDF, auditoría, firma y confirmación). `dotnet build` sin errores y `dotnet test` 13/13 aprobadas. Sin commit ni push.

---

## 1. Resumen ejecutivo

El portal tiene una base de seguridad razonable para un sistema en construcción: Identity con hash de contraseña y bloqueo, antiforgery en todos los POST, consultas parametrizadas por EF Core, Razor sin `Html.Raw`, cookies `HttpOnly` y `Secure` fuera de desarrollo, HSTS en producción y separación de privilegios en la base de datos.

Sin embargo, **la decisión de autorización más importante del sistema está invertida**: existe un interruptor (`Seguridad:ExigirInicioSesion`) cuyo valor por defecto en código es `false`, y con ese valor *todas* las políticas de permiso —incluida la de administración de funcionarios— se resuelven como «permitir a cualquiera». La configuración versionada hoy lo deja en `true`, pero el comportamiento seguro depende de que ninguna publicación olvide definir la variable, no de un valor seguro por defecto. Publicar así, por ejemplo a través del túnel de Cloudflare ya preparado, deja el expediente completo de menores navegable sin autenticación.

A esto se suman dos problemas de fondo: el permiso de entrada a la consulta de alumnos (`GENERAL:LEER`) es único y arrastra consigo datos de salud, PIE, restricciones familiares y antecedentes judiciales —que el rol `DOCENTE` obtiene por defecto, contra la matriz de la sección 10 de los requerimientos—, y la desactivación de un funcionario o el cierre de un rol **no revoca la sesión activa**, que puede seguir operando hasta 8 horas prorrogables.

**Veredicto:** no apto para exposición pública ni para operar con datos reales hasta resolver S-01, S-02 y S-03.

| # | Hallazgo | Severidad |
|---|---|---|
| S-01 | El interruptor de seguridad falla «abierto»: con `ExigirInicioSesion=false` se anula toda autorización | **Crítico** |
| S-02 | `GENERAL:LEER` es un permiso único que expone salud, PIE, judicial y restricciones; `DOCENTE` lo tiene | **Alto** |
| S-03 | Desactivar un funcionario o cerrar su rol no invalida la sesión ni los permisos ya emitidos | **Alto** |
| S-04 | Credenciales y contraseñas por defecto versionadas en el repositorio | Medio |
| S-05 | Sin cabeceras de seguridad (CSP, `X-Frame-Options`, `Referrer-Policy`, `nosniff`) | Medio |
| S-06 | Consultas y búsquedas de datos sensibles no quedan en `registros_acceso` (MAT-13 incompleto) | Medio |
| S-07 | `TENS` no puede entrar al listado de alumnos aunque necesita el área `SALUD` | Medio |
| S-08 | `AllowedHosts` acepta `*` por defecto en el paquete Docker | Bajo |
| S-09 | Contraseña inicial compartida para todas las cuentas sembradas, sin cambio forzado | Bajo |
| S-10 | Antiforgery aplicado acción por acción, sin filtro global que cubra código futuro | Bajo |

---

## 2. Escala de severidad

| Nivel | Criterio |
|---|---|
| **Crítico** | Permite acceso no autenticado o no autorizado a datos personales de menores, o compromete el sistema completo. Explotable sin credenciales. |
| **Alto** | Exposición de datos sensibles a un rol que no debe verlos, o pérdida de control sobre una sesión ya otorgada. Requiere una cuenta válida. |
| **Medio** | Debilita defensas en profundidad, facilita otro ataque o incumple un requisito explícito del SRS. |
| **Bajo** | Endurecimiento recomendado; impacto limitado o dependiente de configuración. |

---

## 3. Hallazgos

### S-01 · Crítico — El interruptor de seguridad falla «abierto»

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Program.cs:115`
- `mpaz-matricula/MarcelaPaz.Web/Seguridad/AutorizacionPorPermiso.cs:47-48`, `:59-60`, `:63`, `:65-69`

**Evidencia**

`Program.cs:115` lee la configuración con valor por defecto `false`:

```csharp
var exigirInicioSesion = builder.Configuration.GetValue("Seguridad:ExigirInicioSesion", false);
```

`AutorizacionPorPermiso.cs` construye una política abierta y la devuelve para **cualquier** política cuando el interruptor está apagado:

```csharp
private static readonly AuthorizationPolicy Abierta =
    new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();   // :47-48

public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
    _seguridad.ExigirInicioSesion ? _respaldo.GetDefaultPolicyAsync() : Task.FromResult(Abierta);   // :59-60

public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
    _seguridad.ExigirInicioSesion ? _respaldo.GetFallbackPolicyAsync() : Task.FromResult<AuthorizationPolicy?>(Abierta);   // :63

public Task<AuthorizationPolicy?> GetPolicyAsync(string nombre)
{
    if (!_seguridad.ExigirInicioSesion)
    {
        return Task.FromResult<AuthorizationPolicy?>(Abierta);   // :69
    }
    ...
}
```

El efecto no es «modo lectura»: `GetPolicyAsync` corta antes de evaluar el nombre, así que `Permiso:ADMIN:ADMINISTRAR` —la política de `FuncionariosController:19`— también pasa a abierta. En ese estado se puede crear un funcionario, desactivar cuentas o cerrar roles sin haber iniciado sesión, porque las acciones POST solo exigen el token antiforgery, que se emite al renderizar el formulario.

Las vistas replican la bandera con su propio valor por defecto (`_AdminLayout.cshtml:5-8`, `Home/Index.cshtml:8-12`), de modo que la interfaz y el servidor pueden discrepar: si la clave falta, el menú oculta opciones mientras el servidor las permite (o al revés, según el archivo de configuración que gane).

**Estado actual de la configuración.** Hoy el valor es `true` en `appsettings.json:6`, en `docker/docker-compose.yml:39`, en `deploy/mpaz-matriculas.env.example:5` y en el runbook del servidor. El riesgo no es el valor presente, sino que **el código, si nadie configura nada, abre el portal**, y que el mismo interruptor sirve para «revisar estilos» en un entorno que ya tiene datos de prueba con estructura real.

**Impacto.** Divulgación completa del expediente de matrícula (identidad, RUN, domicilio, apoderados, contactos de emergencia, salud, PIE) y alteración de cuentas y roles, sin credenciales. Un despliegue que omita la variable —o un `appsettings.Production.json` que la sobrescriba— reproduce el escenario.

**Recomendación**
1. Eliminar el interruptor del camino de autorización. Si se necesita revisar estilos sin login, hacerlo con un entorno `Development` y datos ficticios, no con una bandera de producción.
2. Si se conserva por razones operativas, invertir el valor por defecto a `true` y **abortar el arranque** cuando `!IsDevelopment()` y la bandera sea `false`:
   ```csharp
   if (!builder.Environment.IsDevelopment() && !exigirInicioSesion)
       throw new InvalidOperationException("Producción no puede arrancar sin inicio de sesión obligatorio.");
   ```
3. Quitar la lectura de la bandera en las vistas (`_AdminLayout`, `Home/Index`) y derivar la visibilidad solo de los claims del usuario; así la interfaz no puede prometer permisos que el servidor no concede.
4. Añadir una prueba que falle si la política por defecto no exige usuario autenticado.

---

### S-02 · Alto — `GENERAL:LEER` concentra datos sensibles y lo tiene `DOCENTE`

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Controllers/AlumnosController.cs:20`, `:142`, `:187`, `:191`
- `mpaz-matricula/MarcelaPaz.Web/Controllers/FichaController.cs:58`, `:82`, `:431-432`, `:486-487`
- `mpaz-matricula/database/03-permisos-iniciales.sql` (bloques `DOCENTE` y `TENS`)

**Evidencia**

Toda la consulta de alumnos se protege con un único permiso de área general:

```csharp
[Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:LEER")]   // AlumnosController.cs:20
public class AlumnosController : Controller
```

Y `Detalle` devuelve, en la misma respuesta, datos clínicos y familiares:

```csharp
var salud = await _db.AntecedentesSalud ...          // :187
var situaciones = await _db.SituacionesMedicas ...   // :191
```

junto con `RestriccionesFamiliares`, `ContactosEmergencia`, `RetirosAutorizados` y los RUN de los apoderados. La ficha imprimible repite el conjunto: `Comprobante` (`FichaController.cs:431-432`) y `FormularioAlumno` (`:486-487`) exigen el mismo `GENERAL:LEER` y generan un PDF con esos datos.

En la matriz inicial, `DOCENTE` recibe exactamente ese permiso y ningún otro:

```sql
-- DOCENTE: solo datos mínimos.
('DOCENTE', 'GENERAL', 'LEER'),
```

El SRS es explícito en sentido contrario: «Docentes: datos mínimos según funciones; **no acceso por defecto al expediente completo**», y «Administración TI: sin lectura rutinaria de datos clínicos o judiciales».

**Impacto.** Cualquier docente con cuenta ve el diagnóstico, la medicación, las restricciones familiares y los antecedentes judiciales de cualquier alumno del colegio, recorriendo identificadores secuenciales (`/Alumnos/Detalle/{id}`). Son datos sensibles de menores: la exposición es difícil de revertir y activa obligaciones de la Ley 21.719 (vigente desde el 01-12-2026) y del propio protocolo de salud del establecimiento.

**Recomendación**
1. Separar el **permiso de entrada** del **permiso de campo**. Mantener `GENERAL:LEER` para abrir el listado con datos mínimos (nombre, curso, estado) y exigir `SALUD:LEER`, `PIE:LEER`, `JUDICIAL:LEER` y `RETIRO:LEER` para cada bloque de `Detalle`.
2. Aplicar la decisión en el servidor, no en la vista: no consultar ni proyectar el bloque si falta el permiso, en lugar de ocultarlo con CSS o `@if`.
3. Registrar el acceso denegado y el concedido por área (ver S-06).
4. Quitar `GENERAL:LEER` a `DOCENTE` o sustituirlo por un permiso acotado (`ALUMNOS:LEER_MINIMO`) que solo proyecte nombre y curso.

---

### S-03 · Alto — Desactivar un funcionario no revoca su sesión

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Controllers/FuncionariosController.cs:187-215` (`CambiarEstado`), `:217-240` (`CerrarRol`)
- `mpaz-matricula/MarcelaPaz.Web/Program.cs:95-96`
- `mpaz-matricula/MarcelaPaz.Web/Seguridad/FuncionarioClaimsPrincipalFactory.cs:26-31`, `:38-60`

**Evidencia**

`CambiarEstado` escribe `funcionario.Activo` dentro de la escritura auditada y redirige; no toca la cuenta de Identity:

```csharp
await _escritura.EjecutarAsync(FuncionarioActualId, _ =>
{
    funcionario.Activo = activo;
    return Task.CompletedTask;
});
```

Lo mismo hace `CerrarRol` con `relacion.Hasta = hoy`. Ninguna de las dos llama a `UpdateSecurityStampAsync`, `UpdateAsync` sobre `IdentityUser` ni fuerza el cierre de las sesiones del afectado. En todo el proyecto no hay ninguna referencia a `SecurityStamp`, `ValidateSecurityStamp` ni `SignOutAsync` salvo el cierre voluntario en `CuentaController.cs:130`.

Los roles y permisos se materializan **una sola vez**, al construir el principal:

```csharp
if (funcionario is null || !funcionario.Activo)
{
    return identidad;   // sin claims: sesión sin permisos
}
...
identidad.AddClaim(new Claim("permiso", $"{permiso.Area}:{permiso.Accion}"));   // :60
```

y la cookie dura 8 horas con expiración deslizante:

```csharp
opciones.ExpireTimeSpan = TimeSpan.FromHours(8);   // Program.cs:95
opciones.SlidingExpiration = true;                  // :96
```

**Impacto.** Un funcionario despedido, trasladado o al que se le retira un rol conserva exactamente los permisos que tenía al iniciar sesión. Con actividad periódica, la cookie se renueva indefinidamente: el acceso no caduca solo. El cierre de rol con fecha tampoco surte efecto hasta que la cookie expire. La comprobación de `Activo` en la fábrica de claims es correcta pero solo se evalúa en el siguiente inicio de sesión.

**Recomendación**
1. Al desactivar una cuenta o cerrar un rol, ejecutar `UpdateSecurityStampAsync` sobre el `IdentityUser` vinculado (`SsoSujeto`). Con `AddIdentityCookies()` el validador de sello ya está registrado; basta con acortar su intervalo (`SecurityStampValidatorOptions.ValidationInterval`, por defecto 30 min) para acotar la ventana.
2. Complementar con una comprobación periódica en servidor: un `IClaimsTransformation` o un `AuthorizationHandler` que consulte `funcionario.activo` y los roles vigentes con una caché corta (60–120 s). Esto cubre también los cambios de rol, que el sello no refleja si no se actualiza.
3. Bajar la expiración deslizante a un máximo absoluto (por ejemplo 8 h absolutas con `SlidingExpiration = false`, o 30 min deslizantes) y cerrar la sesión al cambiar la contraseña.
4. Registrar en `registros_acceso` la desactivación y el cierre de rol con el funcionario que los ejecutó (hoy el detalle queda en la escritura auditada, pero conviene un evento explícito legible).

---

### S-04 · Medio — Credenciales y contraseñas por defecto versionadas

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/appsettings.Development.json:4`, `:7`
- `mpaz-matricula/docker/docker-compose.yml:12`, `:40`, `:41`
- `mpaz-matricula/docker/README-docker.md:46`
- `mpaz-matricula/database/datos-prueba/credenciales.txt` (texto plano, trackeado)

**Evidencia**

```json
"Default": "Host=localhost;Port=5432;Database=mpaz-matricula;Username=postgres;Password=postgresql;..."
"ContrasenaInicial": "Mpaz2027segura"
```

y en el compose, valores por defecto usados cuando falta `.env`:

```yaml
POSTGRES_PASSWORD: ${CLAVE_POSTGRES:-postgres_mpaz_2027}          # :12
Seguridad__ContrasenaInicial: ${CLAVE_INICIAL:-Mpaz2027segura}     # :40
Password=${CLAVE_APP:-mpaz_app_2027}                              # :41
```

`README-docker.md:46` publica además un par correo/contraseña de una cuenta real de prueba.

**Impacto.** Si el repositorio se comparte, se publica o llega al colegio como entrega, quedan credenciales utilizables. El riesgo mayor es el patrón: el compose arranca en `Production` (`:35`) con contraseñas conocidas si nadie crea el `.env`, y nada lo impide.

**Recomendación**
1. Sacar `ContrasenaInicial` de `appsettings.Development.json` y del compose; dejarla solo como variable de entorno temporal, tal como ya documenta el runbook.
2. Validar al arrancar en `Production` que ninguna contraseña coincida con los valores por defecto conocidos y abortar si ocurre.
3. Rotar las claves expuestas (aunque sean de prueba) y eliminar `credenciales.txt` del control de versiones, reemplazándolo por un puntero al gestor de secretos.
4. Agregar `.env` y los archivos de datos de prueba a `.gitignore` y verificar que no queden en el historial.

---

### S-05 · Medio — Faltan cabeceras de seguridad

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Program.cs:143-147`

**Evidencia**

La única configuración de transporte es:

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
```

Una búsqueda de `Content-Security-Policy`, `X-Frame-Options`, `Referrer-Policy`, `X-Content-Type-Options` y `Permissions-Policy` en todo el proyecto no devuelve coincidencias.

**Impacto**
- **Clickjacking:** el portal puede embeberse en un iframe ajeno y superponer clics sobre «Cerrar rol», «Desactivar cuenta» o «Confirmar matrícula», que son POST con token válido porque el formulario se renderiza legítimamente.
- **Fuga por `Referer`:** las rutas llevan el identificador del alumno (`/Alumnos/Detalle/123`); al salir a un enlace externo, el navegador puede enviar esa URL completa.
- **Sniffing de MIME:** menor, pero relevante si en el futuro se sirven archivos subidos.

**Recomendación**
1. Middleware que agregue, como mínimo: `X-Frame-Options: DENY` (o `frame-ancestors 'none'`), `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy` restrictiva.
2. CSP con nonce para los dos scripts inline existentes (`Views/Ficha/Enviada.cshtml:38-52`); migrarlos a `ux.js` elimina la necesidad del nonce.
3. Mantener HSTS y añadir `includeSubDomains` cuando el hostname definitivo esté acordado.

---

### S-06 · Medio — La auditoría no registra consultas ni búsquedas de datos sensibles

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Controllers/AlumnosController.cs` (`Index`, `Detalle`): sin llamadas a `IAuditoriaAcceso`
- `mpaz-matricula/MarcelaPaz.Web/Controllers/FichaController.cs:431-479`, `:486-560`: solo `DESCARGAR`

**Evidencia**

Se registran tres tipos de evento: `DENEGADO` (en `PermisoAuthorizationHandler`), `REEMPLAZAR` al generar la ficha (`FichaController`, paso 7) y `DESCARGAR` al bajar el PDF. La consulta de la ficha del alumno —la pantalla que muestra salud, restricciones y PIE— no deja rastro. Tampoco la búsqueda por RUN en el listado, que permite barrer el padrón completo.

El requisito MAT-13 pide registrar «lecturas, búsquedas, descargas, exportaciones y accesos denegados de datos sensibles».

**Impacto.** Ante una filtración no se puede reconstruir quién consultó a qué alumno ni cuándo. La trazabilidad actual sirve para auditoría de cambios, no para investigación de accesos.

**Recomendación**
1. Registrar `LEER` en `Detalle` con `funcionario_id`, `estudiante_id`, área y resultado, reutilizando el detalle JSON que ya usa `FichaController`.
2. Registrar la búsqueda con el término normalizado (o un hash, si se prefiere no almacenar el RUN consultado) y el número de resultados.
3. Mantener la tabla como solo-inserción (el rol de base ya no tiene `DELETE`, lo cual es correcto) y definir su retención con el sostenedor.

---

### S-07 · Medio — `TENS` no puede entrar al listado de alumnos

**Ubicación**
- `mpaz-matricula/database/03-permisos-iniciales.sql` (bloque `TENS`)

**Evidencia**

`TENS` recibe `FAMILIA:LEER`, `SALUD:LEER`, `MEDICACION:LEER/CREAR` y `DOCUMENTOS:LEER`, pero **no** `GENERAL:LEER`. Como `AlumnosController` exige ese permiso a nivel de clase (`:20`), el rol no puede abrir el listado ni llegar a `Detalle`, que es justamente donde están los datos de salud que sí tiene permitido ver en el DDL.

**Impacto.** Es un defecto de autorización en ambos sentidos: la matriz no refleja el uso real (el TENS necesita salud) y la presión operativa empuja a conceder `GENERAL:LEER` —y con él todo el expediente— para destrabar el trabajo. Un permiso de más es peor que uno de menos, pero este último también empuja al primero.

**Recomendación.** Resolver junto con S-02: al separar permisos de entrada y de área, `TENS` recibe el permiso mínimo de entrada más `SALUD:LEER` y `MEDICACION:*`, y no obtiene acceso a judicial, PIE ni economía familiar.

---

### S-08 · Bajo — `AllowedHosts` acepta cualquier host en el paquete Docker

**Ubicación**
- `mpaz-matricula/docker/docker-compose.yml:37`

**Evidencia**

```yaml
AllowedHosts: ${ALLOWED_HOSTS:-*}
```

**Impacto.** Si el servicio queda detrás de un proxy que no filtra el encabezado `Host`, ASP.NET Core acepta cualquier nombre. Facilita envenenamiento de enlaces absolutos y reconocimiento. En el despliegue previsto con túnel de Cloudflare el impacto es bajo, porque el proxy reescribe el host.

**Recomendación.** Exigir `ALLOWED_HOSTS` explícito en `Production` (el runbook ya lo indica) y quitar el valor por defecto `*` del compose.

---

### S-09 · Bajo — Contraseña inicial compartida y sin cambio forzado

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Program.cs` (`Seguridad:ContrasenaInicial`, siembra de cuentas)
- `mpaz-matricula/docker/docker-compose.yml:40`

**Evidencia.** Las cuentas que se crean al arrancar desde funcionarios existentes usan la misma contraseña mientras la variable esté definida. Identity no impone cambio en el primer ingreso (no hay marca equivalente a «must change password»).

**Impacto.** Quien conozca el valor por defecto puede entrar a cualquier cuenta sembrada antes de que su titular la cambie. El runbook advierte retirar la variable, pero nada lo verifica.

**Recomendación.** Añadir un claim o columna `debe_cambiar_contrasena`, forzar el cambio en el primer login y dejar de sembrar cuentas con contraseña compartida en producción (crear cada una desde la pantalla de funcionarios).

---

### S-10 · Bajo — Antiforgery aplicado acción por acción

**Ubicación.** `[ValidateAntiForgeryToken]` individual en cada POST (`FuncionariosController.cs`, `AlumnosController.cs`, `FichaController.cs`).

**Impacto.** Hoy todas las acciones de escritura están cubiertas, así que no hay vulnerabilidad actual. El riesgo es de evolución: una acción POST nueva que olvide el atributo queda expuesta a CSRF, y `SameSite=Lax` no protege frente a navegación de nivel superior con `GET` ni frente a subdominios comprometidos.

**Recomendación.** Registrar globalmente:

```csharp
builder.Services.AddControllersWithViews(opciones =>
    opciones.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
```

y dejar el atributo explícito solo donde haga falta exceptuar.

---

## 4. Controles positivos verificados

| Control | Evidencia |
|---|---|
| Hash de contraseña y bloqueo por intentos | Identity, `Program.cs:65-76`; 5 intentos / 15 min |
| Política de contraseña razonable | mínimo 10 caracteres, mayúscula, minúscula y dígito (`Program.cs:68-72`) |
| Cookie `HttpOnly`, `SameSite=Lax`, `Secure` fuera de desarrollo | `Program.cs:83-96` |
| HSTS y redirección a HTTPS en producción | `Program.cs:143-147` |
| Claves de Data Protection persistidas y exigidas en producción | `Program.cs:21-35` |
| Encabezados reenviados solo desde proxies locales | `Program.cs:38-43` (`KnownProxies` loopback) |
| Antiforgery en todos los POST actuales | atributo presente en cada acción de escritura |
| Consultas parametrizadas | EF Core en todos los controladores; sin SQL concatenado |
| Sin `Html.Raw` en vistas | búsqueda sin coincidencias; Razor escapa por defecto |
| `returnUrl` restringido a rutas locales | `CuentaController` |
| Denegación registrada antes de responder 403 | `PermisoAuthorizationHandler` |
| Tabla de auditoría solo-inserción | rol `mpaz_app` sin `UPDATE`/`DELETE` sobre `registros_acceso` |
| Funcionario inactivo sin claims | `FuncionarioClaimsPrincipalFactory.cs:26-31` |
| Contenedor de aplicación sin privilegios | `docker/Dockerfile`, usuario no root |
| Base de datos no publicada al host | `docker/docker-compose.yml`; app y BD en red interna |

---

## 5. Plan de acción sugerido

| Orden | Acción | Hallazgo | Esfuerzo |
|---|---|---|---|
| 1 | Hacer que el sistema falle cerrado: eliminar el interruptor o abortar el arranque en producción | S-01 | Bajo |
| 2 | Separar permiso de entrada y permisos por área; excluir salud/PIE/judicial para `DOCENTE` | S-02, S-07 | Medio |
| 3 | Revocar sesión al desactivar cuenta o cerrar rol (sello + validación periódica) | S-03 | Medio |
| 4 | Rotar credenciales, sacar secretos del repositorio y validar defaults al arrancar | S-04, S-09 | Bajo |
| 5 | Cabeceras de seguridad y CSP | S-05 | Bajo |
| 6 | Registrar lecturas, búsquedas y descargas en `registros_acceso` | S-06 | Medio |
| 7 | Endurecer `AllowedHosts` y antiforgery global | S-08, S-10 | Bajo |

---

## 6. Fuera de alcance y limitaciones

- **No** se ejecutó prueba de penetración ni escaneo dinámico (DAST); los hallazgos provienen de lectura de código y configuración.
- **No** se ejecutó `dotnet list package --vulnerable` ni revisión de CVEs de dependencias o de la imagen base.
- **No** se probó el bloqueo de cuenta, la rotación de contraseña ni el flujo completo de recuperación de credenciales.
- **No** se revisó el servidor de producción (UFW, SSH, túneles, respaldos); solo lo declarado en los documentos del repositorio.
- **No** se evaluó el cumplimiento formal de la Ley 21.719: este informe describe riesgos técnicos, no asesoría legal. La revisión jurídica y del sostenedor sigue pendiente según el checklist del proyecto.
- La verificación funcional de la Etapa I (folio, PDF, auditoría, transiciones) se realizó en la sesión anterior sobre la base temporal `mpaz_e2e`, ya eliminada.

---

*Informe generado a partir del código en `mpaz-matricula/` al 07-11-2026. Las líneas citadas corresponden a ese estado; si el archivo cambia, deben revalidarse.*

---

## 7. Estado de resolución de los hallazgos de baja prioridad (08-11-2026)

Los tres hallazgos de severidad baja quedaron resueltos y verificados en ejecución.

| # | Estado | Implementación | Verificación |
|---|---|---|---|
| S-08 | Resuelto | `Program.cs` aborta el arranque fuera de Development si `AllowedHosts` está vacío o contiene `*`; `docker/docker-compose.yml` exige `ALLOWED_HOSTS` en `.env` (sin valor por defecto) y `docker/README-docker.md` lo documenta como obligatorio. | Con `ASPNETCORE_ENVIRONMENT=Production` y `AllowedHosts=*` la aplicación no arranca: `InvalidOperationException: Producción requiere AllowedHosts con los dominios del portal; no se acepta '*'`. |
| S-09 | Resuelto | `FuncionarioClaimsPrincipalFactory` añade el claim `debe_cambiar_contrasena` cuando la cuenta sigue usando `Seguridad:ContrasenaInicial`; un middleware redirige a `/Cuenta/CambiarContrasena`; la pantalla valida la clave actual, rechaza reutilizar la inicial, audita el evento y cierra la sesión. El sembrado de cuentas con clave compartida ya estaba limitado a Development. | Cuenta temporal: login inicial → 302 a la pantalla de cambio, cambio → 302 al login, login con la clave nueva → 200 sin redirección. |
| S-10 | Resuelto | `AddControllersWithViews` registra `AutoValidateAntiforgeryTokenAttribute` como filtro global, además de los atributos explícitos existentes. | Un POST a `/Cuenta/Login` sin token responde 400; los formularios reales siguen funcionando. |

**Nota sobre el resto del informe.** S-01 (valor seguro por defecto y aborto en producción), S-03 (renovación del sello de seguridad al desactivar cuentas o cerrar roles), S-05 (cabeceras de seguridad y CSP) y S-06 (registro de lecturas y búsquedas en `registros_acceso`) fueron corregidos en rondas anteriores; el código actual lo refleja en `Program.cs`, `FuncionariosController.RevocarSesionesAsync` y `AlumnosController` (`LEER` y `BUSCAR`).

**Requisito operativo pendiente.** El compose ahora falla si falta `ALLOWED_HOSTS`; cada despliegue debe definir el dominio real en su `.env` (en local, `localhost;127.0.0.1`).
