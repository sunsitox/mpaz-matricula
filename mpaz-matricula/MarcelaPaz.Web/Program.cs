using MarcelaPaz.Infrastructure;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Documentos internos del establecimiento (ficha, certificado): licencia
// comunitaria de QuestPDF, configurada una sola vez al iniciar.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var cadenaConexion = builder.Configuration.GetConnectionString("Default");
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(cadenaConexion))
{
    throw new InvalidOperationException(
        "Producción requiere la variable ConnectionStrings__Default; no se usará una conexión implícita.");
}

// Dos caminos a la misma base: primero el endpoint del servicio web y, si no
// responde, la conexión directa a Neon. La elección y la revalidación ocurren
// en caliente (sin reiniciar), dentro de AddPersistencia.
var cadenaDirecta = builder.Configuration.GetConnectionString("Directa");

var directorioClaves = builder.Configuration["DataProtection:KeysPath"];
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(directorioClaves))
{
    throw new InvalidOperationException(
        "Producción requiere DataProtection__KeysPath para conservar cookies entre reinicios y releases.");
}

if (!string.IsNullOrWhiteSpace(directorioClaves))
{
    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(directorioClaves))
        .SetApplicationName("MarcelaPaz.Matriculas");
}

// Producción no acepta cualquier Host: el valor por defecto de appsettings es
// sólo para desarrollo (S-08).
var hostsPermitidos = builder.Configuration["AllowedHosts"];
if (!builder.Environment.IsDevelopment()
    && (string.IsNullOrWhiteSpace(hostsPermitidos) || hostsPermitidos.Contains('*')))
{
    throw new InvalidOperationException(
        "Producción requiere AllowedHosts con los dominios del portal; no se acepta '*'.");
}

// Cloudflare Tunnel se conecta desde el mismo servidor. Sólo se aceptan
// encabezados reenviados del proxy local, nunca de clientes arbitrarios.
builder.Services.Configure<ForwardedHeadersOptions>(opciones =>
{
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opciones.KnownProxies.Add(IPAddress.Loopback);
    opciones.KnownProxies.Add(IPAddress.IPv6Loopback);
});

// Interfaz gráfica MVC. Antiforgery global: toda acción que no sea GET exige
// token, incluidas las que se agreguen en el futuro (S-10).
builder.Services.AddControllersWithViews(opciones =>
    opciones.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Freno por origen a los intentos de inicio de sesión. El bloqueo de Identity
// es por cuenta; este límite evita además la prueba masiva de contraseñas desde
// una misma dirección (una red escolar completa cuenta como un origen).
builder.Services.AddRateLimiter(opciones =>
{
    opciones.AddPolicy("login", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "sin-origen",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
            }));

    // Al agotarse el límite se vuelve al formulario con un aviso, en vez de
    // dejar al usuario en una página 429 vacía. La petición rechazada no
    // reinicia la ventana: el límite se mantiene igual.
    opciones.OnRejected = (contexto, _) =>
    {
        contexto.HttpContext.Response.Redirect("/Cuenta/Login?limite=1");
        return ValueTask.CompletedTask;
    };
});

// Base de datos: una sola base con un esquema por módulo. El dominio y las
// cuentas (Identity) conviven en el esquema "mpaz_matricula"; la cadena
// "Auth" existe por si algún despliegue separa las cuentas en otra base.
// El proveedor de PostgreSQL y los DbContext se configuran en Infrastructure;
// esta capa sólo entrega las cadenas.
builder.Services.AddPersistencia(cadenaConexion, cadenaDirecta, builder.Configuration.GetConnectionString("Auth"));

// Emisor SSO (prototipo del login único): el portal emite credenciales
// OpenID Connect para Asistencia, la web y Notas.
builder.Services.AddEmisorSso(builder.Configuration, builder.Environment);

// Documentos imprimibles (PDF) generados desde el formulario.
builder.Services.AddDocumentos();
builder.Services.Configure<DatosInstitucion>(builder.Configuration.GetSection("Institucion"));

// Guardado del asistente en las tablas del DDL institucional.
builder.Services.AddScoped<RegistroFicha>();

// Sincronización de funcionarios con el web service externo; la base Neon
// es el respaldo si el web service está caído. Timeout corto para no
// retener la creación del funcionario.
builder.Services.AddHttpClient<RegistroWebService>(cliente =>
    cliente.Timeout = TimeSpan.FromSeconds(8));

// Indicadores de la portada (panel administrativo).
builder.Services.AddScoped<PanelIndicadores>();

// Año escolar con matrícula activa: configuración, nunca año calendario.
builder.Services.AddScoped<AnioEscolar>();

// MAT-17: revisión automática de alertas en segundo plano (cada 15 min).
builder.Services.AddHostedService<AlertasProgramadas>();

