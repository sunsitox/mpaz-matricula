namespace MarcelaPaz.Domain.Identificacion;

/// <summary>
/// Regla única de identificadores, sin rangos numéricos inventados:
/// estudiante RUN o IPE, adulto RUN o IPA.
/// El provisorio es genérico (3 a 20 caracteres alfanuméricos, guion o punto,
/// sin espacios) y se distingue por prefijo: IPE es del estudiante, IPA del apoderado.
/// </summary>
public static class DocumentoIdentidad
{
    /// <summary>RUN válido, o IPA provisorio. Rechaza el IPE del estudiante.</summary>
    public static bool EsDocumentoAdulto(string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            return false;
        }

        if (RutChileno.EsValido(documento))
        {
            return true;
        }

        var limpio = documento.Trim();

        // Un RUN con forma de RUN pero DV inválido no se rescata como IPA.
        if (TieneFormaRun(limpio))
        {
            return false;
        }

        if (limpio.StartsWith("IPE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return EsProvisorio(limpio);
    }

    /// <summary>RUN válido, o IPE provisorio. Rechaza el IPA del apoderado.</summary>
    public static bool EsDocumentoEstudiante(string? documento, string? tipo)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            return false;
        }

        var tipoLimpio = string.IsNullOrWhiteSpace(tipo) ? "RUN" : tipo.Trim().ToUpperInvariant();

        if (tipoLimpio == "RUN")
        {
            return RutChileno.EsValido(documento);
        }

        if (tipoLimpio != "IPE")
        {
            return false;
        }

        var limpio = documento.Trim();

        if (limpio.StartsWith("IPA", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return EsProvisorio(limpio);
    }

    /// <summary>RUN limpio con guion, provisorios en mayúsculas.</summary>
    public static string NormalizarParaGuardar(string documento) =>
        RutChileno.EsValido(documento)
            ? RutChileno.Limpiar(documento)
            : documento.Trim().ToUpperInvariant();

    /// <summary>RUN con puntos, provisorios tal como están guardados.</summary>
    public static string ParaMostrar(string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            return string.Empty;
        }

        return RutChileno.EsValido(documento)
            ? RutChileno.Formatear(documento)
            : documento.Trim().ToUpperInvariant();
    }

    /// <summary>Tipo a guardar para un documento adulto ya validado.</summary>
    public static string TipoAdulto(string documento) =>
        RutChileno.EsValido(documento) ? "RUN" : "IPA";

    /// <summary>Compara dos documentos por su forma guardada; vacíos nunca coinciden.</summary>
    public static bool MismoDocumento(string? uno, string? otro)
    {
        if (string.IsNullOrWhiteSpace(uno) || string.IsNullOrWhiteSpace(otro))
        {
            return false;
        }

        return string.Equals(
            NormalizarParaGuardar(uno),
            NormalizarParaGuardar(otro),
            StringComparison.Ordinal);
    }

    private static bool EsProvisorio(string limpio) =>
        limpio.Length is >= 3 and <= 20
        && limpio.All(c => char.IsLetterOrDigit(c) || c is '-' or '.');

    private static bool TieneFormaRun(string texto)
    {
        var sinPuntos = texto.Replace(".", string.Empty).Trim();
        var partes = sinPuntos.Split('-');

        return partes.Length == 2
            && partes[0].Length > 0 && partes[0].All(char.IsDigit)
            && partes[1].Length == 1 && char.IsLetterOrDigit(partes[1][0]);
    }
}
