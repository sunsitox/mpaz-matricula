namespace MarcelaPaz.Domain.Entidades;

/// <summary>
/// Ficha de salud del funcionario: identificación, contactos de emergencia,
/// previsión y antecedentes médicos. Es información sensible del personal:
/// sólo UTP y Dirección la consultan y registran (área SALUD, tabla
/// <c>ficha_salud_funcionario</c>).
/// Campos pedidos por el colegio (23-11-2026): nombre, RUN o pasaporte,
/// teléfono y dirección personal; dos contactos de emergencia con teléfono;
/// sistema de salud, centro preferente, ambulancia en convenio, seguro
/// complementario, alergias, enfermedades crónicas, medicamentos frecuentes
/// y aceptación de transfusión de sangre.
/// </summary>
public class FichaSaludFuncionario
{
    public int Id { get; set; }

    public int FuncionarioId { get; set; }

    /// <summary>Nombre completo del funcionario (copia vigente al registrar).</summary>
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>RUN chileno. Uno de los dos documentos es obligatorio.</summary>
    public string? Rut { get; set; }

    /// <summary>Pasaporte u otro documento extranjero.</summary>
    public string? Pasaporte { get; set; }

    public string? TelefonoPersonal { get; set; }

    public string? DireccionPersonal { get; set; }

    public string? Contacto1Nombre { get; set; }

    public string? Contacto1Telefono { get; set; }

    public string? Contacto2Nombre { get; set; }

    public string? Contacto2Telefono { get; set; }

    /// <summary>Sistema de salud: Fonasa, Isapre u otro.</summary>
    public string? SistemaSalud { get; set; }

    public string? CentroMedicoPreferente { get; set; }

    public string? CentroUbicacion { get; set; }

    public string? CentroTelefono { get; set; }

    public string? AmbulanciaConvenio { get; set; }

    public string? AmbulanciaTelefono { get; set; }

    public string? SeguroComplementario { get; set; }

    public string? Otros { get; set; }

    public string? AlergiasMedicamentos { get; set; }

    public string? EnfermedadesCronicas { get; set; }

    public string? MedicamentosFrecuentes { get; set; }

    /// <summary>¿Acepta transfusión de sangre? ("Si"/"No").</summary>
    public string? AceptaTransfusion { get; set; }

    /// <summary>Motivo religioso o de otra índole (obligatorio si no acepta).</summary>
    public string? MotivoTransfusion { get; set; }

    public int? RegistradoPor { get; set; }

    public DateTimeOffset RegistradoEn { get; set; } = DateTimeOffset.UtcNow;
}
