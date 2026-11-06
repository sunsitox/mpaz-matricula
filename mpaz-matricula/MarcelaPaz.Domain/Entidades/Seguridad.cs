namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Persona que trabaja en la escuela y opera el portal. Corresponde a la tabla
/// <c>funcionario</c> del DDL: la identidad institucional vive aquí y la
/// contraseña vive en Identity, enlazada por <see cref="SsoSujeto"/>.
/// </summary>
public class Funcionario
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    /// <summary>Identificador del usuario en el inicio de sesión único (aquí, el id de Identity).</summary>
    public string? SsoSujeto { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<FuncionarioRol> Roles { get; set; } = new List<FuncionarioRol>();
}

/// <summary>Perfil institucional (SECRETARIA, DIRECCION, INSPECTORIA, EQUIPO_PIE, TENS, DOCENTE, ADMIN_TI).</summary>
public class Rol
{
    public int Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
}

/// <summary>
/// Asignación de un rol a un funcionario, con vigencia. Un cambio de cargo cierra
/// la fila con <see cref="Hasta"/>; no se borra, para conservar la trazabilidad.
/// </summary>
public class FuncionarioRol
{
    public int FuncionarioId { get; set; }

    public int RolId { get; set; }

    public DateOnly Desde { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? Hasta { get; set; }

    public Funcionario? Funcionario { get; set; }

    public Rol? Rol { get; set; }
}

/// <summary>Qué acción puede ejecutar un rol sobre cada área de datos (MAT-12).</summary>
public class RolPermiso
{
    public int RolId { get; set; }

    public string Area { get; set; } = string.Empty;

    public string Accion { get; set; } = string.Empty;
}
