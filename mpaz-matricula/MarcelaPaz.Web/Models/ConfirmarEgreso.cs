namespace MarcelaPaz.Web.Models;

/// <summary>
/// Datos para la confirmación que aparece al renovar a un estudiante cuya
/// matrícula anterior fue en 8º básico: no hay curso superior, así que hay
/// que elegir entre dejarlo como egresado o registrar que repite el nivel.
/// </summary>
public class ConfirmarEgresoModelo
{
    public int EstudianteId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public int AnioAnterior { get; set; }

    /// <summary>Nombre del curso de la matrícula anterior («8° A»).</summary>
    public string? CursoAnterior { get; set; }

    public int AnioNuevo { get; set; }
}
