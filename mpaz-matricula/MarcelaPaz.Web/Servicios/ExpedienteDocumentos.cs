using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace MarcelaPaz.Web.Servicios;

/// <summary>
/// Guarda los archivos del expediente fuera de <c>wwwroot</c>, con nombre
/// aleatorio, hash SHA-256 y validación de formato real (no solo extensión).
/// La raíz se configura con <c>Documentos:RutaBase</c>: en el servidor debe
/// quedar fuera del release, que se reemplaza en cada despliegue.
/// </summary>
public sealed class ExpedienteDocumentos
{
    /// <summary>Límite del DDL: 15 MB por archivo.</summary>
    public const int LimiteBytes = 15 * 1024 * 1024;

    /// <summary>Extensiones y tipos MIME aceptados por el esquema.</summary>
    public static readonly string[] Extensiones = [".pdf", ".jpg", ".jpeg", ".png"];

    private readonly string _raiz;

    public ExpedienteDocumentos(IWebHostEnvironment entorno, IConfiguration configuracion)
    {
        _raiz = ResolverRaiz(configuracion["Documentos:RutaBase"], entorno.ContentRootPath);
    }

    /// <summary>
    /// Raíz del expediente. Sin configuración queda en <c>App_Data/documentos</c>
    /// (cómodo en desarrollo); en el servidor se apunta a un directorio estable.
    /// </summary>
    public static string ResolverRaiz(string? configurada, string contenidoRaiz) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(configurada)
            ? Path.Combine(contenidoRaiz, "App_Data", "documentos")
            : configurada);

    public static string? MimePermitido(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => null,
    };

    /// <summary>Valida presencia, tamaño y extensión antes de leer el archivo.</summary>
    public static string? Validar(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return "Seleccione un archivo PDF, JPG o PNG.";
        }

        if (archivo.Length > LimiteBytes)
        {
            return "El archivo supera el máximo de 15 MB.";
        }

        var extension = Path.GetExtension(archivo.FileName);

        if (MimePermitido(extension) is null)
        {
            return "Sólo se aceptan archivos PDF, JPG o PNG.";
        }

        return null;
    }

    /// <summary>
    /// Comprueba que los primeros bytes correspondan al formato declarado: un
    /// PDF renombrado como imagen se rechaza antes de guardarlo.
    /// </summary>
    public static bool FirmaValida(ReadOnlySpan<byte> cabecera, string mime) => mime switch
    {
        "application/pdf" => cabecera.StartsWith("%PDF-"u8),
        "image/jpeg" => cabecera.Length >= 3
                        && cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF,
        "image/png" => cabecera.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        _ => false,
    };

    /// <summary>
    /// Guarda el archivo con nombre aleatorio bajo la raíz configurada y
    /// devuelve la ruta relativa que se persiste en la base.
    /// </summary>
    public Task<ResultadoArchivo> GuardarAsync(
        IFormFile archivo,
        int estudianteId,
        CancellationToken ct = default) =>
        GuardarEnAsync(archivo, estudianteId.ToString(), ct);

    /// <summary>
    /// Igual que <see cref="GuardarAsync"/>, pero en una carpeta con nombre
    /// propio (por ejemplo, las fichas médicas de un funcionario).
    /// </summary>
    public async Task<ResultadoArchivo> GuardarEnAsync(
        IFormFile archivo,
        string carpeta,
        CancellationToken ct = default)
    {
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        var mime = MimePermitido(extension)
            ?? throw new ArchivoInvalidoException("Sólo se aceptan archivos PDF, JPG o PNG.");

        byte[] cabecera = new byte[8];
        int leidos;

        await using (var entrada = archivo.OpenReadStream())
        {
            leidos = await entrada.ReadAsync(cabecera, ct);
        }

        if (!FirmaValida(cabecera.AsSpan(0, leidos), mime))
        {
            throw new ArchivoInvalidoException(
                "El archivo no corresponde al formato declarado. Verifique que no esté dañado.");
        }

        string hash;

        await using (var entrada = archivo.OpenReadStream())
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(entrada, ct)).ToLowerInvariant();
        }

        var nombre = Path.GetFileName(archivo.FileName);

        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = $"documento{extension}";
        }

        var relativa = Path.Combine(
            carpeta,
            $"{Guid.NewGuid():N}{extension}");

        var fisica = Path.Combine(_raiz, relativa);
        Directory.CreateDirectory(Path.GetDirectoryName(fisica)!);

        await using (var salida = File.Create(fisica))
        {
            await using var entrada = archivo.OpenReadStream();
            await entrada.CopyToAsync(salida, ct);
        }

        return new ResultadoArchivo(
            relativa.Replace('\\', '/'),
            nombre,
            mime,
            (int)archivo.Length,
            hash);
    }

    /// <summary>Resuelve la ruta física solo si queda dentro del directorio del expediente.</summary>
    public string? RutaFisica(string relativa) => ResolverArchivo(_raiz, relativa);

    /// <summary>
    /// Ruta absoluta de un registro relativo, o <c>null</c> si apunta fuera de la
    /// raíz o el archivo no está. Acepta el prefijo antiguo
    /// <c>App_Data/documentos/</c> para no perder lo ya cargado.
    /// </summary>
    public static string? ResolverArchivo(string raiz, string relativa)
    {
        var limpia = relativa.Replace('\\', '/');

        const string legado = "App_Data/documentos/";

        if (limpia.StartsWith(legado, StringComparison.OrdinalIgnoreCase))
        {
            limpia = limpia[legado.Length..];
        }

        var baseFisica = Path.GetFullPath(raiz) + Path.DirectorySeparatorChar;
        var fisica = Path.GetFullPath(Path.Combine(baseFisica, limpia));

        return fisica.StartsWith(baseFisica, StringComparison.OrdinalIgnoreCase) && File.Exists(fisica)
            ? fisica
            : null;
    }

    public void Eliminar(string relativa)
    {
        var fisica = RutaFisica(relativa);

        if (fisica is not null)
        {
            File.Delete(fisica);
        }
    }
}

public sealed record ResultadoArchivo(
    string Ruta,
    string Nombre,
    string Mime,
    int Tamano,
    string Hash);

public sealed class ArchivoInvalidoException(string mensaje) : Exception(mensaje);
