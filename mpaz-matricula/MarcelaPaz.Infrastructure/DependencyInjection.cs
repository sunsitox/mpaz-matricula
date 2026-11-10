using MarcelaPaz.Infrastructure.Documentos;
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
    public const string CadenaPorDefecto = "Host=localhost;Port=5432;Database=mpaz;Username=mpaz_app;Search Path=mpaz_matricula,public";

    /// <param name="cadena">Base del dominio (tablas institucionales).</param>
    /// <param name="cadenaCuentas">Base de las cuentas (Identity). Si se omite,
    /// las cuentas viven junto al dominio con la misma conexión.</param>
    public static IServiceCollection AddPersistencia(
        this IServiceCollection servicios, string? cadena, string? cadenaCuentas = null)
    {
        var conexion = string.IsNullOrWhiteSpace(cadena) ? CadenaPorDefecto : cadena;
        var conexionCuentas = string.IsNullOrWhiteSpace(cadenaCuentas) ? conexion : cadenaCuentas;

        servicios.AddDbContext<MatriculaDbContext>(opciones => opciones.UseNpgsql(conexion));
        servicios.AddDbContext<CuentasDbContext>(opciones => opciones.UseNpgsql(conexionCuentas));

        return servicios;
    }

    /// <summary>Generación de documentos imprimibles (PDF).</summary>
    public static IServiceCollection AddDocumentos(this IServiceCollection servicios)
    {
        servicios.AddSingleton<FichaMatriculaPdf>();

        return servicios;
    }
}
