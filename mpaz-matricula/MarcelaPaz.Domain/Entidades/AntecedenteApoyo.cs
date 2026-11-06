namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Resumen PIE del estudiante, uno por estudiante (tabla
/// <c>public.antecedente_apoyo</c>): participación, apoyos y existencia de
/// diagnóstico o documentación.
/// </summary>
public class AntecedenteApoyo
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    /// <summary>SI, NO o PENDIENTE, como lo exige el DDL.</summary>
    public string ParticipaPie { get; set; } = "PENDIENTE";

    public string? ApoyosInformados { get; set; }

    public bool? TieneDiagnostico { get; set; }
}
