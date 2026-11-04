namespace MarcelaPaz.Web.Models;

/// <summary>
/// Datos del acceso al portal de apoderados (mockup 01 · acceso-apoderado).
/// Por ahora solo los dos RUT; el año de nacimiento del estudiante se agrega
/// cuando se acuerde la verificación con el colegio.
/// </summary>
public class AccesoForm
{
    /// <summary>RUT del estudiante cuyo expediente se quiere revisar.</summary>
    public string? RutEstudiante { get; set; }

    public DateTime? FechaNacimientoEstudiante { get; set; }

    /// <summary>RUT del apoderado que solicita el ingreso.</summary>
    public string? RutApoderado { get; set; }
}
