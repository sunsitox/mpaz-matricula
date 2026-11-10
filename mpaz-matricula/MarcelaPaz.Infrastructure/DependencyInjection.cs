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
        var conexion = NormalizarConexion(string.IsNullOrWhiteSpace(cadena) ? CadenaPorDefecto : cadena);
        var conexionCuentas = NormalizarConexion(string.IsNullOrWhiteSpace(cadenaCuentas) ? conexion : cadenaCuentas);

        servicios.AddDbContext<MatriculaDbContext>(opciones => opciones.UseNpgsql(conexion));
        servicios.AddDbContext<CuentasDbContext>(opciones => opciones.UseNpgsql(conexionCuentas));

        return servicios;
    }

    /// <summary>
    /// Neon entrega las conexiones como URI (postgresql://usuario:clave@host/db?...),
    /// pero Npgsql sólo entiende formato palabra-clave (Host=...;Database=...).
    /// La URI se convierte aquí, así pegar la cadena tal como la da Neon funciona.
    /// Las cadenas que ya vienen en formato palabra-clave pasan intactas.
    /// </summary>
    private static string NormalizarConexion(string conexion)
    {
        if (!Uri.TryCreate(conexion, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgresql" && uri.Scheme != "postgres"))
            return conexion;

        var credenciales = uri.UserInfo.Split(':', 2);
        var usuario = credenciales.Length > 0 ? Uri.UnescapeDataString(credenciales[0]) : string.Empty;
        var clave = credenciales.Length > 1
            ? Uri.UnescapeDataString(credenciales[1]).Replace("'", "''")
            : string.Empty;

        var parametros = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].ToLowerInvariant(), p => Uri.UnescapeDataString(p[1]));

        var ssl = parametros.TryGetValue("sslmode", out var modo) ? modo : "require";
        var partes = new List<string>
        {
            $"Host={uri.Host}",
            $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
            $"Database={uri.AbsolutePath.Trim('/')}",
            $"Username={usuario}",
            $"Password='{clave}'",
            $"Ssl Mode={ssl}",
        };
        if (parametros.TryGetValue("channel_binding", out var canal))
            partes.Add($"Channel Binding={canal}");

        return string.Join(';', partes);
    }

    /// <summary>Generación de documentos imprimibles (PDF).</summary>
    public static IServiceCollection AddDocumentos(this IServiceCollection servicios)
    {
        servicios.AddSingleton<FichaMatriculaPdf>();
        servicios.AddSingleton<CertificadoAlumnoRegularPdf>();

        return servicios;
    }
}
