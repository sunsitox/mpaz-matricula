using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;

namespace MarcelaPaz.Infrastructure.Persistence;

/// <summary>
/// Emisor SSO (prototipo del login único): aplicaciones, autorizaciones,
/// scopes y tokens de OpenIddict.
/// Vive en el esquema neutral mpaz_auth: no pertenece a ningún sistema y
/// sus tablas son nuevas, nada de lo existente se toca.
/// </summary>
public sealed class SsoDbContext : DbContext
{
    /// <summary>Esquema neutral de identidad, compartido por todos los sistemas.</summary>
    public const string Esquema = "mpaz_auth";
    public SsoDbContext(DbContextOptions<SsoDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Esquema);
        builder.UseOpenIddict();
    }
}
