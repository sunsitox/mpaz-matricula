using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MarcelaPaz.Web.Servicios;

/// <summary>
/// Calcula los indicadores de la portada sobre la base institucional: avance de
/// la matrícula del año, distribución por curso, pendientes documentales y
/// actividad reciente. Sólo devuelve cifras agregadas.
///
/// El panel se abre muchas veces al día y sus cifras cambian poco: los cuatro
/// recuentos de vistas se piden en una sola consulta (antes eran cuatro viajes)
/// y el resultado se guarda 15 segundos en memoria. El responsable se resuelve
/// en la misma consulta que la última matrícula.
/// </summary>
public class PanelIndicadores
{
    /// <summary>Estados en que la matrícula del año ya está cerrada.</summary>
    private static readonly string[] EstadosCerrados = { "FIRMADA", "CONFIRMADA", "FORMALIZADA" };

    /// <summary>Vigencia de las cifras en memoria: corta, para no mostrar datos viejos.</summary>
    private static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(15);

    private const string ClavePanel = "panel:";
    private const string ClaveActividad = "panel:actividad:";

    private readonly MatriculaDbContext _db;
    private readonly IMemoryCache _cache;

    public PanelIndicadores(MatriculaDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<PanelInicio> CargarAsync(int anio, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(ClavePanel + anio, out PanelInicio? enMemoria) && enMemoria is not null)
        {
            return enMemoria;
        }

        var panel = await CalcularAsync(anio, ct);

        _cache.Set(ClavePanel + anio, panel, Vigencia);

        return panel;
    }

    private async Task<PanelInicio> CalcularAsync(int anio, CancellationToken ct)
    {
        var panel = new PanelInicio { Anio = anio };

        var matriculas = await _db.Matriculas
            .AsNoTracking()
            .Where(m => m.Anio == anio)
            .Select(m => new { m.Estado, m.Tipo, Curso = m.Curso != null ? m.Curso.Nombre : null })
            .ToListAsync(ct);

        panel.TotalMatriculas = matriculas.Count;
        panel.Cerradas = matriculas.Count(m => EstadosCerrados.Contains(m.Estado));
        panel.Borradores = matriculas.Count(m => m.Estado == "BORRADOR");
        panel.Renovaciones = matriculas.Count(m => m.Tipo == "RENOVACION");
        panel.Nuevas = matriculas.Count(m => m.Tipo == "NUEVA");

        panel.Cursos = matriculas
            .GroupBy(m => m.Curso ?? "Sin curso")
            .Select(grupo => new PanelInicio.CursoPanel
            {
                Curso = grupo.Key,
                Total = grupo.Count(),
                Cerradas = grupo.Count(m => EstadosCerrados.Contains(m.Estado)),
            })
            // Del nivel más bajo al más alto (pre-kínder primero), no por
            // orden alfabético: «8° B» quedaría antes que «PK A».
            .OrderBy(curso => MarcelaPaz.Domain.Identificacion.NivelesCurso.Orden(curso.Curso))
            .ThenBy(curso => curso.Curso, StringComparer.CurrentCulture)
            .ToList();

        panel.EstudiantesRegistrados = await _db.Estudiantes.CountAsync(ct);
        panel.FuncionariosActivos = await _db.Funcionarios.CountAsync(f => f.Activo, ct);

        // Cuatro recuentos sobre vistas: una sola ida a la base.
        var contadores = await ContarPanelAsync(anio, ct);
        panel.FichasConFolio = contadores.FichasConFolio;
        panel.PendientesDocumentales = contadores.PendientesDocumentales;
        panel.AutorizacionesVigentes = contadores.AutorizacionesVigentes;
        panel.AlertasVencimiento = contadores.AlertasVencimiento;

        // La última matrícula y el nombre de quién la creó, en la misma consulta.
        var ultima = await _db.Matriculas
            .AsNoTracking()
            .Where(m => m.Anio == anio)
            .OrderByDescending(m => m.UpdatedAt)
            .Select(m => new
            {
                m.UpdatedAt,
                Responsable = m.CreadaPor == null
                    ? null
                    : _db.Funcionarios
                        .Where(f => f.Id == m.CreadaPor)
                        .Select(f => f.Nombre)
                        .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        if (ultima is not null)
        {
            panel.ActualizadoEn = ultima.UpdatedAt;
            panel.Responsable = ultima.Responsable;
        }

        return panel;
    }

    /// <summary>
    /// Actividad del portal para su página propia: quién consultó, descargó o
    /// fue rechazado, en qué área y sobre qué recurso. Sólo datos de la sesión
    /// del funcionario, sin contenido de las fichas.
    /// </summary>
    public async Task<List<PanelInicio.AccesoPanel>> ActividadAsync(int limite = 100, CancellationToken ct = default)
    {
        var clave = ClaveActividad + limite;

        if (_cache.TryGetValue(clave, out List<PanelInicio.AccesoPanel>? enMemoria) && enMemoria is not null)
        {
            return enMemoria;
        }

        var accesos = await LeerActividadAsync(limite, ct);

        // La actividad cambia con cada visita: se guarda apenas unos segundos.
        _cache.Set(clave, accesos, TimeSpan.FromSeconds(10));

        return accesos;
    }

    private async Task<List<PanelInicio.AccesoPanel>> LeerActividadAsync(int limite, CancellationToken ct)
    {
        var accesos = await _db.RegistrosAcceso
            .AsNoTracking()
            .OrderByDescending(a => a.OcurridoEn)
            .Take(limite)
            .Select(a => new { a.OcurridoEn, a.Accion, a.Area, a.Recurso, a.FuncionarioId })
            .ToListAsync(ct);

        var ids = accesos
            .Where(a => a.FuncionarioId is not null)
            .Select(a => a.FuncionarioId!.Value)
            .Distinct()
            .ToList();

        var nombres = await _db.Funcionarios
            .AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .Select(f => new { f.Id, f.Nombre })
            .ToDictionaryAsync(f => f.Id, f => f.Nombre, ct);

        return accesos
            .Select(a => new PanelInicio.AccesoPanel
            {
                Cuando = a.OcurridoEn,
                Accion = a.Accion,
                Area = a.Area,
                Recurso = a.Recurso,
                Funcionario = a.FuncionarioId is int id && nombres.TryGetValue(id, out var nombre) ? nombre : null,
            })
            .ToList();
    }

    /// <summary>Resultado de los cuatro recuentos que viajan en una sola consulta.</summary>
    private sealed class ContadoresPanel
    {
        public int FichasConFolio { get; set; }

        public int PendientesDocumentales { get; set; }

        public int AutorizacionesVigentes { get; set; }

        public int AlertasVencimiento { get; set; }
    }

    private async Task<ContadoresPanel> ContarPanelAsync(int anio, CancellationToken ct)
    {
        const string sql = """
            select
                (select count(*)::int from ficha_version v
                   join matricula_anual m on m.id = v.matricula_id
                  where m.anio = {0}) as "FichasConFolio",
                (select count(*)::int from v_pendientes_documentales
                  where anio = {0}) as "PendientesDocumentales",
                (select count(*)::int from v_autorizaciones_vigentes
                  where vigente_hasta is null or vigente_hasta >= current_date) as "AutorizacionesVigentes",
                (select count(*)::int from v_alertas_vencimiento
                  where vencida) as "AlertasVencimiento"
            """;

        return await _db.Database.SqlQueryRaw<ContadoresPanel>(sql, anio).SingleAsync(ct);
    }
}
