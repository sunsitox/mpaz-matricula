using System.Data.Common;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// MAT-17: órdenes médicas y autorizaciones vencidas o por vencer en los
/// próximos 30 días. El acceso es por área: AUTORIZACIONES:LEER abre las
/// autorizaciones y MEDICACION:LEER las órdenes médicas. Un rol sin ninguna de
/// las dos no entra y el intento queda registrado (MAT-12, MAT-13).
/// </summary>
[Authorize]
public class AlertasController : Controller
{
    /// <summary>
    /// Fuente: v_alertas_vencimiento, que ya trae las vencidas y las que vencen
    /// en 30 días. Se une con el alumno y con el curso de su última matrícula
    /// no anulada para poder mostrarla y enlazar a la ficha.
    /// </summary>
    private const string SqlAutorizaciones = """
        select v.registro_id     as "RegistroId",
               v.estudiante_id   as "EstudianteId",
               'AUTORIZACION'    as "Origen",
               e.tipo_identificador || ' ' || e.identificador as "Identificador",
               e.apellido_paterno || coalesce(' ' || e.apellido_materno, '')
                   || ', ' || e.nombres as "Estudiante",
               coalesce(c.nombre, 'Sin curso') as "Curso",
               c.anio            as "Anio",
               case a.tipo
                    when 'IMAGEN' then 'Imagen'
                    when 'CONVIVENCIA' then 'Convivencia'
                    else 'Medicación'
               end               as "Detalle",
               v.vence_el        as "VenceEl",
               v.vencida         as "Vencida"
        from v_alertas_vencimiento v
        join autorizacion a on a.id = v.registro_id
        join estudiante e   on e.id = v.estudiante_id
        left join lateral (
            select cu.nombre, m.anio::int as anio
            from matricula_anual m
            join curso cu on cu.id = m.curso_id
            where m.estudiante_id = e.id
              and m.anio = {0}
              and m.estado <> 'ANULADA'
            limit 1
        ) c on true
        where v.origen = 'AUTORIZACION'
          and ({0} < 0 or c.anio = {0})
        order by v.vencida desc, v.vence_el, e.apellido_paterno, e.apellido_materno
        """;

    private const string SqlPrescripciones = """
        select v.registro_id     as "RegistroId",
               v.estudiante_id   as "EstudianteId",
               'PRESCRIPCION'    as "Origen",
               e.tipo_identificador || ' ' || e.identificador as "Identificador",
               e.apellido_paterno || coalesce(' ' || e.apellido_materno, '')
                   || ', ' || e.nombres as "Estudiante",
               coalesce(c.nombre, 'Sin curso') as "Curso",
               c.anio            as "Anio",
               p.medicamento     as "Detalle",
               v.vence_el        as "VenceEl",
               v.vencida         as "Vencida"
        from v_alertas_vencimiento v
        join prescripcion p on p.id = v.registro_id
        join estudiante e   on e.id = v.estudiante_id
        left join lateral (
            select cu.nombre, m.anio::int as anio
            from matricula_anual m
            join curso cu on cu.id = m.curso_id
            where m.estudiante_id = e.id
              and m.anio = {0}
              and m.estado <> 'ANULADA'
            limit 1
        ) c on true
        where v.origen = 'PRESCRIPCION'
          and ({0} < 0 or c.anio = {0})
        order by v.vencida desc, v.vence_el, e.apellido_paterno, e.apellido_materno
        """;

    private readonly MatriculaDbContext _db;
    private readonly IAuditoriaAcceso _auditoria;
    private readonly AnioEscolar _anios;

    public AlertasController(
        MatriculaDbContext db,
        IAuditoriaAcceso auditoria,
        AnioEscolar anios)
    {
        _db = db;
        _auditoria = auditoria;
        _anios = anios;
    }

    public async Task<IActionResult> Index(int? anio, CancellationToken ct)
    {
        var puedeAutorizaciones = User.HasClaim("permiso", "AUTORIZACIONES:LEER");
        var puedeMedicacion = User.HasClaim("permiso", "MEDICACION:LEER");

        if (!puedeAutorizaciones && !puedeMedicacion)
        {
            await _auditoria.RegistrarAsync(
                "DENEGADO",
                "ALERTAS",
                "alertas",
                detalle: "{\"permiso_requerido\":\"AUTORIZACIONES:LEER o MEDICACION:LEER\"}");

            return RedirectToAction("SinPermiso", "Cuenta");
        }

        var anioConsulta = await _anios.ResolverAsync(anio);
        var anios = await _anios.ListarAsync();
        var pagina = new AlertasPagina
        {
            Anio = anioConsulta,
            Anios = anios,
            PuedeAutorizaciones = puedeAutorizaciones,
            PuedeMedicacion = puedeMedicacion,
        };

        try
        {
            var anioSql = anioConsulta ?? -1;

            if (puedeAutorizaciones)
            {
                pagina.Alertas.AddRange(
                    await _db.Database.SqlQueryRaw<AlertaVencimiento>(SqlAutorizaciones, anioSql).ToListAsync(ct));
            }

            if (puedeMedicacion)
            {
                pagina.Alertas.AddRange(
                    await _db.Database.SqlQueryRaw<AlertaVencimiento>(SqlPrescripciones, anioSql).ToListAsync(ct));
            }

            // Primero lo vencido y, dentro de cada grupo, lo que vence antes.
            pagina.Alertas = pagina.Alertas
                .OrderByDescending(a => a.Vencida)
                .ThenBy(a => a.VenceEl)
                .ThenBy(a => a.Estudiante, StringComparer.CurrentCulture)
                .ToList();

            try
            {
                pagina.UltimaRevision = await _db.Database.SqlQueryRaw<RevisionAlertas>(
                    """
                    select revisada_en as "RevisadaEn", anio as "Anio",
                           vencidas_autorizaciones as "VencidasAutorizaciones",
                           vencidas_prescripciones as "VencidasPrescripciones",
                           por_vencer_autorizaciones as "PorVencerAutorizaciones",
                           por_vencer_prescripciones as "PorVencerPrescripciones",
                           resumen as "Resumen"
                      from alerta_revision
                     order by revisada_en desc
                     limit 1
                    """).FirstOrDefaultAsync(ct);
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                // La tabla puede no existir en una base instalada sin el paso 13.
                pagina.UltimaRevision = null;
            }

            await _auditoria.RegistrarAsync(
                "LEER", "ALERTAS", "alertas",
                detalle: anioConsulta is int anioLog ? "{\"anio\":" + anioLog + "}" : null);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            // Sin base el portal sigue abriendo: la pantalla muestra el aviso.
            pagina.Error = "No se pudieron leer las alertas: revise la conexión con la base de datos.";
        }

        return View(pagina);
    }
}
