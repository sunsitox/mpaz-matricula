using MarcelaPaz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace MarcelaPaz.Web.Seguridad;

/// <summary>
/// Comprueba, en cada petición autorizada, que la sesión sigue representando al
/// funcionario tal como está hoy en la base: activo y con los mismos permisos con
/// los que inició sesión. Cubre el caso de despedir a alguien o cerrarle un rol
/// mientras su cookie sigue vigente (S-03).
///
/// La comprobación exige que **todos** los permisos de la cookie sigan vigentes:
/// quitar un rol invalida la sesión de inmediato. Un permiso **nuevo** no la
/// invalida (la cookie no lo trae hasta el próximo ingreso, pero tampoco concede
/// nada de más), así que ajustar la matriz no expulsa a quien está trabajando.
///
/// La consulta se memoriza 60 segundos por funcionario: la desactivación surte
/// efecto en ese plazo sin golpear la base en cada recurso.
/// </summary>
public sealed class VigilanteSesion
{
    private static readonly TimeSpan Memoria = TimeSpan.FromSeconds(60);

    private readonly MatriculaDbContext _db;
    private readonly IMemoryCache _cache;

    public VigilanteSesion(MatriculaDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<bool> SigueVigenteAsync(ClaimsPrincipal usuario, CancellationToken ct = default)
    {
        if (!int.TryParse(usuario.FindFirstValue("funcionario_id"), out var funcionarioId))
        {
            return false;
        }

        var vigentes = await _cache.GetOrCreateAsync($"sesion:{funcionarioId}", async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = Memoria;

            var activo = await _db.Funcionarios
                .AsNoTracking()
                .AnyAsync(f => f.Id == funcionarioId && f.Activo, ct);

            if (!activo)
            {
                return null;
            }

            var hoy = DateOnly.FromDateTime(DateTime.Today);

            return await _db.FuncionarioRoles
                .AsNoTracking()
                .Where(fr => fr.FuncionarioId == funcionarioId && (fr.Hasta == null || fr.Hasta >= hoy))
                .Join(_db.RolPermisos, fr => fr.RolId, rp => rp.RolId, (_, rp) => rp.Area + ":" + rp.Accion)
                .Distinct()
                .OrderBy(permiso => permiso)
                .ToListAsync(ct);
        });

        if (vigentes is null)
        {
            return false;
        }

        var vigentesHoy = vigentes.ToHashSet(StringComparer.Ordinal);

        // Se recorren los permisos de la sesión, no los de la base: si a la
        // persona le quitaron uno, la cookie deja de servir.
        return usuario.FindAll("permiso")
            .Select(claim => claim.Value)
            .All(vigentesHoy.Contains);
    }

    /// <summary>Olvida la comprobación memorizada: la próxima petición vuelve a la base.</summary>
    public void Invalidar(int funcionarioId) => _cache.Remove($"sesion:{funcionarioId}");
}
