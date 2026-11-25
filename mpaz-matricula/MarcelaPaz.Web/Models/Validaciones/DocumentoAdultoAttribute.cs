using System.ComponentModel.DataAnnotations;
using MarcelaPaz.Domain.Identificacion;

namespace MarcelaPaz.Web.Models.Validaciones;

/// <summary>
/// Valida el documento de una persona adulta (madre, padre, apoderado o
/// persona autorizada para el retiro): RUN chileno con módulo 11, o IPA
/// (identificador provisorio de apoderado) para adultos extranjeros sin RUN.
/// El IPE es identificador del estudiante y no se acepta para adultos.
/// Los campos vacíos se aceptan aquí: la obligatoriedad se declara con
/// [Required] o se revisa en <see cref="ValidadorFicha"/>.
/// No se inventan rangos numéricos: el IPA se acepta en el mismo formato
/// genérico provisorio que el IPE (3 a 20 caracteres alfanuméricos, guion o
/// punto, sin espacios), distinguiéndose por no empezar con "IPE".
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class DocumentoAdultoAttribute : ValidationAttribute
{
    public DocumentoAdultoAttribute()
        : base("Ingrese un RUN válido o un número IPA.")
    {
    }

    public override bool IsValid(object? value)
    {
        var texto = value?.ToString();
        return string.IsNullOrWhiteSpace(texto) || EsValido(texto);
    }

    /// <summary>
    /// RUN válido (módulo 11) o IPA provisorio. Rechaza el IPE de estudiante.
    /// </summary>
    public static bool EsValido(string? documento) =>
        DocumentoIdentidad.EsDocumentoAdulto(documento);

    public override string FormatErrorMessage(string name) =>
        "El documento no es válido: revise el RUN (módulo 11) o el número IPA del apoderado. El IPE es del estudiante y no corresponde aquí.";
}
