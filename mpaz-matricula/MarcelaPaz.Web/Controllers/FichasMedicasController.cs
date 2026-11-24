using System.Security.Claims;
using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Infrastructure.Documentos;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Fichas de salud de los funcionarios. Es información sensible del personal:
/// la sección «Personal» queda reservada a UTP y Dirección, tanto en la vista
/// como en el servidor. La ficha se completa en el portal y su documento se
/// genera desde aquí; no se suben archivos.
/// </summary>
[Authorize(Roles = "UTP,DIRECCION")]
[Route("personal/fichas-medicas")]
public class FichasMedicasController : Controller
{
    private readonly MatriculaDbContext _db;
    private readonly FichaSaludFuncionarioPdf _pdf;
    private readonly EscrituraAuditada _escritura;
    private readonly IAuditoriaAcceso _auditoria;
    private readonly DatosInstitucion _institucion;
    private readonly IWebHostEnvironment _entorno;

    public FichasMedicasController(
        MatriculaDbContext db,
        FichaSaludFuncionarioPdf pdf,
        EscrituraAuditada escritura,
        IAuditoriaAcceso auditoria,
        IOptions<DatosInstitucion> institucion,
        IWebHostEnvironment entorno)
    {
        _db = db;
        _pdf = pdf;
        _escritura = escritura;
        _auditoria = auditoria;
        _institucion = institucion.Value;
        _entorno = entorno;
    }

