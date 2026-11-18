using MarcelaPaz.Web.Servicios;

namespace MarcelaPaz.Web.Models;

/// <summary>
/// MAT-17: listado de órdenes médicas y autorizaciones vencidas o por vencer en
/// los próximos 30 días. Cada bloque se consulta sólo si la sesión tiene el
/// área correspondiente (AUTORIZACIONES o MEDICACION), así que la página nunca
/// mezcla datos que el rol no puede leer.
/// </summary>
public class AlertasPagina
{
    public List<AlertaVencimiento> Alertas { get; set; } = new();

    public bool PuedeAutorizaciones { get; set; }

    public bool PuedeMedicacion { get; set; }

    /// <summary>Año escolar de la matrícula usada; null si no hay año configurado.</summary>
    public int? Anio { get; set; }

    /// <summary>Años disponibles para el filtro.</summary>
    public IReadOnlyList<AnioEscolarOpcion> Anios { get; set; } = [];

    /// <summary>Última revisión escrita por el proceso programado (MAT-17).</summary>
    public RevisionAlertas? UltimaRevision { get; set; }

    public string? Error { get; set; }

    /// <summary>Alertas cuyo plazo ya terminó.</summary>
    public int Vencidas => Alertas.Count(a => a.Vencida);

    /// <summary>Alertas que vencen en los próximos 30 días.</summary>
    public int PorVencer => Alertas.Count(a => !a.Vencida);
}

/// <summary>Resumen de la última revisión programada de alertas.</summary>
public sealed class RevisionAlertas
{
    public DateTimeOffset RevisadaEn { get; set; }

    public int? Anio { get; set; }

    public int VencidasAutorizaciones { get; set; }

    public int VencidasPrescripciones { get; set; }

    public int PorVencerAutorizaciones { get; set; }

    public int PorVencerPrescripciones { get; set; }

    public string Resumen { get; set; } = string.Empty;
}

/// <summary>
/// Una fila de <c>v_alertas_vencimiento</c> con los datos del alumno y el curso
/// de su última matrícula no anulada.
/// </summary>
public class AlertaVencimiento
{
    public int RegistroId { get; set; }

    public int EstudianteId { get; set; }

    /// <summary>PRESCRIPCION (orden médica) o AUTORIZACION.</summary>
    public string Origen { get; set; } = string.Empty;

    public string Identificador { get; set; } = string.Empty;

    public string Estudiante { get; set; } = string.Empty;

    public string Curso { get; set; } = string.Empty;

    /// <summary>Año de la última matrícula, para abrir la ficha en ese año.</summary>
    public int? Anio { get; set; }

    /// <summary>Tipo de autorización o medicamento de la orden médica.</summary>
    public string Detalle { get; set; } = string.Empty;

    public DateOnly VenceEl { get; set; }

    public bool Vencida { get; set; }

    public string Tipo => Origen == "PRESCRIPCION" ? "Orden médica" : "Autorización";

    /// <summary>Días hasta el vencimiento; negativo si ya venció.</summary>
    public int Dias => VenceEl.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;

    public string Plazo => Dias switch
    {
        < 0 => $"Venció hace {-Dias} día{(-Dias == 1 ? string.Empty : "s")}",
        0 => "Vence hoy",
        _ => $"Vence en {Dias} día{(Dias == 1 ? string.Empty : "s")}",
    };
}
