namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Resumen de antecedentes de salud, uno por estudiante (tabla
/// <c>public.antecedente_salud</c>).
/// </summary>
public class AntecedenteSalud
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    public bool? TieneSituacionMedica { get; set; }

    /// <summary>Necesidades de atención declaradas en la ficha (por ejemplo, salidas al baño).</summary>
    public string? NecesidadesAtencion { get; set; }

    /// <summary>Persona a quien avisar ante una urgencia sanitaria.</summary>
    public string? ContactoSanitarioNombre { get; set; }

    public string? ContactoSanitarioTelefono { get; set; }
}
