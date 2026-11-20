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

    /// <summary>
    /// Nombre de quien firma (decisión 17, paso 20): firma el subrogante, es
    /// decir el funcionario que emite; este valor ya no se usa para el nombre.
    /// Se conserva por compatibilidad de configuración.
    /// </summary>
    public string FirmanteNombre { get; set; } = string.Empty;

    /// <summary>Cargo impreso en el certificado: «Director».</summary>
    public string FirmanteCargo { get; set; } = "Director";

    /// <summary>
    /// Origen público del portal para el QR de verificación (por ejemplo
    /// <c>https://portal.mpaz.cl</c>). Vacía = se usa el dominio de la
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

    /// <summary>
    /// Días de vigencia de un certificado desde su emisión. Pasado ese plazo la
    /// validación pública lo declara vencido: el papel acredita la situación de
    /// un año y el establecimiento no responde por él indefinidamente.
    /// </summary>
    public const int DiasVigencia = 30;

    /// <summary>Momento en que el certificado deja de ser válido (emisión + 30 días).</summary>
    public static DateTimeOffset VenceEn(DateTimeOffset emitidoEn) =>
        emitidoEn.AddDays(DiasVigencia);

    /// <summary>Vigente mientras no se cumpla el plazo: al día 30 ya está vencido.</summary>
    public static bool EstaVigente(DateTimeOffset emitidoEn, DateTimeOffset ahora) =>
        ahora < VenceEn(emitidoEn);

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

    /// <summary>
    /// Código sin guiones para la dirección corta del QR: mientras menos
    /// caracteres, menos módulos y más fácil de leer el código impreso.
    /// </summary>
    public static string CodigoParaUrl(string codigo) =>
        codigo.Replace("-", string.Empty).Trim().ToUpperInvariant();
}