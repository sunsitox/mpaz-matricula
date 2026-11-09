using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Reportes autorizados (MAT-16): estado de las matrículas del año y
/// requisitos documentales pendientes, en pantalla y en CSV. El estado usa
/// los mismos datos del listado (GENERAL:LEER); el detalle documental exige
/// DOCUMENTOS:LEER y respeta la sensibilidad por área (S-02).
/// </summary>
[Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:LEER")]
public class ReportesController : Controller
{
    private static readonly UTF8Encoding CsvEncoding = new(true);

    private static readonly string[] OrdenEstados =
    [
        "BORRADOR", "FICHA_GENERADA", "FIRMADA", "CONFIRMADA", "ANULADA",
    ];

    private readonly MatriculaDbContext _db;
    private readonly AnioEscolar _anios;
    private readonly IAuditoriaAcceso _auditoria;

    public ReportesController(MatriculaDbContext db, AnioEscolar anios, IAuditoriaAcceso auditoria)
    {
        _db = db;
        _anios = anios;
        _auditoria = auditoria;
    }

    private bool Puede(string area) => User.HasClaim("permiso", $"{area}:LEER");

    private async Task<int?> ResolverAnioAsync(int? anio)
    {
        var resuelto = await _anios.ResolverAsync(anio);

        if (resuelto is null)
        {
            TempData["Aviso"] =
                "No hay un año escolar con matrícula activa. Secretaría o Dirección debe definirlo en Configuración → Año escolar.";
        }

        return resuelto;
    }

    private static FileContentResult Csv(string contenido, string nombre) =>
        new(CsvEncoding.GetBytes(contenido), "text/csv") { FileDownloadName = nombre };

    [HttpGet]
    public async Task<IActionResult> Matriculas(int? anio)
    {
        var anioConsulta = await ResolverAnioAsync(anio);

        if (anioConsulta is null)
        {
            return RedirectToAction("Index", "Alumnos");
        }

        return View(await ArmarMatriculasAsync(anioConsulta.Value));
    }

    // MAT-12: descargar exige EXPORTAR, distinto de LEER. Así un rol puede
    // ver el reporte en pantalla sin poder llevarse el archivo.
    [HttpGet]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:EXPORTAR")]
    public async Task<IActionResult> MatriculasCsv(int? anio)
    {
        var anioConsulta = await ResolverAnioAsync(anio);

        if (anioConsulta is null)
        {
            return RedirectToAction("Index", "Alumnos");
        }

        var reporte = await ArmarMatriculasAsync(anioConsulta.Value);

        await _auditoria.RegistrarAsync(
            "DESCARGAR",
            "REPORTE",
            "matriculas",
            detalle: $"{{\"anio\":{reporte.Anio}}}");

        return Csv(CsvReporte.Matriculas(reporte.Filas), $"reporte-matriculas-{reporte.Anio}.csv");
    }

    [HttpGet]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "DOCUMENTOS:LEER")]
    public async Task<IActionResult> Faltantes(int? anio)
    {
        var anioConsulta = await ResolverAnioAsync(anio);

        if (anioConsulta is null)
        {
            return RedirectToAction("Index", "Alumnos");
        }

        return View(await ArmarFaltantesAsync(anioConsulta.Value));
    }

    [HttpGet]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "DOCUMENTOS:LEER")]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "DOCUMENTOS:EXPORTAR")]
    public async Task<IActionResult> FaltantesCsv(int? anio)
    {
        var anioConsulta = await ResolverAnioAsync(anio);

        if (anioConsulta is null)
        {
            return RedirectToAction("Index", "Alumnos");
        }

        var reporte = await ArmarFaltantesAsync(anioConsulta.Value);

        await _auditoria.RegistrarAsync(
            "DESCARGAR",
            "REPORTE",
            "faltantes",
            detalle: $"{{\"anio\":{reporte.Anio}}}");

        return Csv(CsvReporte.Faltantes(reporte.Filas), $"reporte-faltantes-{reporte.Anio}.csv");
    }

    private async Task<ReporteMatriculas> ArmarMatriculasAsync(int anioConsulta)
    {
        var anios = await _anios.ListarAsync();

        var estudiantes = await _db.Estudiantes
            .AsNoTracking()
            .OrderBy(e => e.ApellidoPaterno)
            .ThenBy(e => e.Nombres)
            .Select(e => new { e.Id, e.NombreCompleto, e.Identificador })
            .ToListAsync();

        var matriculas = await _db.Matriculas
            .AsNoTracking()
            .Where(m => m.Anio == anioConsulta)
            .Select(m => new
            {
                m.Id,
                m.EstudianteId,
                m.Estado,
                m.Tipo,
                Curso = m.Curso != null ? m.Curso.Nombre : null,
            })
            .ToListAsync();

        var idsMatriculas = matriculas.Select(m => m.Id).ToList();

        var folios = idsMatriculas.Count == 0
            ? new Dictionary<int, string>()
            : await _db.FichaVersiones
                .AsNoTracking()
                .Where(v => idsMatriculas.Contains(v.MatriculaId) && v.Estado == "VIGENTE")
                .GroupBy(v => v.MatriculaId)
                .Select(grupo => new { MatriculaId = grupo.Key, Folio = grupo.OrderByDescending(v => v.NumeroVersion).First().Folio })
                .ToDictionaryAsync(x => x.MatriculaId, x => x.Folio);

        var pendientes = idsMatriculas.Count == 0
            ? new Dictionary<int, int>()
            : await _db.RequisitosDocumentales
                .AsNoTracking()
                .Where(r => idsMatriculas.Contains(r.MatriculaId) && r.Estado == "PENDIENTE")
                .GroupBy(r => r.MatriculaId)
                .Select(grupo => new { MatriculaId = grupo.Key, Total = grupo.Count() })
                .ToDictionaryAsync(x => x.MatriculaId, x => x.Total);

        var delAnio = matriculas
            .GroupBy(m => m.EstudianteId)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.First());

        var reporte = new ReporteMatriculas
        {
            Anio = anioConsulta,
            Anios = anios.Select(opcion => opcion.Anio).ToList(),
        };

        foreach (var estado in OrdenEstados)
        {
            reporte.Resumen.Add(new ReporteMatriculas.ResumenEstado
            {
                Estado = estado,
                Total = matriculas.Count(m => m.Estado == estado),
            });
        }

        reporte.Resumen.Add(new ReporteMatriculas.ResumenEstado
        {
            Estado = "Sin matrícula",
            Total = estudiantes.Count(e => !delAnio.ContainsKey(e.Id)),
        });

        foreach (var estudiante in estudiantes)
        {
            if (!delAnio.TryGetValue(estudiante.Id, out var matricula))
            {
                reporte.Filas.Add(new ReporteMatriculas.FilaMatricula
                {
                    EstudianteId = estudiante.Id,
                    Estudiante = estudiante.NombreCompleto,
                    Rut = RutChileno.Formatear(estudiante.Identificador),
                    Estado = "Sin matrícula",
                    Tipo = "—",
                });

                continue;
            }

            reporte.Filas.Add(new ReporteMatriculas.FilaMatricula
            {
                EstudianteId = estudiante.Id,
                Estudiante = estudiante.NombreCompleto,
                Rut = RutChileno.Formatear(estudiante.Identificador),
                Curso = matricula.Curso,
                Estado = matricula.Estado,
                Tipo = matricula.Tipo,
                Folio = folios.GetValueOrDefault(matricula.Id),
                DocsPendientes = pendientes.GetValueOrDefault(matricula.Id),
            });
        }

        return reporte;
    }

    private async Task<ReporteFaltantes> ArmarFaltantesAsync(int anioConsulta)
    {
        var anios = await _anios.ListarAsync();

        var matriculas = await _db.Matriculas
            .AsNoTracking()
            .Where(m => m.Anio == anioConsulta)
            .Select(m => new
            {
                m.Id,
                m.EstudianteId,
                Curso = m.Curso != null ? m.Curso.Nombre : null,
            })
            .ToListAsync();

        var idsEstudiantes = matriculas.Select(m => m.EstudianteId).Distinct().ToList();

        var estudiantes = idsEstudiantes.Count == 0
            ? new Dictionary<int, (string Nombre, string Rut)>()
            : await _db.Estudiantes
                .AsNoTracking()
                .Where(e => idsEstudiantes.Contains(e.Id))
                .Select(e => new { e.Id, e.NombreCompleto, e.Identificador })
                .ToDictionaryAsync(e => e.Id, e => (Nombre: e.NombreCompleto, Rut: e.Identificador));

        var porMatricula = matriculas.ToDictionary(m => m.Id);
        var idsMatriculas = porMatricula.Keys.ToList();

        var requisitos = idsMatriculas.Count == 0
            ? new List<(int MatriculaId, string Tipo, DateTimeOffset RegistradoEn)>()
            : await _db.RequisitosDocumentales
                .AsNoTracking()
                .Where(r => idsMatriculas.Contains(r.MatriculaId) && r.Estado == "PENDIENTE")
                .Select(r => new ValueTuple<int, string, DateTimeOffset>(r.MatriculaId, r.TipoDocumento, r.RegistradoEn))
                .ToListAsync();

        var reporte = new ReporteFaltantes
        {
            Anio = anioConsulta,
            Anios = anios.Select(opcion => opcion.Anio).ToList(),
        };

        foreach (var (matriculaId, tipo, registradoEn) in requisitos)
        {
            var sensibilidad = CatalogoDocumentos.Sensibilidad(tipo);

            if (!CatalogoDocumentos.VisiblePara(sensibilidad, area => Puede(area)))
            {
                continue;
            }

            var matricula = porMatricula[matriculaId];
            var estudiante = estudiantes.GetValueOrDefault(matricula.EstudianteId);

            reporte.Filas.Add(new ReporteFaltantes.FilaFaltante
            {
                EstudianteId = matricula.EstudianteId,
                Estudiante = estudiante.Nombre ?? $"Estudiante {matricula.EstudianteId}",
                Rut = RutChileno.Formatear(estudiante.Rut ?? string.Empty),
                Curso = matricula.Curso,
                TipoDocumento = tipo,
                Etiqueta = CatalogoDocumentos.Etiqueta(tipo),
                RegistradoEn = registradoEn,
            });
        }

        reporte.Filas = reporte.Filas
            .OrderBy(f => f.Estudiante)
            .ThenBy(f => f.Etiqueta)
            .ToList();

        return reporte;
    }
}
