namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Orden médica vigente para un medicamento (tabla <c>prescripcion</c>, MAT-05).
/// Una autorización MEDICACION por medicamento; el trigger valida vigencia.
/// Base legal: Código Sanitario (DFL 725) arts. 112-113 y Decreto 90
/// (profesiones auxiliares): el TENS actúa con indicación y supervigilancia
/// médica, sin prescribir ni diagnosticar.
/// </summary>
public class Prescripcion
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    public int AutorizacionId { get; set; }

    public string Medicamento { get; set; } = string.Empty;

    public decimal DosisCantidad { get; set; }

    public string DosisUnidad { get; set; } = string.Empty;

    public string Via { get; set; } = string.Empty;

    public string Frecuencia { get; set; } = string.Empty;

    public string? Horario { get; set; }

    public DateOnly VigenteDesde { get; set; }

    public DateOnly VigenteHasta { get; set; }

    /// <summary>PENDIENTE_VALIDACION, VIGENTE, SUSPENDIDA o VENCIDA.</summary>
    public string Estado { get; set; } = "PENDIENTE_VALIDACION";
}

/// <summary>
/// Dosis efectivamente administrada por un TENS (tabla
/// <c>administracion_medicamento</c>, solo-inserción, MAT-05). Es un registro
/// de ficha clínica: Ley 20.584 arts. 12-13 y Decreto 41 (datos sensibles,
/// conservación de al menos 15 años, acceso solo de quien participa en la
/// atención).
/// </summary>
public class AdministracionMedicamento
{
    public int Id { get; set; }

    public int PrescripcionId { get; set; }

    public DateTimeOffset AdministradoEn { get; set; }

    public decimal DosisCantidad { get; set; }

    public string DosisUnidad { get; set; } = string.Empty;

    public int TensId { get; set; }

    public string? TensNombre { get; set; }

    public string? Incidencias { get; set; }
}
