using MarcelaPaz.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace MarcelaPaz.Web.Seguridad;

/// <summary>
/// Construye la identidad de la sesión combinando Identity (credenciales) con los
/// roles y permisos del DDL. Si el funcionario no existe o está inactivo, la
/// sesión queda sin permisos: el acceso se deniega en el servidor.
/// </summary>
public class FuncionarioClaimsPrincipalFactory : UserClaimsPrincipalFactory<IdentityUser>
{
    private readonly MatriculaDbContext _db;

    public FuncionarioClaimsPrincipalFactory(
        UserManager<IdentityUser> usuarios,
        IOptions<IdentityOptions> opciones,
        MatriculaDbContext db)
        : base(usuarios, opciones)
    {
        _db = db;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(IdentityUser usuario)
    {
        var identidad = await base.GenerateClaimsAsync(usuario);

        var funcionario = await _db.Funcionarios
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.SsoSujeto == usuario.Id);

        if (funcionario is null || !funcionario.Activo)
        {
            return identidad;
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        identidad.AddClaim(new Claim("funcionario_id", funcionario.Id.ToString()));
        identidad.AddClaim(new Claim("nombre_funcionario", funcionario.Nombre));

        var vigentes = _db.FuncionarioRoles
            .AsNoTracking()
            .Where(fr => fr.FuncionarioId == funcionario.Id && (fr.Hasta == null || fr.Hasta >= hoy));

        var roles = await vigentes
            .Join(_db.Roles, fr => fr.RolId, r => r.Id, (fr, r) => r.Codigo)
            .Distinct()
            .ToListAsync();

        foreach (var rol in roles)
        {
            identidad.AddClaim(new Claim(ClaimTypes.Role, rol));
        }

        var permisos = await vigentes
            .Join(_db.RolPermisos, fr => fr.RolId, rp => rp.RolId, (fr, rp) => new { rp.Area, rp.Accion })
            .Distinct()
            .ToListAsync();

        foreach (var permiso in permisos)
        {
            identidad.AddClaim(new Claim("permiso", $"{permiso.Area}:{permiso.Accion}"));
        }

        return identidad;
    }
}
