namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Observación de situación familiar del estudiante (tabla
/// <c>observacion_situacion_familiar</c>, pasos/21). Área propia
/// <c>SITUACION_FAMILIAR</c>: solo UTP y Convivencia la registran;
/// Dirección la lee. No es salud ni judicial, y no lleva adjuntos en
/// esta versión.
/// </summary>
public class ObservacionSituacionFamiliar
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    public string Detalle { get; set; } = string.Empty;

    public DateOnly? VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public int? RegistradoPor { get; set; }

    public DateTimeOffset RegistradoEn { get; set; } = DateTimeOffset.UtcNow;
}