// Expediente documental: archivos fuera de wwwroot, con hash y validación real.
builder.Services.AddScoped<ExpedienteDocumentos>();

// Credenciales de funcionarios: ASP.NET Core Identity (hash) con los roles del DDL.
builder.Services
    .AddIdentityCore<IdentityUser>(opciones =>
    {
        opciones.User.RequireUniqueEmail = true;
        opciones.Password.RequiredLength = 10;
        opciones.Password.RequireDigit = true;
        opciones.Password.RequireUppercase = true;
        opciones.Password.RequireLowercase = true;
        opciones.Password.RequireNonAlphanumeric = false;
        opciones.Lockout.AllowedForNewUsers = true;
        opciones.Lockout.MaxFailedAccessAttempts = 5;
        opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddSignInManager()
    .AddEntityFrameworkStores<CuentasDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(opciones =>
{
    opciones.Cookie.Name = "mpaz.matricula";
    opciones.Cookie.HttpOnly = true;
    opciones.Cookie.SameSite = SameSiteMode.Lax;
    // HTTPS obligatorio; en desarrollo local se permite http para poder probar.
    opciones.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    opciones.LoginPath = "/Cuenta/Login";
    opciones.LogoutPath = "/Cuenta/Salida";
    opciones.AccessDeniedPath = "/Cuenta/SinPermiso";
    // Duración absoluta: la sesión no se renueva sola indefinidamente. Con la
    // validación de permisos por petición (VigilanteSesion), ocho horas es el
    // techo de una sesión abandonada, no el mínimo.
    opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
    opciones.SlidingExpiration = false;
});

// Roles y permisos se resuelven desde el DDL, no desde Identity.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<IdentityUser>, FuncionarioClaimsPrincipalFactory>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermisoAuthorizationHandler>();
builder.Services.AddScoped<IAuditoriaAcceso, AuditoriaAcceso>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<VigilanteSesion>();
builder.Services.AddScoped<EscrituraAuditada>();

// Reconoce a los funcionarios que ya están en la base. Una cuenta faltante sólo
// se crea si se entregó Seguridad__ContrasenaInicial mediante un secreto temporal.
builder.Services.AddScoped<CuentasFuncionario>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthorization();

// Interruptor de revisión de estilos: sin la clave, el portal exige inicio de sesión.
// Falla cerrado: nunca se asume «sin login» por omisión de configuración.
var exigirInicioSesion = builder.Configuration.GetValue("Seguridad:ExigirInicioSesion", true);

if (!builder.Environment.IsDevelopment() && !exigirInicioSesion)
{
    throw new InvalidOperationException(
        "Producción no puede arrancar con Seguridad__ExigirInicioSesion=false: el portal quedaría sin autorización.");
}

builder.Services.AddSingleton(new ConfiguracionSeguridad(exigirInicioSesion));

// Credenciales conocidas del repositorio y de la documentación: si alguna
// sobrevive hasta producción, el arranque se detiene (S-04).
if (!builder.Environment.IsDevelopment())
{
    var clavesPorDefecto = new[] { "postgresql", "postgres_mpaz_2027", "mpaz_app_2027", "Mpaz2027segura" };

    var credenciales = new[]
    {
        ("ConnectionStrings__Default", cadenaConexion),
        ("Seguridad__ContrasenaInicial", builder.Configuration["Seguridad:ContrasenaInicial"]),
    };

    foreach (var (nombre, valor) in credenciales)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            continue;
        }

        var expuesta = clavesPorDefecto.FirstOrDefault(
            clave => valor.Contains($"Password={clave}", StringComparison.OrdinalIgnoreCase)
                     || valor.Contains($"Password={clave};", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(valor, clave, StringComparison.Ordinal));

        if (expuesta is not null)
        {
            throw new InvalidOperationException(
                $"{nombre} usa una contraseña por defecto conocida ({expuesta}). Genere una nueva antes de publicar.");
        }
    }
}

var app = builder.Build();

// Queda en el registro de arranque qué camino se está usando a la base.
var conexionViva = app.Services.GetRequiredService<ConexionViva>();
app.Logger.LogInformation("Base de datos: {Origen}.", conexionViva.Origen);
if (conexionViva.Aviso is not null)
{
    app.Logger.LogWarning("{Aviso}", conexionViva.Aviso);
}

// Al partir, cada funcionario activo de la base queda con su cuenta lista.
await using (var alcance = app.Services.CreateAsyncScope())
{
    try
    {
        var cuentas = alcance.ServiceProvider.GetRequiredService<CuentasFuncionario>();
        var (creadas, enlazadas) = await cuentas.SembrarAsync();

        app.Logger.LogInformation(
            "Cuentas de funcionarios desde la base: {Creadas} creadas, {Enlazadas} enlazadas.",
            creadas,
            enlazadas);
    }
    catch (Exception ex)
    {
        // Sin base de datos el portal igual abre el login y avisa en pantalla.
        app.Logger.LogError(ex, "No se pudieron preparar las cuentas de los funcionarios.");
    }
    }

    // Emisor SSO (prototipo del login único): registra el cliente y los scopes
    // de prueba. Sólo siembra en desarrollo; el registro real de clientes es
    // tarea del despliegue.
    await using (var alcanceSso = app.Services.CreateAsyncScope())
    {
        try
        {
            await EmisorSso.SembrarAsync(alcanceSso.ServiceProvider);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "No se pudo sembrar el emisor SSO de prueba.");
        }
    }

