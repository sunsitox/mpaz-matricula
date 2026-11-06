namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Relación vigente entre un estudiante y una persona (tabla
/// <c>public.estudiante_persona</c>): madre, padre o apoderado principal/secundario.
/// </summary>
public class EstudiantePersona
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    public Estudiante? Estudiante { get; set; }

    public int PersonaId { get; set; }

    public Persona? Persona { get; set; }

    /// <summary>MADRE, PADRE u OTRO.</summary>
    public string Parentesco { get; set; } = "OTRO";

    /// <summary>Vínculo declarado cuando el parentesco es OTRO (por ejemplo, abuela).</summary>
    public string? Vinculo { get; set; }

    /// <summary>PRINCIPAL o SECUNDARIO; nulo para la madre y el padre.</summary>
    public string? TipoApoderado { get; set; }

    public bool? TieneRestriccion { get; set; }

    public DateOnly? VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }
}
