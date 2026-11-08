using System.Data.Common;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarcelaPaz.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly PanelIndicadores _panel;
    private readonly AnioEscolar _anios;

    public HomeController(PanelIndicadores panel, AnioEscolar anios)
    {
        _panel = panel;
        _anios = anios;
    }

    /// <summary>
    /// Panel de gestión. Sin año en la URL muestra el año con matrícula activa;
    /// con <c>?anio=</c> permite revisar un año anterior.
    /// </summary>
    public async Task<IActionResult> Index(int? anio)
    {
        var anioConsulta = await _anios.ResolverAsync(anio);

        ViewData["Anios"] = await _anios.ListarAsync();

        if (anioConsulta is null)
        {
            return View(new PanelInicio
            {
                Anio = 0,
                Error = "No hay un año escolar con matrícula activa. Secretaría o Dirección debe definirlo en Configuración → Año escolar.",
            });
        }

        try
        {
            return View(await _panel.CargarAsync(anioConsulta.Value));
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            // Sin base el portal sigue abriendo: el panel muestra el aviso y ceros.
            return View(new PanelInicio
            {
                Anio = anioConsulta.Value,
                Error = "No se pudieron leer los indicadores: revise la conexión con la base de datos.",
            });
        }
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
