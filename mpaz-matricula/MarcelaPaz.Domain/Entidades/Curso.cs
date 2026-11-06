namespace MarcelaPaz.Domain.Entidades;

/// <summary>Curso del colegio (tabla <c>public.curso</c>), por ejemplo «1° A».</summary>
public class Curso
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; }
}
