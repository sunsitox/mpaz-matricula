namespace MarcelaPaz.Web.Models;

/// <summary>
/// Resultado público de validar un certificado de alumno regular por folio
/// y código. Sólo muestra lo impreso en el papel que ya tiene quien valida.
/// </summary>
public sealed record ValidacionCertificado
{
    public bool Valido { get; init; }

    public string Folio { get; init; } = string.Empty;

    public string? Establecimiento { get; init; }

    public string? AlumnoNombre { get; init; }

    public string? Curso { get; init; }

    public int? Anio { get; init; }

    public DateTimeOffset? EmitidoEn { get; init; }

    /// <summary>Fin de vigencia impreso en el papel: emisión + 30 días.</summary>
    public DateTimeOffset? VigenteHasta { get; init; }

    /// <summary>
    /// El certificado existe y es auténtico, pero pasó su plazo de vigencia.
    /// Se distingue de «no válido» para no confundir un documento vencido con
    /// un folio mal escrito.
    /// </summary>
    public bool Vencido { get; init; }
}