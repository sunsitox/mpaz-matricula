namespace MarcelaPaz.Infrastructure.Documentos;

/// <summary>
/// Contenido del certificado de alumno regular. Estructura plana: la capa
/// Web la arma desde la matrícula vigente y este generador no consulta la
/// base de datos. El QR llega ya renderizado como PNG.
/// </summary>
public sealed class DocumentoCertificado
{
    public required string Sostenedor { get; init; }

    public required string Establecimiento { get; init; }

    public required string Rbd { get; init; }

    /// <summary>Línea de reconocimiento oficial (decreto). Null si aún no se confirma el número.</summary>
    public string? Reconocimiento { get; init; }

    public required string Direccion { get; init; }

    public required string Contacto { get; init; }

    public required string AlumnoNombre { get; init; }

    public required string AlumnoRut { get; init; }

    public string? FechaNacimiento { get; init; }

    public required string Curso { get; init; }

    public required int Anio { get; init; }

    /// <summary>Finalidad declarada o la frase genérica.</summary>
    public required string Proposito { get; init; }

    /// <summary>Ya formateado: "San Bernardo, 10 de noviembre de 2026".</summary>
    public required string LugarFecha { get; init; }

    public required string FirmanteNombre { get; init; }

    public required string FirmanteCargo { get; init; }

    public required string Folio { get; init; }

    /// <summary>Código de verificación en claro, sólo para imprimir.</summary>
    public required string Codigo { get; init; }

    public required string UrlValidacion { get; init; }

    /// <summary>QR (PNG) que apunta a <see cref="UrlValidacion"/>.</summary>
    public byte[]? Qr { get; init; }

    public byte[]? Logo { get; init; }
}