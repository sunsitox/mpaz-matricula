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

    /// <summary>Sólo la página actual del listado, no el padrón completo.</summary>
    public List<AlumnoListado> Alumnos { get; set; } = new();

    /// <summary>Total de alumnos filtrados, sin paginar.</summary>
    public int TotalAlumnos { get; set; }

    /// <summary>Página actual, desde 1.</summary>
    public int Pagina { get; set; } = 1;

    /// <summary>Alumnos por página.</summary>
    public int TamanoPagina { get; set; } = 50;

    public int TotalPaginas => TamanoPagina <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalAlumnos / (double)TamanoPagina));

    public bool HayAnterior => Pagina > 1;

    public bool HaySiguiente => Pagina < TotalPaginas;

    /// <summary>Primer alumno visible de la página (0 si no hay resultados).</summary>
    public int Desde => TotalAlumnos == 0 ? 0 : (Pagina - 1) * TamanoPagina + 1;

    /// <summary>Último alumno visible de la página (0 si no hay resultados).</summary>
    public int Hasta => Math.Min(Pagina * TamanoPagina, TotalAlumnos);

    public List<string> Cursos { get; set; } = new();

    public List<string> Estados { get; set; } = new();

    public bool TieneFiltros =>
        !string.IsNullOrWhiteSpace(Texto)
        || !string.IsNullOrWhiteSpace(Curso)
        || !string.IsNullOrWhiteSpace(Estado)
        || SoloPendientes;
}
