namespace MarcelaPaz.Domain.Entidades;

/// <summary>Contacto de emergencia del estudiante (tabla <c>public.contacto_emergencia</c>).</summary>
public class ContactoEmergencia
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    /// <summary>Posición dentro de la ficha: 1 o 2.</summary>
    public short Orden { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;

    public string Vinculo { get; set; } = string.Empty;

    public DateOnly? VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }
}
