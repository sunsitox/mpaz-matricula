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
    /// Historial de certificados emitidos, los más recientes primero.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:LEER")]
    public async Task<IActionResult> Index()
    {
        // El historial muestra folio, alumno, curso y propósito de cada
        // certificado: se reserva a quien puede emitirlos, no a cualquier
        // sesión de sólo lectura.
        if (!PuedeEmitir())
        {
            return Forbid();
        }

        var emitidos = await _db.CertificadosEmitidos
            .AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Take(50)
            .ToListAsync();

        var idsEstudiantes = emitidos.Select(c => c.EstudianteId).Distinct().ToList();
        var idsMatriculas = emitidos.Select(c => c.MatriculaId).Distinct().ToList();
        var idsFuncionarios = emitidos
            .Where(c => c.EmitidoPor is not null)
            .Select(c => c.EmitidoPor!.Value)
            .Distinct()
            .ToList();

        var nombres = await _db.Estudiantes
            .AsNoTracking()
            .Where(e => idsEstudiantes.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.NombreCompleto);

        var cursos = await _db.Matriculas
            .AsNoTracking()
            .Where(m => idsMatriculas.Contains(m.Id))
            .Select(m => new { m.Id, Curso = m.Curso != null ? m.Curso.Nombre : null })
            .ToDictionaryAsync(m => m.Id, m => m.Curso);

        var funcionarios = idsFuncionarios.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Funcionarios
                .AsNoTracking()
                .Where(f => idsFuncionarios.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.Nombre);

        return View(emitidos.Select(c => new CertificadoResumen
        {
            Folio = c.Folio,
            AlumnoNombre = nombres.GetValueOrDefault(c.EstudianteId, $"#{c.EstudianteId}"),
            Curso = cursos.GetValueOrDefault(c.MatriculaId),
            Anio = c.Anio,
            EmitidoEn = c.EmitidoEn,
            EmitidoPor = c.EmitidoPor is int por ? funcionarios.GetValueOrDefault(por) : null,
            Anulado = c.AnuladoEn is not null,
        }).ToList());
    }

    /// <summary>
    /// Emite el certificado del alumno para el año activo. Exige matrícula
    /// vigente apta: firmada, confirmada o validada por Dirección (nunca
    /// borrador ni anulada). Es POST porque crea folio y registro: un GET
    /// permitía emitir certificados con sólo navegar a la dirección.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:LEER")]
    public async Task<IActionResult> Emitir(int id, string? proposito = null)
    {
        // DOCENTE y solo-lectura no emiten: exige crear, editar o validar institucional.
        if (!PuedeEmitir())
        {
            return Forbid();
        }

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
            ? $"{Request.Scheme}://{Request.Host}"
            : _institucion.UrlValidacion.Trim().TrimEnd('/');
        var urlValidacion = $"{baseValidacion}/c/{EmisionCertificado.CodigoParaUrl(codigo)}";

        var documento = new DocumentoCertificado
        {
            Sostenedor = _institucion.Sostenedor,
            Establecimiento = _institucion.Establecimiento,
            Rbd = _institucion.Rbd,
            Reconocimiento = _institucion.LineaReconocimiento,
            Direccion = _institucion.Direccion,
            AlumnoNombre = estudiante.NombreCompleto,
            AlumnoDocumento = string.Equals(estudiante.TipoIdentificador, "RUN", StringComparison.OrdinalIgnoreCase)
                ? $"RUN {RutChileno.Formatear(estudiante.Identificador)}"
                : $"{estudiante.TipoIdentificador.Trim().ToUpperInvariant()} {estudiante.Identificador.Trim().ToUpperInvariant()}",
            Curso = matricula.Curso?.Nombre ?? "sin curso asignado",
            Anio = anioActivo.Value,
            MatriculaFutura = anioActivo.Value > hoy.Year,
            LugarFecha = $"{_institucion.LugarEmision}, {hoy.ToString("d 'de' MMMM 'de' yyyy", EspanolChile)}",
            Vigencia = $"Válido por {EmisionCertificado.DiasVigencia} días, hasta el " +
                $"{EmisionCertificado.VenceEn(emitido.EmitidoEn).ToLocalTime().ToString("d 'de' MMMM 'de' yyyy", EspanolChile)}.",
            FirmanteNombre = firmante,
            FirmanteCargo = _institucion.FirmanteCargo,
            Folio = emitido.Folio,
            Codigo = codigo,
            UrlValidacion = urlValidacion,
            Qr = GeneradorQr.Png(urlValidacion),
            Logo = LogoInstitucional(),
        };

        var archivo = _pdf.Generar(documento).Generar();

        return File(archivo, "application/pdf", $"certificado-alumno-regular-{emitido.Folio}.pdf");
    }

    /// <summary>
    /// Validación pública escribiendo folio y código, para quien recibe el
    /// papel y no puede escanear el QR.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Validar(string? folio, string? codigo)
    {
        return View(await ResolverAsync(folio, codigo));
    }

    /// <summary>
    /// Destino corto del QR impreso: <c>/c/CODIGO</c>. El código es aleatorio
    /// y sólo se guarda su hash; un documento anulado responde inválido.
    /// </summary>
    [HttpGet("/c/{codigo}")]
    [AllowAnonymous]
    public async Task<IActionResult> Verificar(string codigo)
    {
        return View("Validar", await ResolverAsync(null, codigo));
    }

    /// <summary>
    /// Busca el certificado por código (y folio, si se escribió) y devuelve lo
    /// justo para confirmar el papel: nada del alumno si no calza.
    /// </summary>
    private async Task<ValidacionCertificado> ResolverAsync(string? folio, string? codigo)
    {
        var modelo = new ValidacionCertificado { Folio = folio?.Trim() ?? string.Empty };

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return modelo;
        }

        var hash = EmisionCertificado.HashCodigo(codigo);
        var consulta = _db.CertificadosEmitidos
            .AsNoTracking()
            .Where(c => c.CodigoHash == hash && c.AnuladoEn == null);

        if (!string.IsNullOrWhiteSpace(folio))
        {
            consulta = consulta.Where(c => c.Folio == folio.Trim());
        }

        var emitido = await consulta.FirstOrDefaultAsync();

        if (emitido is null)
        {
            return modelo;
        }

        var vence = EmisionCertificado.VenceEn(emitido.EmitidoEn);

        // Vencido no es lo mismo que inexistente: el papel es auténtico y se
        // dice hasta cuándo valió, sin exponer datos del alumno.
        if (!EmisionCertificado.EstaVigente(emitido.EmitidoEn, DateTimeOffset.UtcNow))
        {
            return modelo with
            {
                Vencido = true,
                Folio = emitido.Folio,
                Anio = emitido.Anio,
                EmitidoEn = emitido.EmitidoEn,
                VigenteHasta = vence,
            };
        }

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

        return modelo with
        {
            Valido = true,
            Folio = emitido.Folio,
            Establecimiento = _institucion.Establecimiento,
            AlumnoNombre = estudiante?.NombreCompleto,
            Curso = curso,
            Anio = emitido.Anio,
            EmitidoEn = emitido.EmitidoEn,
            VigenteHasta = vence,
        };
    }

    /// <summary>
    /// DOCENTE y otros roles de sólo lectura no emiten ni consultan el historial:
    /// hace falta crear, editar o validar institucionalmente.
    /// </summary>
    private bool PuedeEmitir() =>
        User.HasClaim("permiso", "GENERAL:CREAR")
        || User.HasClaim("permiso", "GENERAL:EDITAR")
        || User.HasClaim("permiso", "GENERAL:VALIDAR");

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