using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Infrastructure.Persistence;

/// <summary>
/// Acceso a las cuentas (tablas de Identity) cuando viven en una base
/// separada —el auth central compartido por los sistemas— en vez de junto
/// al dominio. Reutiliza el modelo de <see cref="MatriculaDbContext"/>:
/// sólo cambia la conexión. Si no se configura otra cadena, apunta a la
/// misma base del dominio y el portal funciona con una sola conexión.
/// </summary>
public sealed class CuentasDbContext : MatriculaDbContext
{
    public CuentasDbContext(DbContextOptions<CuentasDbContext> options)
        : base(options)
    {
    }
}
