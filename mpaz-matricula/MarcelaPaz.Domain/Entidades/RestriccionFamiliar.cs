namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Restricción judicial o familiar que afecta el vínculo con el estudiante
/// (tabla <c>public.restriccion_familiar</c>).
/// </summary>
public class RestriccionFamiliar
{
    public int Id { get; set; }

    public int EstudiantePersonaId { get; set; }

    public EstudiantePersona? EstudiantePersona { get; set; }

    /// <summary>JUDICIAL o FAMILIAR.</summary>
    public string Tipo { get; set; } = "FAMILIAR";

    public string Descripcion { get; set; } = string.Empty;

    public DateOnly? VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }
}
