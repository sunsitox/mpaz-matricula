namespace MarcelaPaz.Web.Models;

/// <summary>
/// Resultado de buscar un RUN antes de abrir la ficha. Determina si corresponde
/// matrícula nueva, renovación o si ya existe una matrícula cerrada para el año.
/// </summary>
public class BusquedaFicha
{
    public string? Rut { get; set; }

    /// <summary>True cuando el usuario ya envió una búsqueda.</summary>
    public bool Buscado { get; set; }

    public int? EstudianteId { get; set; }

    public string? NombreCompleto { get; set; }

    public short? AnioAnterior { get; set; }

    public string? CursoAnterior { get; set; }

    /// <summary>Estado de la matrícula del año en curso, si ya existe.</summary>
    public string? EstadoVigente { get; set; }

    public bool PuedeRenovar { get; set; }

    public bool PuedeInscribir { get; set; }

    public bool Existe => EstudianteId is not null;

    /// <summary>Ya hay matrícula cerrada para el año: no se crea otro expediente.</summary>
    public bool YaMatriculado => EstadoVigente is not null;
}
