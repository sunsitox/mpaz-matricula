using MarcelaPaz.Infrastructure;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
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

// Cloudflare Tunnel se conecta desde el mismo servidor. Sólo se aceptan
// encabezados reenviados del proxy local, nunca de clientes arbitrarios.
builder.Services.Configure<ForwardedHeadersOptions>(opciones =>
{
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opciones.KnownProxies.Add(IPAddress.Loopback);
    opciones.KnownProxies.Add(IPAddress.IPv6Loopback);
});

// Interfaz gráfica MVC
builder.Services.AddControllersWithViews();

// Base de datos: el DDL institucional vive en "public" y Identity en "auth".
// La conexión, el proveedor de PostgreSQL y el DbContext se configuran en
// Infrastructure; esta capa sólo entrega la cadena.
builder.Services.AddPersistencia(cadenaConexion);

// Documentos imprimibles (PDF) generados desde el formulario.
builder.Services.AddDocumentos();

// Guardado del asistente en las tablas del DDL institucional.
builder.Services.AddScoped<RegistroFicha>();

// Indicadores de la portada (panel administrativo).
builder.Services.AddScoped<PanelIndicadores>();

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
    opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
    opciones.SlidingExpiration = true;
});

// Roles y permisos se resuelven desde el DDL, no desde Identity.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<IdentityUser>, FuncionarioClaimsPrincipalFactory>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermisoAuthorizationHandler>();
builder.Services.AddScoped<IAuditoriaAcceso, AuditoriaAcceso>();
builder.Services.AddScoped<EscrituraAuditada>();

// Reconoce a los funcionarios que ya están en la base. Una cuenta faltante sólo
// se crea si se entregó Seguridad__ContrasenaInicial mediante un secreto temporal.
builder.Services.AddScoped<CuentasFuncionario>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthorization();

// Interruptor temporal: mientras se revisan estilos se puede navegar sin iniciar sesión.
// Para volver al acceso obligatorio, poner "Seguridad:ExigirInicioSesion": true.
var exigirInicioSesion = builder.Configuration.GetValue("Seguridad:ExigirInicioSesion", false);
builder.Services.AddSingleton(new ConfiguracionSeguridad(exigirInicioSesion));

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

// Única entrada: login de funcionarios. No hay rutas para apoderados.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
