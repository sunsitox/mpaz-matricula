using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Web.Servicios;

/// <summary>
/// Calcula los indicadores de la portada sobre la base institucional: avance de
/// la matrícula del año, distribución por curso, pendientes documentales y
/// actividad reciente. Sólo devuelve cifras agregadas.
/// </summary>
public class PanelIndicadores
{
    /// <summary>Estados en que la matrícula del año ya está cerrada.</summary>
    private static readonly string[] EstadosCerrados = { "FIRMADA", "CONFIRMADA", "FORMALIZADA" };

    private readonly MatriculaDbContext _db;

    public PanelIndicadores(MatriculaDbContext db) => _db = db;

    public async Task<PanelInicio> CargarAsync(int anio, CancellationToken ct = default)
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
            .OrderBy(curso => curso.Curso, StringComparer.CurrentCulture)
            .ToList();

        panel.EstudiantesRegistrados = await _db.Estudiantes.CountAsync(ct);
        panel.FuncionariosActivos = await _db.Funcionarios.CountAsync(f => f.Activo, ct);

        panel.FichasConFolio = await ContarAsync(
            "select count(*)::int as \"Value\" from ficha_version v " +
            "join matricula_anual m on m.id = v.matricula_id where m.anio = {0}", anio, ct);

        panel.PendientesDocumentales = await ContarAsync(
            "select count(*)::int as \"Value\" from v_pendientes_documentales where anio = {0}", anio, ct);

        panel.AutorizacionesVigentes = await ContarAsync(
            "select count(*)::int as \"Value\" from v_autorizaciones_vigentes " +
            "where vigente_hasta is null or vigente_hasta >= current_date", anio, ct);

        panel.AlertasVencimiento = await ContarAsync(
            "select count(*)::int as \"Value\" from v_alertas_vencimiento where vencida", anio, ct);

        var ultima = await _db.Matriculas
            .AsNoTracking()
            .Where(m => m.Anio == anio)
            .OrderByDescending(m => m.UpdatedAt)
            .Select(m => new { m.UpdatedAt, m.CreadaPor })
            .FirstOrDefaultAsync(ct);

        if (ultima is not null)
        {
            panel.ActualizadoEn = ultima.UpdatedAt;

            if (ultima.CreadaPor is int funcionarioId)
            {
                panel.Responsable = await _db.Funcionarios
                    .AsNoTracking()
                    .Where(f => f.Id == funcionarioId)
                    .Select(f => f.Nombre)
                    .FirstOrDefaultAsync(ct);
            }
        }

        var accesos = await _db.RegistrosAcceso
            .AsNoTracking()
            .OrderByDescending(a => a.OcurridoEn)
            .Take(5)
            .Select(a => new { a.OcurridoEn, a.Accion, a.Area, a.FuncionarioId })
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

        panel.Accesos = accesos
            .Select(a => new PanelInicio.AccesoPanel
            {
                Cuando = a.OcurridoEn,
                Accion = a.Accion,
                Area = a.Area,
                Funcionario = a.FuncionarioId is int id && nombres.TryGetValue(id, out var nombre) ? nombre : null,
            })
            .ToList();

        return panel;
    }

    /// <summary>Cuenta filas de una vista o tabla que el contexto no mapea.</summary>
    private async Task<int> ContarAsync(string sql, int anio, CancellationToken ct) =>
        await _db.Database.SqlQueryRaw<int>(sql, anio).SingleAsync(ct);
}
