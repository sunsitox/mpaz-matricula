namespace MarcelaPaz.Web.Models;

/// <summary>
/// Indicadores de la portada: avance de la matrícula del año, pendientes
/// operativos y actividad reciente. Son cifras agregadas, sin datos personales.
/// </summary>
public class PanelInicio
{
    public int Anio { get; set; }

    /// <summary>Total de expedientes del año (borradores incluidos).</summary>
    public int TotalMatriculas { get; set; }

    /// <summary>Expedientes firmados, confirmados o formalizados.</summary>
    public int Cerradas { get; set; }

    public int Borradores { get; set; }

    public int Renovaciones { get; set; }

    public int Nuevas { get; set; }

    public int FichasConFolio { get; set; }

    public int PendientesDocumentales { get; set; }

    public int AutorizacionesVigentes { get; set; }

    public int AlertasVencimiento { get; set; }

    public int EstudiantesRegistrados { get; set; }

    public int FuncionariosActivos { get; set; }

    public DateTimeOffset? ActualizadoEn { get; set; }

    public string? Responsable { get; set; }

    /// <summary>Mensaje cuando la base no respondió y el panel va en cero.</summary>
    public string? Error { get; set; }

    public List<CursoPanel> Cursos { get; set; } = new();

    public List<AccesoPanel> Accesos { get; set; } = new();

    /// <summary>Porcentaje de expedientes cerrados sobre el total del año.</summary>
    public int Avance => TotalMatriculas == 0
        ? 0
        : (int)Math.Round(Cerradas * 100d / TotalMatriculas);

    public class CursoPanel
    {
        public string Curso { get; set; } = string.Empty;

        public int Total { get; set; }

        public int Cerradas { get; set; }

        public int Avance => Total == 0 ? 0 : (int)Math.Round(Cerradas * 100d / Total);
    }

    public class AccesoPanel
    {
        public DateTimeOffset Cuando { get; set; }

        public string Accion { get; set; } = string.Empty;

        public string? Area { get; set; }

        public string? Funcionario { get; set; }
    }
}
