using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MarcelaPaz.Web.Servicios;

/// <summary>
/// Sincroniza el registro de funcionarios con el web service externo
/// (<c>https://api.empaz.cl/api/registro/</c>): envía <c>nombre</c>,
/// <c>correo</c> y <c>contrasena</c> con cabecera <c>Authorization: Token …</c>.
/// El token vive en <c>WebService:Token</c> (variable de entorno o user-secrets,
/// nunca en el repositorio).
///
/// Si el web service está caído o responde error, el método devuelve <c>false</c>
/// y el funcionario ya quedó cargado en Neon: la base del portal es el respaldo.
/// </summary>
public sealed class RegistroWebService
{
    private readonly HttpClient _http;
    private readonly string? _baseUrl;
    private readonly string? _token;
    private readonly ILogger<RegistroWebService> _log;

    public RegistroWebService(
        HttpClient http,
        IConfiguration configuracion,
        ILogger<RegistroWebService> log)
    {
        _http = http;
        _baseUrl = configuracion["WebService:BaseUrl"]?.TrimEnd('/');
        _token = configuracion["WebService:Token"];
        _log = log;
    }

    /// <summary>True si hay base y token configurados para llamar al web service.</summary>
    public bool Configurado => !string.IsNullOrWhiteSpace(_baseUrl)
        && !string.IsNullOrWhiteSpace(_token);

    /// <summary>
    /// Registra el funcionario en el web service. Devuelve <c>true</c> si el
    /// web service aceptó el registro; <c>false</c> si está caído, no está
    /// configurado o rechazó la petición (en ese caso rige el respaldo Neon).
    /// </summary>
    public async Task<bool> RegistrarAsync(
        string nombre,
        string correo,
        string contrasena,
        CancellationToken ct = default)
    {
        if (!Configurado)
        {
            _log.LogWarning(
                "Web service sin configurar (WebService:BaseUrl/Token); el funcionario {Correo} quedó en Neon.",
                correo);
            return false;
        }

        try
        {
            var cuerpo = JsonSerializer.Serialize(new { nombre, correo, contrasena });

            using var mensaje = new HttpRequestMessage(
                HttpMethod.Post, $"{_baseUrl}/api/registro/");
            mensaje.Headers.Authorization = new AuthenticationHeaderValue("Token", _token);
            mensaje.Content = new StringContent(cuerpo, Encoding.UTF8, "application/json");

            using var respuesta = await _http.SendAsync(mensaje, ct);

            if (respuesta.IsSuccessStatusCode)
            {
                _log.LogInformation(
                    "Funcionario {Correo} registrado en el web service.", correo);
                return true;
            }

            var detalle = await respuesta.Content.ReadAsStringAsync(ct);
            _log.LogWarning(
                "El web service respondió {Codigo} al registrar {Correo}: {Detalle}. Queda en Neon.",
                (int)respuesta.StatusCode, correo, detalle);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogWarning(
                ex,
                "Web service no disponible al registrar {Correo}; el funcionario quedó en Neon.",
                correo);
            return false;
        }
    }
}
