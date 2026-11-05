namespace MarcelaPaz.Domain.Entidades;

/// <summary>Situación médica declarada (tabla <c>public.situacion_medica</c>).</summary>
public class SituacionMedica
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    /// <summary>ALERGIA, CONDICION u OTRO.</summary>
    public string Tipo { get; set; } = "CONDICION";

    public string Descripcion { get; set; } = string.Empty;

    public bool? EnTratamiento { get; set; }

    public DateOnly? VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }
}
