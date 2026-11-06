namespace MarcelaPaz.Web.Models;

/// <summary>Fila del listado de funcionarios con sus roles vigentes.</summary>
public class FuncionarioListado
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public bool Activo { get; set; }

    public List<RolAsignado> Roles { get; set; } = new();

    public class RolAsignado
    {
        public int RolId { get; set; }

        public string Codigo { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;
    }
}
