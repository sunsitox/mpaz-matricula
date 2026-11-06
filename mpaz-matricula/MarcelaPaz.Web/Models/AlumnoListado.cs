namespace MarcelaPaz.Web.Models;

/// <summary>Fila del listado de alumnos: datos del registro único y matrícula del año.</summary>
public class AlumnoListado
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>RUN con puntos, como se muestra en pantalla.</summary>
    public string Rut { get; set; } = string.Empty;

    public string? Curso { get; set; }

    /// <summary>Estado de la matrícula del año (BORRADOR, FIRMADA, ...), si existe.</summary>
    public string? Estado { get; set; }

    /// <summary>NUEVA o RENOVACION, si existe matrícula del año.</summary>
    public string? Tipo { get; set; }

    public string? Comuna { get; set; }

    public bool TieneMatricula => !string.IsNullOrWhiteSpace(Estado);

    public string EstadoTexto => TieneMatricula ? Estado! : "Sin matrícula";

    /// <summary>Clase del distintivo de estado, para colorearlo en la tabla.</summary>
    public string EstadoClase => !TieneMatricula
        ? "alumnos__estado--sin"
        : Estado is "FIRMADA" or "CONFIRMADA" or "FORMALIZADA"
            ? "alumnos__estado--cerrada"
            : Estado == "BORRADOR"
                ? "alumnos__estado--borrador"
                : "alumnos__estado--otro";

    public string TipoTexto => Tipo switch
    {
        "RENOVACION" => "Renovación",
        "NUEVA" => "Nueva",
        _ => "—",
    };
}
