using MarcelaPaz.Web.Servicios;

namespace MarcelaPaz.Web.Models;

/// <summary>Una fila del historial de certificados emitidos.</summary>
public sealed class CertificadoResumen
{
    public string Folio { get; init; } = string.Empty;

    public string AlumnoNombre { get; init; } = string.Empty;

    public string? Curso { get; init; }

    public int Anio { get; init; }

    public DateTimeOffset EmitidoEn { get; init; }

    public string? EmitidoPor { get; init; }

    public bool Anulado { get; init; }

    /// <summary>Pasó el plazo de vigencia y todavía no se anuló.</summary>
    public bool Vencido => !Anulado
        && !EmisionCertificado.EstaVigente(EmitidoEn, DateTimeOffset.UtcNow);
}