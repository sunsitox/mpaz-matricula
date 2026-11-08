using MarcelaPaz.Infrastructure;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

var cadenaConexion = builder.Configuration.GetConnectionString("Default");
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(cadenaConexion))
{
    throw new InvalidOperationException(
        "Producción requiere la variable ConnectionStrings__Default; no se usará una conexión implícita.");
}

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

// Base de datos: todo el portal vive en el esquema dedicado "mpaz_matricula"
// (DDL institucional, Identity y __EFMigrationsHistory), para que la base pueda
// convivir con otras sin mezclar tablas. La conexión, el proveedor de PostgreSQL
// y el DbContext se configuran en Infrastructure; esta capa sólo entrega la cadena.
builder.Services.AddPersistencia(cadenaConexion);

// Documentos imprimibles (PDF) generados desde el formulario.
builder.Services.AddDocumentos();

// Guardado del asistente en las tablas del DDL institucional.
builder.Services.AddScoped<RegistroFicha>();

// Indicadores de la portada (panel administrativo).
builder.Services.AddScoped<PanelIndicadores>();

// Año escolar con matrícula activa: configuración, nunca año calendario.
builder.Services.AddScoped<AnioEscolar>();

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
    .AddEntityFrameworkStores<MatriculaDbContext>()
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

app.UseForwardedHeaders();

// Cabeceras de seguridad en todas las respuestas (S-05). La CSP no permite
// scripts externos ni inline: la interfaz sólo carga /js/ux.js del propio sitio.
app.Use(async (contexto, siguiente) =>
{
    var cabeceras = contexto.Response.Headers;
    cabeceras["X-Content-Type-Options"] = "nosniff";
    cabeceras["X-Frame-Options"] = "DENY";
    cabeceras["Referrer-Policy"] = "no-referrer";
    cabeceras["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    cabeceras["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; "
        + "script-src 'self'; frame-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self';";

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
app.UseAuthentication();
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

app.Run();
