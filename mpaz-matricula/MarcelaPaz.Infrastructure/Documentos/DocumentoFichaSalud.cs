namespace MarcelaPaz.Infrastructure.Documentos;

/// <summary>
/// Contenido de la ficha de salud del funcionario que se imprime o descarga
/// desde el portal. Estructura plana: la capa Web la arma desde la ficha
/// registrada y este generador no consulta la base de datos. Sólo incluye lo
/// que el formulario del colegio pide; no hay archivos adjuntos.
/// </summary>
public sealed class DocumentoFichaSalud
{
    public required string Sostenedor { get; init; }

    public required string Establecimiento { get; init; }

    public required string DireccionEstablecimiento { get; init; }

    public byte[]? Logo { get; init; }

    public required string FuncionarioNombre { get; init; }

    /// <summary>RUN formateado o vacío si el funcionario se identifica por pasaporte.</summary>
    public string? Rut { get; init; }

    public string? Pasaporte { get; init; }

    public string? TelefonoPersonal { get; init; }

    public string? DireccionPersonal { get; init; }

    public string? Contacto1Nombre { get; init; }

    public string? Contacto1Telefono { get; init; }

    public string? Contacto2Nombre { get; init; }

    public string? Contacto2Telefono { get; init; }

    public string? SistemaSalud { get; init; }

    public string? CentroMedicoPreferente { get; init; }

    public string? CentroUbicacion { get; init; }

    public string? CentroTelefono { get; init; }

    public string? AmbulanciaConvenio { get; init; }

    public string? AmbulanciaTelefono { get; init; }

    public string? SeguroComplementario { get; init; }

    public string? Otros { get; init; }

    public string? AlergiasMedicamentos { get; init; }

    public string? EnfermedadesCronicas { get; init; }

    public string? MedicamentosFrecuentes { get; init; }

    /// <summary>«Si» o «No»; null mientras no se responda.</summary>
    public string? AceptaTransfusion { get; init; }

    public string? MotivoTransfusion { get; init; }

    /// <summary>Función que registró o actualizó la ficha.</summary>
    public required string RegistradoPor { get; init; }

    public required DateTimeOffset RegistradoEn { get; init; }
}
