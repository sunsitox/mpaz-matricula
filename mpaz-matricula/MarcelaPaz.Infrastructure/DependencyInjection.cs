using MarcelaPaz.Infrastructure.Documentos;
using MarcelaPaz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MarcelaPaz.Infrastructure;

/// <summary>
/// Registro de la persistencia. El proveedor de PostgreSQL (Npgsql) queda
/// encapsulado en esta capa: las capas superiores sólo entregan la cadena.
/// </summary>
public static class DependencyInjection
{
    /// <param name="principal">Endpoint del servicio web (cadena configurada).</param>
    /// <param name="directa">Conexión directa a Neon; si se omite, se deriva del principal.</param>
    /// <param name="cadenaCuentas">Base de las cuentas (Identity). Si se omite,
    /// las cuentas comparten el camino vivo junto al dominio.</param>
    public static IServiceCollection AddPersistencia(
        this IServiceCollection servicios,
        string? principal,
        string? directa = null,
        string? cadenaCuentas = null)
    {
        if (string.IsNullOrWhiteSpace(principal))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:Default con la conexión a Neon; no hay una base local de respaldo.");
        }

        // La conexión vive en un único contenedor: la elección inicial por el
        // servicio web y su revalidación en caliente (cambio automático al camino
        // directo si el primero deja de responder) ocurren aquí, sin reiniciar.
        servicios.AddSingleton<ConexionViva>(proveedor =>
        {
            var viva = new ConexionViva(
                principal,
                directa,
                proveedor.GetRequiredService<ILogger<ConexionViva>>());
            viva.IniciarRevision();
            return viva;
        });

        string? conexionCuentas = string.IsNullOrWhiteSpace(cadenaCuentas)
            ? null
            : AjustarConexion(NormalizarConexion(cadenaCuentas));

        // Reintentos ante fallos transitorios: si el servidor cierra una conexión
        // ociosa o la red se corta, la consulta se repite en vez de terminar en
        // una página de error «posible fallo transitorio».
        // Cada contexto lee el camino vigente al crearse, así que el cambio en
        // caliente surte efecto en el pedido siguiente.
        void Registrar<TContext>(Func<IServiceProvider, string> cadena)
            where TContext : DbContext
        {
            servicios.AddDbContext<TContext>((proveedor, opciones) => opciones.UseNpgsql(
                cadena(proveedor),
                npgsql => npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null)));
        }

        Func<IServiceProvider, string> caminoActual =
            proveedor => proveedor.GetRequiredService<ConexionViva>().CadenaActual;

        Registrar<MatriculaDbContext>(caminoActual);
        Registrar<CuentasDbContext>(conexionCuentas is null ? caminoActual : _ => conexionCuentas);

        // Emisor SSO (prototipo del login único): comparte la conexión de las
        // cuentas para no crear una base aparte.
        Registrar<SsoDbContext>(conexionCuentas is null ? caminoActual : _ => conexionCuentas);

        return servicios;
    }

    /// <summary>
    /// Neon entrega las conexiones como URI (postgresql://usuario:clave@host/db?...),
    /// pero Npgsql sólo entiende formato palabra-clave (Host=...;Database=...).
    /// La URI se convierte aquí, así pegar la cadena tal como la da Neon funciona.
    /// Las cadenas que ya vienen en formato palabra-clave pasan intactas.
    /// </summary>
    internal static string NormalizarConexion(string conexion)
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

    /// <summary>
    /// Deja un latido en la conexión para que un socket ocioso no quede muerto:
    /// sin esto, la primera consulta después de un rato podía fallar con
    /// «Se ha forzado la interrupción de una conexión existente».
    /// </summary>
    internal static string AjustarConexion(string conexion)
    {
        try
        {
            var constructor = new NpgsqlConnectionStringBuilder(conexion);

            if (constructor.KeepAlive == 0)
            {
                constructor.KeepAlive = 30;
            }

            return constructor.ConnectionString;
        }
        catch (ArgumentException)
        {
            // Cadena con formato inesperado: se usa tal cual y el error aparece
            // al conectar, con su mensaje original.
            return conexion;
        }
    }

    /// <summary>Generación de documentos imprimibles (PDF).</summary>
    public static IServiceCollection AddDocumentos(this IServiceCollection servicios)
    {
        servicios.AddSingleton<FichaMatriculaPdf>();
        servicios.AddSingleton<CertificadoAlumnoRegularPdf>();

        return servicios;
    }
}
