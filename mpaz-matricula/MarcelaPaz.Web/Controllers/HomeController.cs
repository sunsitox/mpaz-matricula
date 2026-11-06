using System.Data.Common;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarcelaPaz.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    /// <summary>Año escolar que se está matriculando (mismo que usa el asistente).</summary>
    private const int AnioPanel = 2027;

    private readonly PanelIndicadores _panel;

    public HomeController(PanelIndicadores panel) => _panel = panel;

    public async Task<IActionResult> Index()
    {
        try
        {
            return View(await _panel.CargarAsync(AnioPanel));
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            // Sin base el portal sigue abriendo: el panel muestra el aviso y ceros.
            return View(new PanelInicio
            {
                Anio = AnioPanel,
                Error = "No se pudieron leer los indicadores: revise la conexión con la base de datos.",
            });
        }
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
