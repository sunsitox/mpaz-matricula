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
}
