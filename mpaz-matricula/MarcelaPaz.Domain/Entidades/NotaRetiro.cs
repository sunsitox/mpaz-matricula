namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Nota de retiro del estudiante (tabla <c>nota_retiro</c>). UTP registra la
/// situación vigente —orden de alejamiento, retiro autorizado o retiro no
/// autorizado— con su detalle y, si corresponde, la fecha hasta la que rige.
/// No reemplaza a <see cref="RetiroAutorizado"/>: documenta la decisión.
/// </summary>
public class NotaRetiro
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    /// <summary>ORDEN_ALEJAMIENTO, AUTORIZADO o NO_AUTORIZADO.</summary>
    public string Tipo { get; set; } = "AUTORIZADO";

    public string Detalle { get; set; } = string.Empty;

    public DateOnly? VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public int? RegistradoPor { get; set; }

    public DateTimeOffset RegistradoEn { get; set; } = DateTimeOffset.UtcNow;
}
