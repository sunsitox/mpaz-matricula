using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Infrastructure.Persistence;

/// <summary>
/// Acceso a las cuentas (tablas de Identity). Reutiliza el modelo de
/// <see cref="MatriculaDbContext"/>: sólo puede cambiar la conexión.
/// Normalmente apunta a la misma base del dominio (las cuentas conviven
/// con él en el esquema del portal); si algún despliegue las separa en
/// otra base, basta con entregarle esa cadena.
/// </summary>
public sealed class CuentasDbContext : MatriculaDbContext
{
    public CuentasDbContext(DbContextOptions<CuentasDbContext> options)
        : base(options)
    {
    }
}
