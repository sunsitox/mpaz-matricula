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
}