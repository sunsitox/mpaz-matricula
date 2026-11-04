var builder = WebApplication.CreateBuilder(args);

// Registrar controladores con vistas para la interfaz gráfica (MVC)
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configurar el pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Matricula/Error");
    app.UseHsts();
}

// Archivos estáticos (CSS, imágenes; el logo oficial irá en wwwroot cuando llegue)
app.UseStaticFiles();

// Ruta por defecto: acceso del apoderado al portal (mockup 01)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Matricula}/{action=Acceso}/{id?}");

app.Run();