using MarcelaPaz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MarcelaPaz.Infrastructure;

/// <summary>
/// Registro de la persistencia. El proveedor de PostgreSQL (Npgsql) queda
/// encapsulado en esta capa: las capas superiores sólo entregan la cadena.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Cadena usada cuando no hay configuración. Permite que la aplicación parta
    /// y muestre el login; los accesos a datos fallarán hasta definir la conexión.
    /// </summary>
    public const string CadenaPorDefecto = "Host=localhost;Port=5432;Database=mpaz;Username=mpaz_app";

    public static IServiceCollection AddPersistencia(this IServiceCollection servicios, string? cadena)
    {
        var conexion = string.IsNullOrWhiteSpace(cadena) ? CadenaPorDefecto : cadena;

        servicios.AddDbContext<MatriculaDbContext>(opciones => opciones.UseNpgsql(conexion));

        return servicios;
    }
}
