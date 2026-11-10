using System.Globalization;
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
/// Certificado de alumno regular: se emite desde la matrícula vigente del
/// año activo y se valida en forma pública por folio y código. Cada emisión
/// deja registro con folio único; la anulación le quita validez.
/// </summary>
public class CertificadosController : Controller
{
    private static readonly CultureInfo EspanolChile = new("es-CL");

    private readonly MatriculaDbContext _db;
    private readonly CertificadoAlumnoRegularPdf _pdf;
    private readonly AnioEscolar _anios;
    private readonly EscrituraAuditada _escritura;
    private readonly IAuditoriaAcceso _auditoria;
    private readonly DatosInstitucion _institucion;
    private readonly IWebHostEnvironment _entorno;

    public CertificadosController(
        MatriculaDbContext db,
        CertificadoAlumnoRegularPdf pdf,
        AnioEscolar anios,
        EscrituraAuditada escritura,
        IAuditoriaAcceso auditoria,
        IOptions<DatosInstitucion> institucion,
        IWebHostEnvironment entorno)
    {
        _db = db;
        _pdf = pdf;
        _anios = anios;
        _escritura = escritura;
        _auditoria = auditoria;
        _institucion = institucion.Value;
        _entorno = entorno;
    }

    /// <summary>
    /// Emite el certificado del alumno para el año activo. Exige matrícula
    /// vigente apta: firmada, confirmada o validada por Dirección (nunca
    /// borrador ni anulada).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:LEER")]
    public async Task<IActionResult> Emitir(int id, string? proposito = null, bool descargar = false)
    {
        var anioActivo = await _anios.ActivoAsync();

        if (anioActivo is null)
        {
            TempData["Aviso"] = "No hay un año escolar con matrícula activa.";
            return RedirectToAction("Detalle", "Alumnos", new { id });
        }

        var estudiante = await _db.Estudiantes
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (estudiante is null)
        {
            return NotFound();
        }

        var matricula = await _db.Matriculas
            .AsNoTracking()
            .Include(m => m.Curso)
            .FirstOrDefaultAsync(m =>
                m.EstudianteId == id
                && m.Anio == anioActivo.Value
                && m.Estado != "ANULADA"
                && (m.Estado == "FIRMADA" || m.Estado == "CONFIRMADA" || m.ValidadaEn != null));

        if (matricula is null)
        {
            TempData["Aviso"] = "El alumno no tiene matrícula apta para certificar este año (firmada, confirmada o validada).";
            return RedirectToAction("Detalle", "Alumnos", new { id });
        }

        var funcionarioId = int.TryParse(User.FindFirstValue("funcionario_id"), out var fid) ? fid : (int?)null;

        if (funcionarioId is null)
        {
            TempData["Aviso"] = "Inicie sesión como funcionario para emitir el certificado.";
            return RedirectToAction("Detalle", "Alumnos", new { id });
        }

        var codigo = EmisionCertificado.GenerarCodigo();
        var propositoFinal = string.IsNullOrWhiteSpace(proposito)
            ? EmisionCertificado.PropositoGenerico
            : proposito.Trim()[..Math.Min(200, proposito.Trim().Length)];

        var emitido = new CertificadoEmitido
        {
            Folio = $"CAR-{anioActivo.Value}-TMP-{Guid.NewGuid():N}",
            CodigoHash = EmisionCertificado.HashCodigo(codigo),
            MatriculaId = matricula.Id,
            EstudianteId = id,
            Anio = (short)anioActivo.Value,
            EmitidoPor = funcionarioId,
            EmitidoEn = DateTimeOffset.UtcNow,
            Proposito = propositoFinal,
        };

        await _escritura.EjecutarAsync(funcionarioId.Value, async ct =>
        {
            _db.CertificadosEmitidos.Add(emitido);
            await _db.SaveChangesAsync(ct);
            emitido.Folio = EmisionCertificado.FormatoFolio(anioActivo.Value, emitido.Id);
        }, HttpContext.RequestAborted);

        await _auditoria.RegistrarAsync(
            "EMITIR",
            "DOCUMENTOS",
            "certificado_emitido",
            detalle: $"{{\"evento\":\"emitir_certificado\",\"folio\":\"{emitido.Folio}\"}}",
            estudianteId: id,
            recursoId: emitido.Id);

        var firmante = _institucion.FirmanteNombre;

        if (string.IsNullOrWhiteSpace(firmante))
        {
            firmante = await _db.Funcionarios
                .AsNoTracking()
                .Where(f => f.Id == funcionarioId.Value)
                .Select(f => f.Nombre)
                .FirstOrDefaultAsync() ?? "La Dirección";
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var baseValidacion = string.IsNullOrWhiteSpace(_institucion.UrlValidacion)
            ? $"{Request.Scheme}://{Request.Host}/Certificados/Validar"
            : _institucion.UrlValidacion.Trim().TrimEnd('/');
        var urlValidacion =
            $"{baseValidacion}?folio={Uri.EscapeDataString(emitido.Folio)}&codigo={Uri.EscapeDataString(codigo)}";

        var documento = new DocumentoCertificado
        {
            Sostenedor = _institucion.Sostenedor,
            Establecimiento = _institucion.Establecimiento,
            Rbd = _institucion.Rbd,
            Reconocimiento = _institucion.LineaReconocimiento,
            Direccion = _institucion.Direccion,
            Contacto = _institucion.Contacto,
            AlumnoNombre = estudiante.NombreCompleto,
            AlumnoRut = string.Equals(estudiante.TipoIdentificador, "RUN", StringComparison.OrdinalIgnoreCase)
                ? RutChileno.Formatear(estudiante.Identificador)
                : estudiante.Identificador.ToUpperInvariant(),
            FechaNacimiento = estudiante.FechaNacimiento.ToString("d 'de' MMMM 'de' yyyy", EspanolChile),
            Curso = matricula.Curso?.Nombre ?? "sin curso asignado",
            Anio = anioActivo.Value,
            Proposito = propositoFinal,
            LugarFecha = $"{_institucion.LugarEmision}, {hoy.ToString("d 'de' MMMM 'de' yyyy", EspanolChile)}",
            FirmanteNombre = firmante,
            FirmanteCargo = _institucion.FirmanteCargo,
            Folio = emitido.Folio,
            Codigo = codigo,
            UrlValidacion = urlValidacion,
            Qr = GeneradorQr.Png(urlValidacion),
            Logo = LogoInstitucional(),
        };

        var archivo = _pdf.Generar(documento);

        return descargar
            ? File(archivo, "application/pdf", $"certificado-alumno-regular-{emitido.Folio}.pdf")
            : File(archivo, "application/pdf");
    }

    /// <summary>
    /// Validación pública: exige folio y código juntos; un documento anulado
    /// o un código distinto responde inválido sin revelar datos del alumno.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Validar(string? folio, string? codigo)
    {
        var modelo = new ValidacionCertificado { Folio = folio?.Trim() ?? string.Empty };

        if (!string.IsNullOrWhiteSpace(folio) && !string.IsNullOrWhiteSpace(codigo))
        {
            var emitido = await _db.CertificadosEmitidos
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Folio == folio.Trim());

            if (emitido is not null
                && emitido.AnuladoEn is null
                && emitido.CodigoHash == EmisionCertificado.HashCodigo(codigo))
            {
                var estudiante = await _db.Estudiantes
                    .AsNoTracking()
                    .Where(e => e.Id == emitido.EstudianteId)
                    .Select(e => new { e.NombreCompleto })
                    .FirstOrDefaultAsync();

                var curso = await _db.Matriculas
                    .AsNoTracking()
                    .Where(m => m.Id == emitido.MatriculaId)
                    .Select(m => m.Curso != null ? m.Curso.Nombre : null)
                    .FirstOrDefaultAsync();

                modelo = modelo with
                {
                    Valido = true,
                    Establecimiento = _institucion.Establecimiento,
                    AlumnoNombre = estudiante?.NombreCompleto,
                    Curso = curso,
                    Anio = emitido.Anio,
                    EmitidoEn = emitido.EmitidoEn,
                };
            }
        }

        return View(modelo);
    }

    /// <summary>
    /// Lee el escudo desde wwwroot (el mismo PNG con lema de la ficha);
    /// si falta, el certificado se genera sin logo.
    /// </summary>
    private byte[]? LogoInstitucional()
    {
        var ruta = Path.Combine(_entorno.WebRootPath, "images", "logo-mpaz-con-lema.png");

        return System.IO.File.Exists(ruta) ? System.IO.File.ReadAllBytes(ruta) : null;
    }
}