app.UseForwardedHeaders();

// Cabeceras de seguridad en todas las respuestas (S-05). La CSP no permite
// scripts externos ni inline: la interfaz sólo carga /js/ux.js del propio sitio.
// En Desarrollo se amplía sólo para BrowserLink/Browser Refresh (puertos
// locales y su fragmento inline); producción queda estricta.
var esDesarrollo = app.Environment.IsDevelopment();
app.Use(async (contexto, siguiente) =>
{
    var cabeceras = contexto.Response.Headers;
    cabeceras["X-Content-Type-Options"] = "nosniff";
    cabeceras["X-Frame-Options"] = "DENY";
    cabeceras["Referrer-Policy"] = "no-referrer";
    cabeceras["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    cabeceras["Content-Security-Policy"] = esDesarrollo
        ? "default-src 'self'; connect-src 'self' http://localhost:* https://localhost:* "
          + "ws://localhost:* wss://localhost:*; img-src 'self' data:; "
          + "style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; "
          + "frame-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self';"
        : "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; "
          + "script-src 'self'; frame-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self';";

    // El PDF del paso 8 debe poder incrustarse en el propio portal. Se aplica
    // al emitir la respuesta para comprobar la acción resuelta y el tipo real:
    // ni el login ni un error/redirección reciben esta excepción.
    contexto.Response.OnStarting(() =>
    {
        if (string.Equals(contexto.Request.RouteValues["controller"]?.ToString(), "Ficha", StringComparison.OrdinalIgnoreCase)
            && string.Equals(contexto.Request.RouteValues["action"]?.ToString(), "Comprobante", StringComparison.OrdinalIgnoreCase)
            && contexto.Response.ContentType == "application/pdf")
        {
            cabeceras["X-Frame-Options"] = "SAMEORIGIN";
            cabeceras["Content-Security-Policy"] = cabeceras["Content-Security-Policy"]
                .ToString().Replace("frame-ancestors 'none'", "frame-ancestors 'self'", StringComparison.Ordinal);
        }

        return Task.CompletedTask;
    });

    await siguiente();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();

// Datos de menores: ninguna página autenticada queda en la caché del navegador.
// Los archivos estáticos no pasan por aquí (UseStaticFiles ya respondió) y el
// login, sin sesión, sigue cacheándose.
app.Use(async (contexto, siguiente) =>
{
    if (contexto.User.Identity?.IsAuthenticated == true)
    {
        contexto.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        contexto.Response.Headers.Pragma = "no-cache";
    }

    await siguiente();
});

app.UseAuthorization();

// S-09: mientras la cuenta siga usando la contraseña inicial compartida, no se
// puede operar: el primer ingreso obliga a cambiarla.
app.Use(async (contexto, siguiente) =>
{
    if (contexto.User.Identity?.IsAuthenticated == true
        && contexto.User.HasClaim("debe_cambiar_contrasena", "1")
        && !contexto.Request.Path.StartsWithSegments("/Cuenta/CambiarContrasena")
        && !contexto.Request.Path.StartsWithSegments("/Cuenta/Salida"))
    {
        contexto.Response.Redirect("/Cuenta/CambiarContrasena");
        return;
    }

    await siguiente();
});

// Única entrada: login de funcionarios. No hay rutas para apoderados.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Calentamiento en segundo plano de QuestPDF (precarga fuentes y SkiaSharp para evitar lag en primera vista previa)
var generadorPdf = app.Services.GetService<MarcelaPaz.Infrastructure.Documentos.FichaMatriculaPdf>();
_ = Task.Run(() =>
{
    try
    {
        var dummy = MarcelaPaz.Web.Models.FichaImpresion.DesdeBorrador(new Dictionary<string, string>(), 2027, DateTime.Now);
        generadorPdf?.Generar(dummy);
    }
    catch
    {
        // Si falla en segundo plano no detiene la aplicación
    }
});

app.Run();
