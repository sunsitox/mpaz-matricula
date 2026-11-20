using System.Security.Claims;
using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Fichas y certificados médicos de los funcionarios. Es información sensible
/// del personal: la sección «Personal» queda reservada a UTP y Dirección,
/// tanto en la vista como en el servidor. Está fuera de la zona académica
/// de UTP.
/// </summary>
[Authorize(Roles = "UTP,DIRECCION")]
[Route("personal/fichas-medicas")]
public class FichasMedicasController : Controller
{
    private readonly MatriculaDbContext _db;
    private readonly ExpedienteDocumentos _documentos;
    private readonly EscrituraAuditada _escritura;
    private readonly IAuditoriaAcceso _auditoria;

    public FichasMedicasController(
        MatriculaDbContext db,
        ExpedienteDocumentos documentos,
        EscrituraAuditada escritura,
        IAuditoriaAcceso auditoria)
    {
        _db = db;
        _documentos = documentos;
        _escritura = escritura;
        _auditoria = auditoria;
    }

    private int FuncionarioActualId =>
        int.TryParse(User.FindFirstValue("funcionario_id"), out var id) ? id : 0;

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? busqueda,
        int funcionarioId = 0,
        string estado = "todas")
    {
        var funcionarios = await _db.Funcionarios
            .AsNoTracking()
            .Where(f => f.Activo)
            .OrderBy(f => f.Nombre)
            .ToListAsync();

        var nombres = await _db.Funcionarios
            .AsNoTracking()
            .ToDictionaryAsync(f => f.Id, f => f.Nombre);

        var todas = await _db.FichasMedicasFuncionario
            .AsNoTracking()
            .OrderByDescending(f => f.RegistradoEn)
            .ThenByDescending(f => f.Id)
            .ToListAsync();

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var texto = (busqueda ?? string.Empty).Trim().ToLowerInvariant();
        var estadoFiltro = (estado ?? "todas").Trim().ToLowerInvariant();

        var fichas = todas.Where(f =>
        {
            if (funcionarioId != 0 && f.FuncionarioId != funcionarioId)
            {
                return false;
            }

            if (estadoFiltro == "vigentes" && !(f.VigenteHasta == null || f.VigenteHasta >= hoy))
            {
                return false;
            }

            if (estadoFiltro == "vencidas" && !(f.VigenteHasta != null && f.VigenteHasta < hoy))
            {
                return false;
            }

            if (estadoFiltro == "sin-fecha" && f.VigenteHasta != null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(texto))
            {
                return true;
            }

            var nombre = nombres.GetValueOrDefault(f.FuncionarioId, string.Empty).ToLowerInvariant();
            var tipo = (f.Tipo ?? string.Empty).ToLowerInvariant();
            var descripcion = (f.Descripcion ?? string.Empty).ToLowerInvariant();
            var emisor = (f.Emisor ?? string.Empty).ToLowerInvariant();

            return nombre.Contains(texto) || tipo.Contains(texto)
                || descripcion.Contains(texto) || emisor.Contains(texto);
        }).ToList();

        var modelo = new FichasMedicasPersonal
        {
            Busqueda = busqueda ?? string.Empty,
            FuncionarioFiltro = funcionarioId,
            EstadoFiltro = estadoFiltro,
            TotalFichas = todas.Count,
            TotalFuncionariosConFicha = todas.Select(f => f.FuncionarioId).Distinct().Count(),
            Funcionarios = funcionarios
                .Select(f => new FichasMedicasPersonal.FuncionarioOpcion
                {
                    Id = f.Id,
                    Nombre = f.Nombre,
                })
                .ToList(),
            Fichas = fichas
                .Select(f => new FichasMedicasPersonal.FichaMedicaItem
                {
                    Id = f.Id,
                    FuncionarioId = f.FuncionarioId,
                    Funcionario = nombres.GetValueOrDefault(f.FuncionarioId, $"#{f.FuncionarioId}"),
                    Tipo = f.Tipo,
                    Descripcion = f.Descripcion,
                    Emisor = f.Emisor,
                    EmitidoEn = f.EmitidoEn,
                    VigenteHasta = f.VigenteHasta,
                    ArchivoNombre = f.ArchivoNombre,
                    TieneArchivo = !string.IsNullOrWhiteSpace(f.ArchivoRuta),
                    RegistradoEn = f.RegistradoEn,
                })
                .ToList(),
        };

        await _auditoria.RegistrarAsync("LEER", "SALUD", "ficha_medica_funcionario");

        return View(modelo);
    }

    [HttpGet("nuevo")]
    public async Task<IActionResult> Nuevo()
    {
        var funcionarios = await _db.Funcionarios
            .AsNoTracking()
            .Where(f => f.Activo)
            .OrderBy(f => f.Nombre)
            .ToListAsync();

        var modelo = new FichasMedicasPersonal
        {
            Funcionarios = funcionarios
                .Select(f => new FichasMedicasPersonal.FuncionarioOpcion { Id = f.Id, Nombre = f.Nombre })
                .ToList(),
        };

        return View(modelo);
    }

    [HttpPost("crear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(
        int funcionarioId,
        string tipo,
        string? descripcion,
        string? emisor,
        string? emitidoEn,
        string? vigenteHasta,
        IFormFile? archivo)
    {
        if (FuncionarioActualId == 0)
        {
            TempData["Aviso"] = "Inicie sesión con acceso a Personal (UTP o Dirección) para registrar la ficha médica.";
            return RedirectToAction(nameof(Index));
        }

        var funcionario = await _db.Funcionarios
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == funcionarioId && f.Activo);

        if (funcionario is null)
        {
            TempData["Aviso"] = "Seleccione un funcionario activo.";
            return RedirectToAction(nameof(Index));
        }

        var tipoLimpio = (tipo ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(tipoLimpio))
        {
            TempData["Aviso"] = "Indique el tipo de documento (certificado, licencia, ficha médica, etc.).";
            return RedirectToAction(nameof(Index));
        }

        ResultadoArchivo? guardado = null;

        if (archivo is not null && archivo.Length > 0)
        {
            var error = ExpedienteDocumentos.Validar(archivo);

            if (error is not null)
            {
                TempData["Aviso"] = error;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                guardado = await _documentos.GuardarEnAsync(
                    archivo,
                    $"funcionarios/{funcionario.Id}",
                    HttpContext.RequestAborted);
            }
            catch (ArchivoInvalidoException ex)
            {
                TempData["Aviso"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        var ficha = new FichaMedicaFuncionario
        {
            FuncionarioId = funcionario.Id,
            Tipo = tipoLimpio[..Math.Min(80, tipoLimpio.Length)],
            Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
            Emisor = string.IsNullOrWhiteSpace(emisor)
                ? null
                : emisor.Trim()[..Math.Min(150, emisor.Trim().Length)],
            EmitidoEn = DateOnly.TryParse(emitidoEn, out var emitido) ? emitido : null,
            VigenteHasta = DateOnly.TryParse(vigenteHasta, out var vence) ? vence : null,
            ArchivoRuta = guardado?.Ruta,
            ArchivoNombre = guardado?.Nombre,
            RegistradoPor = FuncionarioActualId,
            RegistradoEn = DateTimeOffset.UtcNow,
        };

        try
        {
            await _escritura.EjecutarAsync(FuncionarioActualId, ct =>
            {
                _db.FichasMedicasFuncionario.Add(ficha);
                return Task.CompletedTask;
            }, HttpContext.RequestAborted);
        }
        catch (Exception)
        {
            if (guardado is not null)
            {
                _documentos.Eliminar(guardado.Ruta);
            }

            TempData["Aviso"] = "No se pudo guardar la ficha médica. Intente nuevamente.";
            return RedirectToAction(nameof(Index));
        }

        await _auditoria.RegistrarAsync(
            "CREAR",
            "SALUD",
            "ficha_medica_funcionario",
            detalle: $"{{\"evento\":\"crear_ficha_medica\",\"funcionario\":{funcionario.Id}}}",
            recursoId: ficha.Id);

        TempData["Notificacion"] = $"Ficha médica de {funcionario.Nombre} registrada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("editar/{id:int}")]
    public async Task<IActionResult> Editar(int id)
    {
        var ficha = await _db.FichasMedicasFuncionario
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (ficha is null)
        {
            return NotFound();
        }

        var funcionarios = await _db.Funcionarios
            .AsNoTracking()
            .Where(f => f.Activo)
            .OrderBy(f => f.Nombre)
            .ToListAsync();

        await _auditoria.RegistrarAsync(
            "LEER", "SALUD", "ficha_medica_funcionario",
            detalle: "{\"evento\":\"abrir_editar_ficha_medica\"}", recursoId: id);

        ViewData["Funcionarios"] = funcionarios
            .Select(f => new FichasMedicasPersonal.FuncionarioOpcion { Id = f.Id, Nombre = f.Nombre })
            .ToList();

        return View(ficha);
    }

    [HttpPost("editar/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        int id,
        int funcionarioId,
        string tipo,
        string? descripcion,
        string? emisor,
        string? emitidoEn,
        string? vigenteHasta,
        IFormFile? archivo)
    {
        if (FuncionarioActualId == 0)
        {
            TempData["Aviso"] = "Inicie sesión con acceso a Personal (UTP o Dirección) para editar la ficha médica.";
            return RedirectToAction(nameof(Index));
        }

        var ficha = await _db.FichasMedicasFuncionario
            .FirstOrDefaultAsync(f => f.Id == id);

        if (ficha is null)
        {
            return NotFound();
        }

        var funcionario = await _db.Funcionarios
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == funcionarioId && f.Activo);

        if (funcionario is null)
        {
            TempData["Aviso"] = "Seleccione un funcionario activo.";
            return RedirectToAction(nameof(Editar), new { id });
        }

        var tipoLimpio = (tipo ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(tipoLimpio))
        {
            TempData["Aviso"] = "Indique el tipo de documento (certificado, licencia, ficha médica, etc.).";
            return RedirectToAction(nameof(Editar), new { id });
        }

        ResultadoArchivo? guardado = null;

        if (archivo is not null && archivo.Length > 0)
        {
            var error = ExpedienteDocumentos.Validar(archivo);

            if (error is not null)
            {
                TempData["Aviso"] = error;
                return RedirectToAction(nameof(Editar), new { id });
            }

            try
            {
                guardado = await _documentos.GuardarEnAsync(
                    archivo,
                    $"funcionarios/{funcionario.Id}",
                    HttpContext.RequestAborted);
            }
            catch (ArchivoInvalidoException ex)
            {
                TempData["Aviso"] = ex.Message;
                return RedirectToAction(nameof(Editar), new { id });
            }
        }

        try
        {
            await _escritura.EjecutarAsync(FuncionarioActualId, ct =>
            {
                ficha.FuncionarioId = funcionario.Id;
                ficha.Tipo = tipoLimpio[..Math.Min(80, tipoLimpio.Length)];
                ficha.Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
                ficha.Emisor = string.IsNullOrWhiteSpace(emisor)
                    ? null
                    : emisor.Trim()[..Math.Min(150, emisor.Trim().Length)];
                ficha.EmitidoEn = DateOnly.TryParse(emitidoEn, out var emitido) ? emitido : null;
                ficha.VigenteHasta = DateOnly.TryParse(vigenteHasta, out var vence) ? vence : null;

                if (guardado is not null)
                {
                    var anterior = ficha.ArchivoRuta;
                    ficha.ArchivoRuta = guardado.Ruta;
                    ficha.ArchivoNombre = guardado.Nombre;

                    if (!string.IsNullOrWhiteSpace(anterior))
                    {
                        _documentos.Eliminar(anterior);
                    }
                }

                return Task.CompletedTask;
            }, HttpContext.RequestAborted);
        }
        catch (Exception)
        {
            if (guardado is not null)
            {
                _documentos.Eliminar(guardado.Ruta);
            }

            TempData["Aviso"] = "No se pudo guardar la ficha médica. Intente nuevamente.";
            return RedirectToAction(nameof(Editar), new { id });
        }

        await _auditoria.RegistrarAsync(
            "EDITAR",
            "SALUD",
            "ficha_medica_funcionario",
            detalle: $"{{\"evento\":\"editar_ficha_medica\",\"funcionario\":{funcionario.Id}}}",
            recursoId: ficha.Id);

        TempData["Notificacion"] = $"Ficha médica de {funcionario.Nombre} actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("descargar/{id:int}")]
    public async Task<IActionResult> Descargar(int id)
    {
        var ficha = await _db.FichasMedicasFuncionario
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (ficha?.ArchivoRuta is null)
        {
            return NotFound();
        }

        var ruta = _documentos.RutaFisica(ficha.ArchivoRuta);

        if (ruta is null)
        {
            return NotFound();
        }

        var mime = ExpedienteDocumentos.MimePermitido(Path.GetExtension(ficha.ArchivoRuta))
            ?? "application/octet-stream";

        await _auditoria.RegistrarAsync(
            "LEER",
            "SALUD",
            "ficha_medica_funcionario",
            detalle: "{\"evento\":\"descargar_ficha_medica\"}",
            recursoId: id);

        return PhysicalFile(ruta, mime, ficha.ArchivoNombre ?? Path.GetFileName(ruta));
    }
}
