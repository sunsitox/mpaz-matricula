namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Archivo del expediente (tabla <c>documento</c>). Se guarda fuera de los
/// directorios públicos, con hash y sensibilidad por área. Un documento anterior
/// nunca se borra: queda inactivo y enlazado con <see cref="ReemplazaId"/>.
/// </summary>
public class Documento
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    public int? MatriculaId { get; set; }

    /// <summary>FICHA_PDF, FICHA_FIRMADA, CEDULA_IDENTIDAD, ORDEN_MEDICA, ...</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>GENERAL, SALUD, PIE o JUDICIAL: área que controla su lectura.</summary>
    public string Sensibilidad { get; set; } = "GENERAL";

    public string NombreOriginal { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public int TamanoBytes { get; set; }

    /// <summary>Ruta relativa al contenido de la aplicación, fuera de wwwroot.</summary>
    public string RutaAlmacenamiento { get; set; } = string.Empty;

    public string HashSha256 { get; set; } = string.Empty;

    public string EstadoAnalisis { get; set; } = "PENDIENTE";

    public bool Activo { get; set; } = true;

    public int? ReemplazaId { get; set; }

    public int SubidoPor { get; set; }

    public DateTimeOffset SubidoEn { get; set; }
}

/// <summary>
/// Estado de un documento requerido para una matrícula (tabla
/// <c>requisito_documental</c>): pendiente, recibido o no aplica. No bloquea la
/// matrícula por sí mismo (MAT-07).
/// </summary>
public class RequisitoDocumental
{
    public int Id { get; set; }

    public int MatriculaId { get; set; }

    public string TipoDocumento { get; set; } = string.Empty;

    public string Estado { get; set; } = "PENDIENTE";

    public int? DocumentoId { get; set; }

    public string? Observacion { get; set; }

    public int? RegistradoPor { get; set; }

    public DateTimeOffset RegistradoEn { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Version { get; set; } = 1;
}
