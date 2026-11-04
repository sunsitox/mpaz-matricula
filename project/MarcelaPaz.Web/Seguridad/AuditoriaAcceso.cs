using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Infrastructure.Persistence;
using System.Security.Claims;

namespace MarcelaPaz.Web.Seguridad;

/// <summary>
/// Registra en <c>registros_acceso</c> las lecturas, búsquedas, descargas y
/// denegaciones (MAT-13). Un fallo al auditar no debe tumbar la operación.
/// </summary>
public interface IAuditoriaAcceso
{
    Task RegistrarAsync(
        string accion,
        string? area,
        string? recurso,
        int? funcionarioId = null,
        string? detalle = null,
        int? estudianteId = null,
        long? recursoId = null);
}

public class AuditoriaAcceso : IAuditoriaAcceso
{
    private readonly MatriculaDbContext _db;
    private readonly IHttpContextAccessor _contexto;
    private readonly ILogger<AuditoriaAcceso> _log;

    public AuditoriaAcceso(MatriculaDbContext db, IHttpContextAccessor contexto, ILogger<AuditoriaAcceso> log)
    {
        _db = db;
        _contexto = contexto;
        _log = log;
    }

    public async Task RegistrarAsync(
        string accion,
        string? area,
        string? recurso,
        int? funcionarioId = null,
        string? detalle = null,
        int? estudianteId = null,
        long? recursoId = null)
    {
        var http = _contexto.HttpContext;

        var idSesion = int.TryParse(http?.User.FindFirstValue("funcionario_id"), out var valor) ? valor : (int?)null;

        var registro = new RegistroAcceso
        {
            FuncionarioId = funcionarioId ?? idSesion,
            Accion = accion,
            Area = area,
            Recurso = recurso,
            RecursoId = recursoId,
            EstudianteId = estudianteId,
            Detalle = detalle,
            Ip = http?.Connection.RemoteIpAddress,
            OcurridoEn = DateTimeOffset.UtcNow,
        };

        try
        {
            _db.RegistrosAcceso.Add(registro);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo registrar el acceso {Accion} en registros_acceso", accion);
        }
    }
}
