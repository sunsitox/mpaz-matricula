namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Persona adulta vinculada al estudiante (tabla <c>public.persona</c>): madre,
/// padre o apoderado. La ficha pide el nombre completo en un solo campo, así que
/// se guarda tal cual en <see cref="Nombres"/> y <see cref="Apellidos"/> queda vacío.
/// </summary>
public class Persona
{
    public int Id { get; set; }

    public string TipoIdentificador { get; set; } = "RUN";

    public string Identificador { get; set; } = string.Empty;

    public string Nombres { get; set; } = string.Empty;

    public string Apellidos { get; set; } = string.Empty;

    public string? Telefono { get; set; }

    public string? Correo { get; set; }

    public string? Domicilio { get; set; }

    public string? Escolaridad { get; set; }
}
