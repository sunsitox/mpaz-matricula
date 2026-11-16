using Npgsql;

namespace MarcelaPaz.Infrastructure.Persistence;

/// <summary>
/// Elige por dónde entrar a la misma base con dos caminos, en orden:
/// 1) el endpoint del servicio web (la cadena configurada, que en Neon es el
///    host publicado con «-pooler»), y
/// 2) la conexión directa a Neon (el mismo host sin «-pooler»), que se deriva
///    sola para no duplicar credenciales.
/// La comprobación inicial se hace al arrancar. Después, el portal revalida el
/// camino en caliente (ver ConexionViva) y cambia al otro lado sin reiniciar.
/// Si ninguno responde al arrancar, el portal conserva la conexión configurada
/// y muestra su aviso al consultar, como hasta ahora.
/// </summary>
public static class ConexionBaseDatos
{
    /// <summary>
    /// Cadena definitiva y de dónde salió. <paramref name="EsRespaldo"/> indica
    /// que se está usando la conexión directa porque el servicio web no respondió.
    /// </summary>
    public sealed record Resultado(string Cadena, string Origen, bool EsRespaldo, string? Aviso);

    /// <param name="principal">Cadena configurada (endpoint del servicio web).</param>
    /// <param name="directa">Conexión directa a Neon; si se omite, se deriva de la principal.</param>
    /// <param name="probar">
    /// Comprobación de una cadena. Se inyecta en las pruebas para no abrir
    /// conexiones reales; en ejecución abre la base y ejecuta SELECT 1.
    /// </param>
    public static Resultado Elegir(
        string? principal,
        string? directa = null,
        Func<string, bool>? probar = null)
    {
        if (string.IsNullOrWhiteSpace(principal))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:Default con la conexión a Neon; no hay una base local de respaldo.");
        }

        var cadenaPrincipal = DependencyInjection.NormalizarConexion(principal);
        var cadenaDirecta = string.IsNullOrWhiteSpace(directa)
            ? ConexionDirecta(cadenaPrincipal)
            : DependencyInjection.NormalizarConexion(directa);

        var comprobar = probar ?? Probar;

        if (comprobar(cadenaPrincipal))
        {
            return new Resultado(cadenaPrincipal, "endpoint del servicio web", false, null);
        }

        if (!string.Equals(cadenaDirecta, cadenaPrincipal, StringComparison.OrdinalIgnoreCase)
            && comprobar(cadenaDirecta))
        {
            return new Resultado(
                cadenaDirecta,
                "conexión directa a Neon (el servicio web no respondió)",
                true,
                "El endpoint del servicio web no respondió; se está usando la conexión directa a Neon.");
        }

        return new Resultado(
            cadenaPrincipal,
            "endpoint del servicio web",
            false,
            "Ni el servicio web ni la conexión directa respondieron; se conserva la conexión configurada.");
    }

    /// <summary>
    /// Conexión directa a Neon: el host del pooler («ep-…-pooler.region…»)
    /// sin «-pooler» apunta a la misma base y acepta las mismas credenciales.
    /// Una cadena que ya es directa se devuelve intacta.
    /// </summary>
    public static string ConexionDirecta(string cadena)
    {
        try
        {
            var constructor = new NpgsqlConnectionStringBuilder(cadena);
            var host = constructor.Host ?? string.Empty;

            if (host.IndexOf("-pooler", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return cadena;
            }

            constructor.Host = host
                .Replace("-pooler.", ".", StringComparison.OrdinalIgnoreCase)
                .Replace("-pooler", string.Empty, StringComparison.OrdinalIgnoreCase);

            return constructor.ConnectionString;
        }
        catch (ArgumentException)
        {
            // Cadena con formato inesperado: no se puede derivar el respaldo.
            return cadena;
        }
    }

    /// <summary>
    /// Abre la base y ejecuta SELECT 1 con espera corta: sólo interesa saber si
    /// el camino responde ahora, no mantener la conexión abierta.
    /// </summary>
    internal static bool Probar(string cadena)
    {
        try
        {
            var constructor = new NpgsqlConnectionStringBuilder(cadena)
            {
                Timeout = 4,
                CommandTimeout = 6,
            };

            using var conexion = new NpgsqlConnection(constructor.ConnectionString);
            conexion.Open();

            using var comando = new NpgsqlCommand("SELECT 1", conexion);
            comando.ExecuteScalar();

            return true;
        }
        catch (Exception ex) when (ex is NpgsqlException
            or TimeoutException
            or ArgumentException
            or InvalidOperationException)
        {
            return false;
        }
    }
}
