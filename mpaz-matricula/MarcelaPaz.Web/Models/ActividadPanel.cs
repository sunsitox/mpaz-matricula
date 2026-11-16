namespace MarcelaPaz.Web.Models;

/// <summary>
/// Página de actividad del portal: los movimientos más recientes del registro
/// de accesos, con quién los hizo y sobre qué área.
/// </summary>
public class ActividadPanel
{
    /// <summary>Movimientos a mostrar, del más reciente al más antiguo.</summary>
    public List<PanelInicio.AccesoPanel> Accesos { get; set; } = new();

    public int Limite { get; set; } = 100;

    public string? Error { get; set; }
}
