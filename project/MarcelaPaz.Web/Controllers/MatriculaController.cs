using Microsoft.AspNetCore.Mvc;
using MarcelaPaz.Web.Models;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Portal de apoderados: identificación del expediente (mockup 01 · acceso-apoderado).
/// Separado del futuro acceso institucional del personal (mockup 04).
/// </summary>
public class MatriculaController : Controller
{
    [HttpGet]
    public IActionResult Acceso()
    {
        return View();
    }

    /// <summary>
    /// Estructural por ahora: recibe los dos RUT sin validar nada.
    /// TODO (Flujo A, propuesta por validar en el acta): comprobar en servidor el vínculo
    /// titular/suplente entre apoderado y estudiante, con mensaje genérico que no revele
    /// qué dato falló ni si el estudiante existe.
    /// </summary>
    [HttpPost]
    public IActionResult Acceso(AccesoForm form)
    {
        // Sin verificación todavía: entra directo a la estructura del wizard (mockup 02).
        return RedirectToAction("Paso", "Ficha", new { id = 1 });
    }

    // Página de error genérica referenciada por Program.cs fuera de desarrollo
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
