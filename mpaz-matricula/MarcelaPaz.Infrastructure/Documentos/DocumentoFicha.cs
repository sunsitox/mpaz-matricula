namespace MarcelaPaz.Infrastructure.Documentos;

/// <summary>
/// Contenido de la ficha que se imprime al terminar el wizard. Es una estructura
/// plana: la capa Web la arma desde el borrador y este generador no conoce ni el
/// formulario ni la base de datos.
/// </summary>
public sealed class DocumentoFicha
{
    public required string Folio { get; init; }

    public required int Anio { get; init; }

    public required string TipoTramite { get; init; }

    public required string Version { get; init; }

    public required DateTime GeneradoEn { get; init; }

    public string? Detalle { get; init; }

    /// <summary>Escudo institucional con lema (PNG); se imprime arriba a la izquierda.</summary>
    public byte[]? Logo { get; init; }

    public required IReadOnlyList<SeccionFicha> Secciones { get; init; }

    /// <summary>Autorizaciones que el apoderado marca y firma junto con la ficha.</summary>
    public IReadOnlyList<AutorizacionFicha> Autorizaciones { get; init; } = Array.Empty<AutorizacionFicha>();
}

/// <summary>Consentimiento impreso en la ficha (uso de imagen, convivencia escolar).</summary>
public sealed class AutorizacionFicha
{
    public required string Titulo { get; init; }

    public required string Texto { get; init; }

    /// <summary>true = Sí, false = No, null = el apoderado aún no responde.</summary>
    public bool? Respuesta { get; init; }
}

/// <summary>Un bloque de la ficha (estudiante, madre, apoderados, etc.).</summary>
public sealed class SeccionFicha
{
    public required string Titulo { get; init; }

    public required IReadOnlyList<CampoFicha> Campos { get; init; }
}

/// <summary>Etiqueta y valor tal como se capturaron en el formulario.</summary>
public sealed class CampoFicha
{
    public required string Etiqueta { get; init; }

    public required string Valor { get; init; }
}
