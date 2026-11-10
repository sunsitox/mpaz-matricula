using System.Security.Cryptography;
using System.Text;

namespace MarcelaPaz.Web.Servicios;

/// <summary>
/// Datos del membrete del certificado de alumno regular (sección
/// <c>Institucion</c> de la configuración). Los valores verificados del
/// establecimiento viven en <c>appsettings.json</c>; el decreto de
/// reconocimiento oficial se completa cuando el colegio lo confirme.
/// </summary>
public sealed class DatosInstitucion
{
    public string Sostenedor { get; set; } = string.Empty;

    public string Establecimiento { get; set; } = string.Empty;

    public string Rbd { get; set; } = string.Empty;

    /// <summary>Número del decreto de reconocimiento oficial; vacío hasta confirmarlo.</summary>
    public string DecretoReconocimiento { get; set; } = string.Empty;

    public string Direccion { get; set; } = string.Empty;

    public string Contacto { get; set; } = string.Empty;

    public string LugarEmision { get; set; } = string.Empty;

    /// <summary>Nombre de quien firma. Vacío = firma el funcionario que emite.</summary>
    public string FirmanteNombre { get; set; } = string.Empty;

    public string FirmanteCargo { get; set; } = "Dirección";

    /// <summary>
    /// Base pública de validación. Vacía = se arma con el dominio de la
    /// petición que emite el documento.
    /// </summary>
    public string UrlValidacion { get; set; } = string.Empty;

    /// <summary>Línea de reconocimiento oficial; null mientras no haya decreto confirmado.</summary>
    public string? LineaReconocimiento => string.IsNullOrWhiteSpace(DecretoReconocimiento)
        ? "Reconocido oficialmente por el Ministerio de Educación de Chile"
        : $"Reconocido oficialmente por el Ministerio de Educación de Chile mediante Decreto N° {DecretoReconocimiento.Trim()}";
}

/// <summary>
/// Folio y código de verificación de los certificados. El folio es
/// legible y correlativo por año (CAR-año-id); el código es aleatorio y
/// sólo se guarda su hash.
/// </summary>
public static class EmisionCertificado
{
    private const string AlfabetoCodigo = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public const string PropositoGenerico = "Para los fines que estime conveniente.";

    public static string FormatoFolio(int anio, int id) => $"CAR-{anio}-{id:D6}";

    /// <summary>Código aleatorio de 12 caracteres en grupos de 4 (XXXX-XXXX-XXXX).</summary>
    public static string GenerarCodigo()
    {
        var letras = new char[12];
        for (var i = 0; i < letras.Length; i++)
        {
            letras[i] = AlfabetoCodigo[RandomNumberGenerator.GetInt32(AlfabetoCodigo.Length)];
        }

        return $"{new string(letras, 0, 4)}-{new string(letras, 4, 4)}-{new string(letras, 8, 4)}";
    }

    public static string HashCodigo(string codigo)
    {
        var limpio = codigo.Replace("-", string.Empty).Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(limpio)));
    }
}