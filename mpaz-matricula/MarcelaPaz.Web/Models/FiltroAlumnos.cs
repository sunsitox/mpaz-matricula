namespace MarcelaPaz.Web.Models;

/// <summary>
/// Filtros activos del listado de alumnos y sus opciones. La matrícula que se
/// consulta es la del año en curso.
/// </summary>
public class FiltroAlumnos
{
    /// <summary>Año consultado: el activo por omisión, o uno anterior desde el selector.</summary>
    public int Anio { get; set; }

    /// <summary>Años disponibles en el selector, del más reciente al más antiguo.</summary>
    public List<int> Anios { get; set; } = new();

    /// <summary>Nombre o RUN, con o sin puntos.</summary>
    public string? Texto { get; set; }

    public string? Curso { get; set; }

    public string? Estado { get; set; }

    /// <summary>Muestra sólo las matrículas con ficha o documentos pendientes (MAT-11).</summary>
    public bool SoloPendientes { get; set; }

    /// <summary>Columna por la que está ordenado el listado.</summary>
    public string Orden { get; set; } = "nombre";

    /// <summary>«asc» o «desc».</summary>
    public string Direccion { get; set; } = "asc";

    public List<AlumnoListado> Alumnos { get; set; } = new();

    public List<string> Cursos { get; set; } = new();

    public List<string> Estados { get; set; } = new();

    public bool TieneFiltros =>
        !string.IsNullOrWhiteSpace(Texto)
        || !string.IsNullOrWhiteSpace(Curso)
        || !string.IsNullOrWhiteSpace(Estado)
        || SoloPendientes;
}
