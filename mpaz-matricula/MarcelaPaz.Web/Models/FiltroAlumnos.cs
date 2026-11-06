namespace MarcelaPaz.Web.Models;

/// <summary>
/// Filtros activos del listado de alumnos y sus opciones. La matrícula que se
/// consulta es la del año en curso.
/// </summary>
public class FiltroAlumnos
{
    public int Anio { get; set; }

    /// <summary>Nombre o RUN, con o sin puntos.</summary>
    public string? Texto { get; set; }

    public string? Curso { get; set; }

    public string? Estado { get; set; }

    public List<AlumnoListado> Alumnos { get; set; } = new();

    public List<string> Cursos { get; set; } = new();

    public List<string> Estados { get; set; } = new();

    public bool TieneFiltros =>
        !string.IsNullOrWhiteSpace(Texto)
        || !string.IsNullOrWhiteSpace(Curso)
        || !string.IsNullOrWhiteSpace(Estado);
}