    private int FuncionarioActualId =>
        int.TryParse(User.FindFirstValue("funcionario_id"), out var id) ? id : 0;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? busqueda, int funcionarioId = 0)
    {
        var funcionarios = await _db.Funcionarios
            .AsNoTracking()
            .Where(f => f.Activo)
            .OrderBy(f => f.Nombre)
            .ToListAsync();

        var nombres = await _db.Funcionarios
            .AsNoTracking()
            .ToDictionaryAsync(f => f.Id, f => f.Nombre);

        var todas = await _db.FichasSaludFuncionario
            .AsNoTracking()
            .OrderByDescending(f => f.RegistradoEn)
            .ThenByDescending(f => f.Id)
            .ToListAsync();

        var texto = (busqueda ?? string.Empty).Trim().ToLowerInvariant();

        var fichas = todas.Where(f =>
        {
            if (funcionarioId != 0 && f.FuncionarioId != funcionarioId)
            {
                return false;
            }

            if (string.IsNullOrEmpty(texto))
            {
                return true;
            }

            var nombre = nombres.GetValueOrDefault(f.FuncionarioId, string.Empty).ToLowerInvariant();

            return nombre.Contains(texto)
                || (f.Rut ?? string.Empty).ToLowerInvariant().Contains(texto)
                || (f.Pasaporte ?? string.Empty).ToLowerInvariant().Contains(texto)
                || (f.Contacto1Nombre ?? string.Empty).ToLowerInvariant().Contains(texto)
                || (f.SistemaSalud ?? string.Empty).ToLowerInvariant().Contains(texto)
                || (f.CentroMedicoPreferente ?? string.Empty).ToLowerInvariant().Contains(texto);
        }).ToList();

        var conFicha = todas.Select(f => f.FuncionarioId).Distinct().ToHashSet();

        var modelo = new FichasMedicasPersonal
        {
            Busqueda = busqueda ?? string.Empty,
            FuncionarioFiltro = funcionarioId,
            TotalRegistros = todas.Count,
            TotalFuncionariosConFicha = conFicha.Count,
            TotalFuncionariosActivos = funcionarios.Count,
            Funcionarios = Opciones(funcionarios),
            SinFicha = funcionarios
                .Where(f => !conFicha.Contains(f.Id))
                .Select(f => new FichasMedicasPersonal.FuncionarioOpcion
                {
                    Id = f.Id,
                    Nombre = f.Nombre,
                })
                .ToList(),
            Fichas = fichas
                .Select(f => new FichasMedicasPersonal.FichaSaludItem
                {
                    Id = f.Id,
                    FuncionarioId = f.FuncionarioId,
                    Funcionario = nombres.GetValueOrDefault(f.FuncionarioId, $"#{f.FuncionarioId}"),
                    Documento = Documento(RutChileno.Formatear(f.Rut), f.Pasaporte),
                    Telefono = f.TelefonoPersonal,
                    ContactoEmergencia = Contacto(f.Contacto1Nombre, f.Contacto1Telefono),
                    SistemaSalud = f.SistemaSalud,
                    RegistradoEn = f.RegistradoEn,
                })
                .ToList(),
        };

        await _auditoria.RegistrarAsync("LEER", "SALUD", "ficha_salud_funcionario");

        return View(modelo);
    }

    [HttpGet("nuevo")]
    public async Task<IActionResult> Nuevo(int funcionarioId = 0)
    {
        await PrepararFormularioAsync();

        var modelo = new FichaSaludFuncionario();

        if (funcionarioId != 0)
        {
            var funcionario = await _db.Funcionarios
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == funcionarioId && f.Activo);

            if (funcionario is not null)
            {
                modelo.FuncionarioId = funcionario.Id;
                modelo.NombreCompleto = funcionario.Nombre;
            }
        }

        return View(modelo);
    }

    [HttpPost("crear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(FichaSaludFuncionario ficha)
    {
        if (FuncionarioActualId == 0)
        {
            TempData["Aviso"] = "Inicie sesión con acceso a Personal (UTP o Dirección) para registrar la ficha de salud.";
            return RedirectToAction(nameof(Index));
        }

        var funcionario = await _db.Funcionarios
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == ficha.FuncionarioId && f.Activo);

        if (funcionario is null)
        {
            TempData["Aviso"] = "Seleccione un funcionario activo.";
            return RedirectToAction(nameof(Index));
        }

        Preparar(ficha, funcionario.Nombre);

        var errores = ValidadorFichaSalud.Validar(ficha);

        if (errores.Count > 0)
        {
            AgregarErrores(errores);
            await PrepararFormularioAsync();
            return View(nameof(Nuevo), ficha);
        }

        ficha.Id = 0;
        ficha.FuncionarioId = funcionario.Id;
        ficha.RegistradoPor = FuncionarioActualId;
        ficha.RegistradoEn = DateTimeOffset.UtcNow;

        try
        {
            await _escritura.EjecutarAsync(FuncionarioActualId, ct =>
            {
                _db.FichasSaludFuncionario.Add(ficha);
                return Task.CompletedTask;
            }, HttpContext.RequestAborted);
        }
        catch (Exception)
        {
            TempData["Aviso"] = "No se pudo guardar la ficha de salud. Intente nuevamente.";
            return RedirectToAction(nameof(Index));
        }

        // El alta queda en historial_cambios por el trigger del paso 26.
        TempData["Notificacion"] = $"Ficha de salud de {funcionario.Nombre} registrada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("editar/{id:int}")]
    public async Task<IActionResult> Editar(int id)
    {
        var ficha = await _db.FichasSaludFuncionario
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (ficha is null)
        {
            return NotFound();
        }

        await PrepararFormularioAsync();

        await _auditoria.RegistrarAsync(
            "LEER", "SALUD", "ficha_salud_funcionario",
            detalle: "{\"evento\":\"abrir_editar_ficha_salud\"}", recursoId: id);

        return View(ficha);
    }

    [HttpPost("editar/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, FichaSaludFuncionario ficha)
    {
        if (FuncionarioActualId == 0)
        {
            TempData["Aviso"] = "Inicie sesión con acceso a Personal (UTP o Dirección) para editar la ficha de salud.";
            return RedirectToAction(nameof(Index));
        }

        var registrada = await _db.FichasSaludFuncionario
            .FirstOrDefaultAsync(f => f.Id == id);

        if (registrada is null)
        {
            return NotFound();
        }

        ficha.Id = id;

        var funcionario = await _db.Funcionarios
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == ficha.FuncionarioId && f.Activo);

        if (funcionario is null)
        {
            TempData["Aviso"] = "Seleccione un funcionario activo.";
            return RedirectToAction(nameof(Editar), new { id });
        }

        Preparar(ficha, funcionario.Nombre);

        var errores = ValidadorFichaSalud.Validar(ficha);

        if (errores.Count > 0)
        {
            AgregarErrores(errores);
            await PrepararFormularioAsync();
            return View(nameof(Editar), ficha);
        }

        try
        {
            await _escritura.EjecutarAsync(FuncionarioActualId, ct =>
            {
                registrada.FuncionarioId = funcionario.Id;
                Copiar(ficha, registrada);
                registrada.RegistradoPor = FuncionarioActualId;
                registrada.RegistradoEn = DateTimeOffset.UtcNow;
                return Task.CompletedTask;
            }, HttpContext.RequestAborted);
        }
        catch (Exception)
        {
            TempData["Aviso"] = "No se pudo guardar la ficha de salud. Intente nuevamente.";
            return RedirectToAction(nameof(Editar), new { id });
        }

        // La edición queda en historial_cambios por el trigger del paso 26.
        TempData["Notificacion"] = $"Ficha de salud de {funcionario.Nombre} actualizada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("ver/{id:int}")]
    public async Task<IActionResult> Ver(int id)
    {
        var ficha = await _db.FichasSaludFuncionario
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (ficha is null)
        {
            return NotFound();
        }

        ViewData["RegistradoPor"] = await NombreFuncionarioAsync(ficha.RegistradoPor);

        await _auditoria.RegistrarAsync(
            "LEER", "SALUD", "ficha_salud_funcionario",
            detalle: "{\"evento\":\"ver_ficha_salud\"}", recursoId: id);

        return View(ficha);
    }

    [HttpGet("pdf/{id:int}")]
    public async Task<IActionResult> Pdf(int id)
    {
        var ficha = await _db.FichasSaludFuncionario
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (ficha is null)
        {
            return NotFound();
        }

        var documento = new DocumentoFichaSalud
        {
            Sostenedor = _institucion.Sostenedor,
            Establecimiento = _institucion.Establecimiento,
            DireccionEstablecimiento = _institucion.Direccion,
            Logo = LogoInstitucional(),
            FuncionarioNombre = ficha.NombreCompleto,
            Rut = RutChileno.Formatear(ficha.Rut),
            Pasaporte = ficha.Pasaporte,
            TelefonoPersonal = ficha.TelefonoPersonal,
            DireccionPersonal = ficha.DireccionPersonal,
            Contacto1Nombre = ficha.Contacto1Nombre,
            Contacto1Telefono = ficha.Contacto1Telefono,
            Contacto2Nombre = ficha.Contacto2Nombre,
            Contacto2Telefono = ficha.Contacto2Telefono,
            SistemaSalud = ficha.SistemaSalud,
            CentroMedicoPreferente = ficha.CentroMedicoPreferente,
            CentroUbicacion = ficha.CentroUbicacion,
            CentroTelefono = ficha.CentroTelefono,
            AmbulanciaConvenio = ficha.AmbulanciaConvenio,
            AmbulanciaTelefono = ficha.AmbulanciaTelefono,
            SeguroComplementario = ficha.SeguroComplementario,
            Otros = ficha.Otros,
            AlergiasMedicamentos = ficha.AlergiasMedicamentos,
            EnfermedadesCronicas = ficha.EnfermedadesCronicas,
            MedicamentosFrecuentes = ficha.MedicamentosFrecuentes,
            AceptaTransfusion = ficha.AceptaTransfusion,
            MotivoTransfusion = ficha.MotivoTransfusion,
            RegistradoPor = await NombreFuncionarioAsync(ficha.RegistradoPor),
            RegistradoEn = ficha.RegistradoEn,
        };

        var archivo = _pdf.Generar(documento);

        await _auditoria.RegistrarAsync(
            "DESCARGAR", "SALUD", "ficha_salud_funcionario",
            detalle: "{\"evento\":\"descargar_ficha_salud_pdf\"}", recursoId: id);

        return File(archivo, "application/pdf", $"ficha-salud-{NombreArchivo(ficha.NombreCompleto)}.pdf");
    }

    private async Task PrepararFormularioAsync()
    {
        var funcionarios = await _db.Funcionarios
            .AsNoTracking()
            .Where(f => f.Activo)
            .OrderBy(f => f.Nombre)
            .ToListAsync();

        ViewData["Funcionarios"] = Opciones(funcionarios);
    }

    private async Task<string> NombreFuncionarioAsync(int? id) =>
        id is null
            ? "sin registro"
            : await _db.Funcionarios
                .AsNoTracking()
                .Where(f => f.Id == id)
                .Select(f => f.Nombre)
                .FirstOrDefaultAsync() ?? $"#{id}";

    private static List<FichasMedicasPersonal.FuncionarioOpcion> Opciones(List<Funcionario> funcionarios) =>
        funcionarios
            .Select(f => new FichasMedicasPersonal.FuncionarioOpcion
            {
                Id = f.Id,
                Nombre = f.Nombre,
            })
            .ToList();

    /// <summary>
    /// Limpia el texto recibido y ajusta el documento y la respuesta de
    /// transfusión a los valores que usan la validación y la base.
    /// </summary>
    private static void Preparar(FichaSaludFuncionario ficha, string nombreFuncionario)
    {
        if (string.IsNullOrWhiteSpace(ficha.NombreCompleto))
        {
            ficha.NombreCompleto = nombreFuncionario;
        }

        ficha.NombreCompleto = Limitar(ficha.NombreCompleto, 150)!;
        ficha.Rut = string.IsNullOrWhiteSpace(ficha.Rut)
            ? null
            : RutChileno.EsValido(ficha.Rut)
                ? Limitar(RutChileno.Limpiar(ficha.Rut), 20)
                : Limitar(ficha.Rut, 20);
        ficha.Pasaporte = Limitar(ficha.Pasaporte, 30);
        ficha.TelefonoPersonal = Limitar(ficha.TelefonoPersonal, 20);
        ficha.DireccionPersonal = Limitar(ficha.DireccionPersonal, 255);
        ficha.Contacto1Nombre = Limitar(ficha.Contacto1Nombre, 150);
        ficha.Contacto1Telefono = Limitar(ficha.Contacto1Telefono, 20);
        ficha.Contacto2Nombre = Limitar(ficha.Contacto2Nombre, 150);
        ficha.Contacto2Telefono = Limitar(ficha.Contacto2Telefono, 20);
        ficha.SistemaSalud = Limitar(ficha.SistemaSalud, 30);
        ficha.CentroMedicoPreferente = Limitar(ficha.CentroMedicoPreferente, 150);
        ficha.CentroUbicacion = Limitar(ficha.CentroUbicacion, 255);
        ficha.CentroTelefono = Limitar(ficha.CentroTelefono, 20);
        ficha.AmbulanciaConvenio = Limitar(ficha.AmbulanciaConvenio, 150);
        ficha.AmbulanciaTelefono = Limitar(ficha.AmbulanciaTelefono, 20);
        ficha.SeguroComplementario = Limitar(ficha.SeguroComplementario, 150);
        ficha.Otros = Limitar(ficha.Otros, 1000);
        ficha.AlergiasMedicamentos = Limitar(ficha.AlergiasMedicamentos, 1000);
        ficha.EnfermedadesCronicas = Limitar(ficha.EnfermedadesCronicas, 1000);
        ficha.MedicamentosFrecuentes = Limitar(ficha.MedicamentosFrecuentes, 1000);
        ficha.MotivoTransfusion = Limitar(ficha.MotivoTransfusion, 1000);
        ficha.AceptaTransfusion = NormalizarSiNo(ficha.AceptaTransfusion);
    }

    private static void Copiar(FichaSaludFuncionario origen, FichaSaludFuncionario destino)
    {
        destino.NombreCompleto = origen.NombreCompleto;
        destino.Rut = origen.Rut;
        destino.Pasaporte = origen.Pasaporte;
        destino.TelefonoPersonal = origen.TelefonoPersonal;
        destino.DireccionPersonal = origen.DireccionPersonal;
        destino.Contacto1Nombre = origen.Contacto1Nombre;
        destino.Contacto1Telefono = origen.Contacto1Telefono;
        destino.Contacto2Nombre = origen.Contacto2Nombre;
        destino.Contacto2Telefono = origen.Contacto2Telefono;
        destino.SistemaSalud = origen.SistemaSalud;
        destino.CentroMedicoPreferente = origen.CentroMedicoPreferente;
        destino.CentroUbicacion = origen.CentroUbicacion;
        destino.CentroTelefono = origen.CentroTelefono;
        destino.AmbulanciaConvenio = origen.AmbulanciaConvenio;
        destino.AmbulanciaTelefono = origen.AmbulanciaTelefono;
        destino.SeguroComplementario = origen.SeguroComplementario;
        destino.Otros = origen.Otros;
        destino.AlergiasMedicamentos = origen.AlergiasMedicamentos;
        destino.EnfermedadesCronicas = origen.EnfermedadesCronicas;
        destino.MedicamentosFrecuentes = origen.MedicamentosFrecuentes;
        destino.AceptaTransfusion = origen.AceptaTransfusion;
        destino.MotivoTransfusion = origen.MotivoTransfusion;
    }

    private void AgregarErrores(IReadOnlyList<ValidadorFichaSalud.ErrorValidacion> errores)
    {
        foreach (var error in errores)
        {
            ModelState.AddModelError(error.Campo ?? string.Empty, error.Mensaje);
        }
    }

    private static string? Limitar(string? valor, int largo) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim()[..Math.Min(largo, valor.Trim().Length)];

    private static string? NormalizarSiNo(string? valor) =>
        valor?.Trim().ToLowerInvariant() switch
        {
            "si" or "sí" => "Si",
            "no" => "No",
            _ => Limitar(valor, 2),
        };

    private static string Documento(string? rut, string? pasaporte) =>
        !string.IsNullOrWhiteSpace(rut)
            ? $"RUN {rut}"
            : !string.IsNullOrWhiteSpace(pasaporte)
                ? $"Pasaporte {pasaporte}"
                : "—";

    private static string? Contacto(string? nombre, string? telefono) =>
        string.IsNullOrWhiteSpace(nombre)
            ? null
            : string.IsNullOrWhiteSpace(telefono)
                ? nombre.Trim()
                : $"{nombre.Trim()} · {telefono.Trim()}";

    private static string NombreArchivo(string nombre)
    {
        var limpio = new string(nombre
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')
            .ToArray());

        while (limpio.Contains("--", StringComparison.Ordinal))
        {
            limpio = limpio.Replace("--", "-", StringComparison.Ordinal);
        }

        return limpio.Trim('-');
    }

    /// <summary>
    /// Lee el escudo desde wwwroot (el mismo PNG con lema de la ficha);
    /// si falta, el documento se genera sin logo.
    /// </summary>
    private byte[]? LogoInstitucional()
    {
        var ruta = Path.Combine(_entorno.WebRootPath, "images", "logo-mpaz-con-lema.png");

        return System.IO.File.Exists(ruta) ? System.IO.File.ReadAllBytes(ruta) : null;
    }
}
