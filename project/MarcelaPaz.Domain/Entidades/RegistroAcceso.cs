using System.Net;

namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Fila de <c>registros_acceso</c> (MAT-13). Es solo-inserción en la base: la
/// escribe la aplicación para dejar constancia de lecturas, búsquedas,
/// descargas, reemplazos y accesos denegados.
/// </summary>
public class RegistroAcceso
{
    public long Id { get; set; }

    public int? FuncionarioId { get; set; }

    /// <summary>LEER, BUSCAR, DESCARGAR, EXPORTAR, REEMPLAZAR o DENEGADO.</summary>
    public string Accion { get; set; } = string.Empty;

    /// <summary>Área de permiso involucrada (GENERAL, SALUD, PIE, JUDICIAL, ...).</summary>
    public string? Area { get; set; }

    public string? Recurso { get; set; }

    public long? RecursoId { get; set; }

    public int? EstudianteId { get; set; }

    /// <summary>Detalle libre en formato JSON; se guarda como jsonb.</summary>
    public string? Detalle { get; set; }

    public IPAddress? Ip { get; set; }

    public DateTimeOffset OcurridoEn { get; set; } = DateTimeOffset.UtcNow;
}
