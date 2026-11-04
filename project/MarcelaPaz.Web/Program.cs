using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Interfaz gráfica MVC
builder.Services.AddControllersWithViews();

// Base de datos: el DDL institucional vive en "public" y Identity en "auth".
var cadena = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(cadena))
{
    // Sin cadena configurada la aplicación igual parte y muestra el login;
    // cualquier acceso a datos fallará hasta definir ConnectionStrings:Default.
    cadena = "Host=localhost;Port=5432;Database=mpaz;Username=mpaz_app";
}

builder.Services.AddDbContext<MatriculaDbContext>(opciones => opciones.UseNpgsql(cadena));

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
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthorization();

var app = builder.Build();

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
