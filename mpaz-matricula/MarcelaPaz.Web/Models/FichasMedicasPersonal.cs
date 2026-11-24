namespace MarcelaPaz.Web.Models;

/// <summary>
/// Vista de las fichas de salud del personal: el listado de fichas
/// registradas y los funcionarios disponibles para completar una nueva.
/// Sólo la abre UTP y Dirección.
/// </summary>
public sealed class FichasMedicasPersonal
{
    public List<FuncionarioOpcion> Funcionarios { get; set; } = new();

    public List<FichaSaludItem> Fichas { get; set; } = new();

    /// <summary>Funcionarios activos que aún no tienen ficha.</summary>
    public List<FuncionarioOpcion> SinFicha { get; set; } = new();

    /// <summary>Texto del buscador (funcionario, documento, contacto o centro médico).</summary>
    public string Busqueda { get; set; } = string.Empty;

    /// <summary>Funcionario seleccionado en el filtro (0 = todos).</summary>
    public int FuncionarioFiltro { get; set; }

    public int TotalRegistros { get; set; }

    public int TotalFuncionariosConFicha { get; set; }

    public int TotalFuncionariosActivos { get; set; }

    public sealed class FuncionarioOpcion
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;
    }

    public sealed class FichaSaludItem
    {
        public int Id { get; set; }

        public int FuncionarioId { get; set; }

        public string Funcionario { get; set; } = string.Empty;

        /// <summary>RUN o pasaporte, listo para mostrar.</summary>
        public string Documento { get; set; } = string.Empty;

        public string? Telefono { get; set; }

        /// <summary>Primer contacto de emergencia, con su teléfono.</summary>
        public string? ContactoEmergencia { get; set; }

        public string? SistemaSalud { get; set; }

        public DateTimeOffset RegistradoEn { get; set; }
    }
}
