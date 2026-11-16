using Microsoft.Extensions.Logging;

namespace MarcelaPaz.Infrastructure.Persistence;

/// <summary>
/// La conexión viva a la base: guarda los dos caminos (servicio web primero y
/// conexión directa a Neon) y el que está en uso. Cada contexto de base de datos
/// lee el camino vigente cuando se crea, así que cambiarlo aquí surte efecto en
/// los pedidos siguientes sin reiniciar el portal.
/// La revisión periódica (cada 30 segundos) comprueba el camino en uso y, si
/// deja de responder, pasa en caliente al otro lado; si ambos caen, conserva el
/// que estaba y lo registra.
/// </summary>
public sealed class ConexionViva : IDisposable
{
    private readonly string _principal;
    private readonly string _directa;
    private readonly ILogger<ConexionViva>? _registro;
    private readonly Func<string, bool> _probar;
    private string _cadena;
    private bool _esRespaldo;
    private Timer? _revision;

    public ConexionViva(
        string principal,
        string? directa = null,
        ILogger<ConexionViva>? registro = null,
        Func<string, bool>? probar = null)
    {
        if (string.IsNullOrWhiteSpace(principal))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:Default con la conexión a Neon; no hay una base local de respaldo.");
        }

        _principal = DependencyInjection.AjustarConexion(DependencyInjection.NormalizarConexion(principal));
        _directa = string.IsNullOrWhiteSpace(directa)
            ? ConexionBaseDatos.ConexionDirecta(_principal)
            : DependencyInjection.AjustarConexion(DependencyInjection.NormalizarConexion(directa));

        _registro = registro;
        _probar = probar ?? ConexionBaseDatos.Probar;

        // Elección inicial con la misma lógica que ya existía al arrancar.
        var elegido = ConexionBaseDatos.Elegir(_principal, _directa, _probar);
        _cadena = elegido.Cadena;
        _esRespaldo = elegido.EsRespaldo;
        Origen = elegido.Origen;
        Aviso = elegido.Aviso;
    }

    /// <summary>Camino vigente: los contextos lo leen al crearse.</summary>
    public string CadenaActual => Volatile.Read(ref _cadena);

    /// <summary>True si hoy se entra por la conexión directa a Neon.</summary>
    public bool EsRespaldo => Volatile.Read(ref _esRespaldo);

    /// <summary>De dónde salió el camino vigente al arrancar (para el registro).</summary>
    public string Origen { get; }

    /// <summary>Un aviso para el registro cuando el arranque no fue por el camino principal.</summary>
    public string? Aviso { get; }

    /// <summary>Empieza la revalidación periódica: cada 30 segundos.</summary>
    public void IniciarRevision()
    {
        _revision = new Timer(
            _ =>
            {
                try
                {
                    Verificar();
                }
                catch
                {
                    // Una revisión fallida nunca tumba el proceso: el portal sigue
                    // con el camino que tiene y lo vuelve a revisar después.
                }
            },
            null,
            TimeSpan.FromSeconds(45),
            TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Comprueba el camino en uso. Si no responde, prueba el otro y, si ese sí
    /// responde, el cambio se aplica de inmediato. Devuelve si el camino queda
    /// operativo.
    /// </summary>
    public bool Verificar()
    {
        var actual = CadenaActual;

        if (_probar(actual))
        {
            return true;
        }

        var otra = string.Equals(actual, _principal, StringComparison.OrdinalIgnoreCase) ? _directa : _principal;

        if (string.Equals(otra, actual, StringComparison.OrdinalIgnoreCase) || !_probar(otra))
        {
            _registro?.LogError("Ninguno de los dos caminos a la base responde.");
            return false;
        }

        _cadena = otra;
        _esRespaldo = string.Equals(otra, _directa, StringComparison.OrdinalIgnoreCase);

        _registro?.LogWarning(
            "El camino a la base dejó de responder; se cambió en caliente a {Origen}.",
            _esRespaldo ? "la conexión directa a Neon" : "el endpoint del servicio web");

        return true;
    }

    public void Dispose() => _revision?.Dispose();
}
