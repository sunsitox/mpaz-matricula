namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Texto aprobado de una autorización (tabla <c>plantilla_autorizacion</c>).
/// Mientras Dirección no apruebe el texto definitivo, la versión 1 queda
/// registrada como pendiente de aprobación.
/// </summary>
public class PlantillaAutorizacion
{
    public int Id { get; set; }

    /// <summary>IMAGEN, CONVIVENCIA o MEDICACION.</summary>
    public string Tipo { get; set; } = string.Empty;

    public short Version { get; set; } = 1;

    public string Texto { get; set; } = string.Empty;

    public string? AprobadoPor { get; set; }

    public DateOnly VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }
}

/// <summary>
/// Respuesta del apoderado a una autorización (tabla <c>autorizacion</c>), con
/// su estado y los alcances otorgados (tabla <c>autorizacion_alcance</c>).
/// </summary>
public class Autorizacion
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    /// <summary>IMAGEN, CONVIVENCIA o MEDICACION.</summary>
    public string Tipo { get; set; } = string.Empty;

    public int PlantillaId { get; set; }

    /// <summary>PENDIENTE, OTORGADA, RECHAZADA, REVOCADA, VENCIDA o REEMPLAZADA.</summary>
    public string Estado { get; set; } = "PENDIENTE";

    public string? Proposito { get; set; }

    public int? FichaVersionId { get; set; }

    /// <summary>Persona que firma la autorización (madre, padre o apoderado).</summary>
    public int? FirmantePersonaId { get; set; }

    public DateOnly? FechaFirma { get; set; }

    public DateOnly? VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public DateTimeOffset? RevocadaEn { get; set; }

    public string? MotivoRevocacion { get; set; }

    public List<AutorizacionAlcance> Alcances { get; set; } = new();
}

/// <summary>Alcance concreto de una autorización (por ejemplo, uso en redes sociales).</summary>
public class AutorizacionAlcance
{
    public int Id { get; set; }

    public int AutorizacionId { get; set; }

    public string Alcance { get; set; } = string.Empty;

    public bool Otorgado { get; set; }
}
