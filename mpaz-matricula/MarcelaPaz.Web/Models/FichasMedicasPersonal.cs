namespace MarcelaPaz.Web.Models;

/// <summary>
/// Vista de las fichas médicas del personal: el formulario de registro y el
/// listado de documentos cargados. Sólo la abre UTP.
/// </summary>
public sealed class FichasMedicasPersonal
{
    public List<FuncionarioOpcion> Funcionarios { get; set; } = new();

    public List<FichaMedicaItem> Fichas { get; set; } = new();

    /// <summary>Texto del buscador (funcionario, tipo, descripción o emisor).</summary>
    public string Busqueda { get; set; } = string.Empty;

    /// <summary>Funcionario seleccionado en el filtro (0 = todos).</summary>
    public int FuncionarioFiltro { get; set; }

    /// <summary>Estado de vigencia: todas, vigentes, vencidas o sin fecha.</summary>
    public string EstadoFiltro { get; set; } = "todas";

    public int TotalFichas { get; set; }

    public int TotalFuncionariosConFicha { get; set; }

    public sealed class FuncionarioOpcion
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;
    }

    public sealed class FichaMedicaItem
    {
        public int Id { get; set; }

        public int FuncionarioId { get; set; }

        public string Funcionario { get; set; } = string.Empty;

        public string Tipo { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public string? Emisor { get; set; }

        public DateOnly? EmitidoEn { get; set; }

        public DateOnly? VigenteHasta { get; set; }

        public string? ArchivoNombre { get; set; }

        public bool TieneArchivo { get; set; }

        public DateTimeOffset RegistradoEn { get; set; }
    }
}
