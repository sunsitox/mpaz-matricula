namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Matrícula de un estudiante en un año escolar (tabla <c>public.matricula_anual</c>).
/// Cada año tiene su propio expediente: una renovación crea un registro nuevo y
/// nunca reescribe el anterior.
/// </summary>
public class MatriculaAnual
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    public short Anio { get; set; }

    public int? CursoId { get; set; }

    public Curso? Curso { get; set; }

    /// <summary>NUEVA o RENOVACION.</summary>
    public string Tipo { get; set; } = "NUEVA";

    /// <summary>BORRADOR, FIRMADA, CONFIRMADA u otro estado del DDL.</summary>
    public string Estado { get; set; } = "BORRADOR";

    /// <summary>Funcionario que creó el expediente (<c>creada_por</c>).</summary>
    public int? CreadaPor { get; set; }

    /// <summary>Creación del registro (<c>created_at</c>).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Última modificación, para el indicador de actualización del panel (<c>updated_at</c>).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Versión de bloqueo optimista (<c>version</c>); la mantiene un trigger.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Datos de la confirmación oficial (MAT-04); la base exige ambos cuando el estado es CONFIRMADA.</summary>
    public DateTimeOffset? ConfirmadaEn { get; set; }

    public int? ConfirmadaPor { get; set; }

    /// <summary>Motivo de la anulación, cuando el estado es ANULADA.</summary>
    public string? Observaciones { get; set; }

    /// <summary>Validación institucional de Dirección (MAT-12): acto distinto
    /// de confirmar, con fecha y responsable (<c>validada_en</c>).</summary>
    public DateTimeOffset? ValidadaEn { get; set; }

    public int? ValidadaPor { get; set; }
}
