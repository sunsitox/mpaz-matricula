using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Configuración institucional: qué año escolar se está matriculando. Se cambia a
/// mano —Secretaría o Dirección— y no por año calendario: el periodo de matrícula
/// no coincide con el calendario y en enero y febrero todavía se matricula el año
/// en curso. El sistema sólo usaría fechas de apertura si esa automatización se
/// acuerda más adelante.
/// </summary>
[Authorize(Roles = "SECRETARIA,DIRECCION")]
public class ConfiguracionController : Controller
{
    private readonly AnioEscolar _anios;
    private readonly IAuditoriaAcceso _auditoria;

    public ConfiguracionController(AnioEscolar anios, IAuditoriaAcceso auditoria)
    {
        _anios = anios;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> AnioEscolar()
    {
        return View(await _anios.ListarAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int anio)
    {
        if (await _anios.ActivarAsync(anio))
        {
            await _auditoria.RegistrarAsync(
                "REEMPLAZAR",
                "GENERAL",
                "anio_escolar",
                detalle: $"{{\"evento\":\"activar_anio\",\"anio\":{anio}}}");

            TempData["Notificacion"] = $"El año escolar {anio} quedó activo para nuevas matrículas.";
        }
        else
        {
            TempData["Aviso"] = $"No se pudo activar el año {anio}: revise que esté registrado.";
        }

        return RedirectToAction(nameof(AnioEscolar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Agregar(int anio, string? apertura, string? cierre)
    {
        DateOnly? inicio = DateOnly.TryParse(apertura, out var desde) ? desde : null;
        DateOnly? termino = DateOnly.TryParse(cierre, out var hasta) ? hasta : null;

        var error = await _anios.AgregarAsync(anio, inicio, termino);

        TempData[error is null ? "Notificacion" : "Aviso"] = error
            ?? $"El año escolar {anio} quedó registrado. Actívelo cuando corresponda.";

        return RedirectToAction(nameof(AnioEscolar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int anioOriginal, int anio, string? apertura, string? cierre)
    {
        DateOnly? inicio = DateOnly.TryParse(apertura, out var desde) ? desde : null;
        DateOnly? termino = DateOnly.TryParse(cierre, out var hasta) ? hasta : null;

        var error = await _anios.EditarAsync(anioOriginal, anio, inicio, termino);

        if (error is null)
        {
            await _auditoria.RegistrarAsync(
                "REEMPLAZAR",
                "GENERAL",
                "anio_escolar",
                detalle: $"{{\"evento\":\"editar_anio\",\"anio\":{anioOriginal},\"anio_nuevo\":{anio}}}");

            TempData["Notificacion"] = anio == anioOriginal
                ? $"Se actualizaron los datos del año escolar {anio}."
                : $"El año escolar {anioOriginal} quedó registrado como {anio}.";
        }
        else
        {
            TempData["Aviso"] = error;
        }

        return RedirectToAction(nameof(AnioEscolar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int anio)
    {
        var error = await _anios.EliminarAsync(anio);

        if (error is null)
        {
            await _auditoria.RegistrarAsync(
                "REEMPLAZAR",
                "GENERAL",
                "anio_escolar",
                detalle: $"{{\"evento\":\"eliminar_anio\",\"anio\":{anio}}}");

            TempData["Notificacion"] = $"El año escolar {anio} se eliminó.";
        }
        else
        {
            TempData["Aviso"] = error;
        }

        return RedirectToAction(nameof(AnioEscolar));
    }
}
