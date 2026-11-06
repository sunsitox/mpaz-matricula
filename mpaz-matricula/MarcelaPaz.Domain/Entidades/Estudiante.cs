namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Registro único de estudiantes del DDL institucional (tabla <c>public.estudiante</c>).
/// Solo se mapea lo que la ficha necesita leer para decidir entre matrícula nueva
/// y renovación.
/// </summary>
public class Estudiante
{
    public int Id { get; set; }

    public string TipoIdentificador { get; set; } = "RUN";

    /// <summary>RUN normalizado tal como lo guarda la base: sin puntos y con guion.</summary>
    public string Identificador { get; set; } = string.Empty;

    public string Nombres { get; set; } = string.Empty;

    public string ApellidoPaterno { get; set; } = string.Empty;

    public string? ApellidoMaterno { get; set; }

    public DateOnly FechaNacimiento { get; set; }

    public string Nacionalidad { get; set; } = "Chilena";

    public string Domicilio { get; set; } = string.Empty;

    public int? ComunaId { get; set; }

    public Comuna? Comuna { get; set; }

    public bool? SaleSoloFinalJornada { get; set; }

    public string NombreCompleto =>
        string.Join(' ', new[] { Nombres, ApellidoPaterno, ApellidoMaterno }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
}
