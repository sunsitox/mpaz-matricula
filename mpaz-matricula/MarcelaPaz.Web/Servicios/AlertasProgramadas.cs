using MarcelaPaz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Web.Servicios;

/// <summary>
/// MAT-17: proceso programado de revisión de alertas de vencimiento.
///
/// Canal: in-app, en <c>/Alertas</c>, sin correo ni servicios externos.
/// Frecuencia: cada 15 minutos, más una primera revisión al arrancar el portal.
/// Responsable del resumen: este proceso (sin funcionario asignado); la pantalla
/// sigue filtrando por los permisos de la sesión y consultando la vista en vivo.
///
/// Cada revisión deja un resumen en <c>alerta_revision</c> con los totales por
/// área para el año escolar activo. Un fallo de base no detiene el portal: se
/// registra y la próxima revisión vuelve a intentarlo.
/// </summary>
public sealed class AlertasProgramadas : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _alcances;
    private readonly ILogger<AlertasProgramadas> _log;

    public AlertasProgramadas(IServiceScopeFactory alcances, ILogger<AlertasProgramadas> log)
    {
        _alcances = alcances;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Primera revisión al arrancar, sin esperar al primer intervalo.
        await RevisarAsync(stoppingToken);

        using var temporizador = new PeriodicTimer(Intervalo);

        while (await temporizador.WaitForNextTickAsync(stoppingToken))
        {
            await RevisarAsync(stoppingToken);
        }
    }

    private async Task RevisarAsync(CancellationToken ct)
    {
        try
        {
            await using var alcance = _alcances.CreateAsyncScope();
            var proveedor = alcance.ServiceProvider;

            var anios = proveedor.GetRequiredService<AnioEscolar>();
            var anio = await anios.ActivoAsync();

            if (anio is null)
            {
                return;
            }

            var db = proveedor.GetRequiredService<MatriculaDbContext>();
            var resumen = await db.Database.SqlQueryRaw<ResumenAlertas>(SqlResumen, anio.Value).SingleAsync(ct);

            var texto = $"Vencidas: {resumen.VencidasAutorizaciones} autorizaciones, {resumen.VencidasPrescripciones} órdenes médicas; " +
                        $"por vencer: {resumen.PorVencerAutorizaciones} autorizaciones, {resumen.PorVencerPrescripciones} órdenes médicas.";

            var parametros = new object[]
            {
                anio.Value,
                resumen.VencidasAutorizaciones,
                resumen.VencidasPrescripciones,
                resumen.PorVencerAutorizaciones,
                resumen.PorVencerPrescripciones,
                texto,
            };

            await db.Database.ExecuteSqlRawAsync(
                """
                insert into alerta_revision (
                    anio,
                    vencidas_autorizaciones,
                    vencidas_prescripciones,
                    por_vencer_autorizaciones,
                    por_vencer_prescripciones,
                    resumen)
                values ({0}, {1}, {2}, {3}, {4}, {5})
                """,
                parametros);

            _log.LogInformation(
                "Revisión programada de alertas {Anio}: {Resumen}",
                anio.Value,
                texto);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo registrar la revisión programada de alertas.");
        }
    }

    private const string SqlResumen = """
        select
            count(*) filter (where v.origen = 'AUTORIZACION' and v.vencida)::int as "VencidasAutorizaciones",
            count(*) filter (where v.origen = 'PRESCRIPCION' and v.vencida)::int as "VencidasPrescripciones",
            count(*) filter (where v.origen = 'AUTORIZACION' and not v.vencida)::int as "PorVencerAutorizaciones",
            count(*) filter (where v.origen = 'PRESCRIPCION' and not v.vencida)::int as "PorVencerPrescripciones"
        from v_alertas_vencimiento v
        join matricula_anual m
          on m.estudiante_id = v.estudiante_id
         and m.anio = {0}
         and m.estado <> 'ANULADA'
        """;

    /// <summary>Totales por área que viajan en una sola consulta.</summary>
    private sealed class ResumenAlertas
    {
        public int VencidasAutorizaciones { get; set; }

        public int VencidasPrescripciones { get; set; }

        public int PorVencerAutorizaciones { get; set; }

        public int PorVencerPrescripciones { get; set; }
    }
}
