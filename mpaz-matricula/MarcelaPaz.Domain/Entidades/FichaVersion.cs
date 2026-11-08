namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Versión cerrada de la ficha de matrícula (tabla <c>ficha_version</c>). Guarda
/// el folio que asigna la base, la copia congelada de los datos y quién la generó.
/// El contenido del PDF se arma siempre desde <see cref="DatosSnapshot"/>.
/// </summary>
public class FichaVersion
{
    public int Id { get; set; }

    public int MatriculaId { get; set; }

    public short NumeroVersion { get; set; } = 1;

    /// <summary>Folio institucional, por ejemplo MP-2027-00001.</summary>
    public string Folio { get; set; } = string.Empty;

    /// <summary>VIGENTE o REEMPLAZADA.</summary>
    public string Estado { get; set; } = "VIGENTE";

    /// <summary>Copia congelada de los datos de la ficha en formato JSON.</summary>
    public string DatosSnapshot { get; set; } = "{}";

    public int GeneradaPor { get; set; }

    public DateTimeOffset GeneradaEn { get; set; }

    public int? PdfDocumentoId { get; set; }

    /// <summary>Fecha de la firma manuscrita y persona firmante (Etapa I: firma sin escaneo).</summary>
    public DateOnly? FechaFirma { get; set; }

    public int? FirmantePersonaId { get; set; }
}
