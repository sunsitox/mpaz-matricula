namespace MarcelaPaz.Web.Models;

/// <summary>Fila del listado de alumnos: datos del registro único y matrícula del año.</summary>
public class AlumnoListado
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Documento tal como está guardado: RUN, IPE o IPA.</summary>
    public string Identificador { get; set; } = string.Empty;

    /// <summary>RUN, IPE o IPA.</summary>
    public string TipoIdentificador { get; set; } = "RUN";

    /// <summary>Documento listo para pantalla: RUN con puntos, IPE/IPA en mayúsculas.</summary>
    public string Rut { get; set; } = string.Empty;

    public string? Curso { get; set; }

    /// <summary>Estado de la matrícula del año (BORRADOR, FIRMADA, ...), si existe.</summary>
    public string? Estado { get; set; }

    /// <summary>NUEVA o RENOVACION, si existe matrícula del año.</summary>
    public string? Tipo { get; set; }

    public string? Comuna { get; set; }

    /// <summary>La matrícula del año todavía no tiene una ficha vigente.</summary>
    public bool SinFicha { get; set; }

    /// <summary>Requisitos documentales de la matrícula que siguen en PENDIENTE.</summary>
    public int DocumentosPendientes { get; set; }

    /// <summary>Pendientes que impiden dar la matrícula por completa.</summary>
    public int Pendientes => DocumentosPendientes + (SinFicha ? 1 : 0);

    public bool TienePendientes => Pendientes > 0;

    public string PendientesTexto => (SinFicha, DocumentosPendientes) switch
    {
        (true, 0) => "Sin ficha",
        (false, 1) => "1 documento",
        (false, > 1) => $"{DocumentosPendientes} documentos",
        (true, 1) => "Sin ficha · 1 documento",
        (true, > 1) => $"Sin ficha · {DocumentosPendientes} documentos",
        _ => "—",
    };

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
