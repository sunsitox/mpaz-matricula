namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Registro de cada certificado de alumno regular emitido por el portal.
/// El folio identifica el papel; el hash del código de verificación permite
/// validar su autenticidad sin guardar el código en claro. La tabla vive en
/// el esquema del módulo (pasos/08) y no la administra EF: sólo lectura y
/// escritura del portal.
/// </summary>
public class CertificadoEmitido
{
    public int Id { get; set; }

    /// <summary>Folio impreso, único: CAR-año-correlativo (p. ej. CAR-2027-000014).</summary>
    public string Folio { get; set; } = string.Empty;

    /// <summary>SHA-256 en hexadecimal del código de verificación entregado.</summary>
    public string CodigoHash { get; set; } = string.Empty;

    public int MatriculaId { get; set; }

    public int EstudianteId { get; set; }

    public short Anio { get; set; }

    /// <summary>Funcionario que emitió (<c>emitido_por</c>).</summary>
    public int? EmitidoPor { get; set; }

    public DateTimeOffset EmitidoEn { get; set; }

    /// <summary>Finalidad declarada al emitir, si se indicó una.</summary>
    public string? Proposito { get; set; }

    /// <summary>Anulación del documento (pierde validez en /Validar).</summary>
    public DateTimeOffset? AnuladoEn { get; set; }

    public int? AnuladoPor { get; set; }

    /// <summary>
    /// Nombre del subrogante que firma: el funcionario que emite. Nulo en
    /// certificados emitidos antes del paso 20.
    /// </summary>
    public string? FirmanteNombre { get; set; }

    /// <summary>Cargo impreso en el certificado: «Director» (paso 20).</summary>
    public string? FirmanteCargo { get; set; }
}