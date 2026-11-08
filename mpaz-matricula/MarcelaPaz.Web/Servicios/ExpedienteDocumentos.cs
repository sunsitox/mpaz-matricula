using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace MarcelaPaz.Web.Servicios;

/// <summary>
/// Guarda los archivos del expediente fuera de <c>wwwroot</c>, con nombre
/// aleatorio, hash SHA-256 y validación de formato real (no solo extensión).
/// </summary>
public sealed class ExpedienteDocumentos
{
    /// <summary>Límite del DDL: 15 MB por archivo.</summary>
    public const int LimiteBytes = 15 * 1024 * 1024;

    /// <summary>Extensiones y tipos MIME aceptados por el esquema.</summary>
    public static readonly string[] Extensiones = [".pdf", ".jpg", ".jpeg", ".png"];

    private readonly IWebHostEnvironment _entorno;

    public ExpedienteDocumentos(IWebHostEnvironment entorno) => _entorno = entorno;

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

        if (MimePermitido(Path.GetExtension(archivo.FileName)) is null)
        {
            return "Formato no permitido: use PDF, JPG o PNG.";
        }

        return null;
    }

    /// <summary>
    /// Comprueba que los primeros bytes correspondan al tipo declarado, para que
    /// renombrar un archivo no baste para subirlo.
    /// </summary>
    public static bool FirmaValida(ReadOnlySpan<byte> cabecera, string mime) => mime switch
    {
        "application/pdf" => cabecera.StartsWith("%PDF"u8),
        "image/jpeg" => cabecera.Length >= 3
                        && cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF,
        "image/png" => cabecera.Length >= 8
                       && cabecera[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        _ => false,
    };

    /// <summary>Guarda el archivo y devuelve sus datos para registrarlo en la base.</summary>
    public async Task<ResultadoArchivo> GuardarAsync(
        IFormFile archivo,
        int estudianteId,
        CancellationToken ct = default)
    {
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        var mime = MimePermitido(extension)
                   ?? throw new ArchivoInvalidoException("Formato no permitido.");

        var cabecera = new byte[8];
        int leidos;

        await using (var entrada = archivo.OpenReadStream())
        {
            leidos = await entrada.ReadAsync(cabecera, ct);
        }

        if (!FirmaValida(cabecera.AsSpan(0, leidos), mime))
        {
            throw new ArchivoInvalidoException("El archivo no corresponde al formato declarado.");
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
            "App_Data",
            "documentos",
            estudianteId.ToString(),
            $"{Guid.NewGuid():N}{extension}");

        var fisica = Path.Combine(_entorno.ContentRootPath, relativa);
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
    public string? RutaFisica(string relativa)
    {
        var raiz = Path.GetFullPath(Path.Combine(_entorno.ContentRootPath, "App_Data", "documentos"))
                   + Path.DirectorySeparatorChar;
        var fisica = Path.GetFullPath(Path.Combine(_entorno.ContentRootPath, relativa));

        return fisica.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) && File.Exists(fisica)
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
