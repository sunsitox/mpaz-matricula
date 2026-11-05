namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Persona autorizada a retirar al estudiante (tabla <c>public.retiro_autorizado</c>).
/// La ficha pide RUN y vínculo; el nombre se completa con el de la persona si ya
/// está registrada y, si no, se deja el RUN como referencia.
/// </summary>
public class RetiroAutorizado
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string TipoIdentificador { get; set; } = "RUN";

    public string Identificador { get; set; } = string.Empty;

    public string Vinculo { get; set; } = string.Empty;

    public DateOnly? VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }
}
