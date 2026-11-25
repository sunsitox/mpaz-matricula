using System.ComponentModel.DataAnnotations;
using MarcelaPaz.Domain.Identificacion;

namespace MarcelaPaz.Web.Models.Validaciones;

/// <summary>
/// Valida el documento con que se identifica el estudiante según Bloque A:
/// RUN con módulo 11 para estudiantes chilenos, o IPE (identificador
/// provisorio escolar) para quienes no tienen RUN. El IPA identifica al
/// apoderado y no se acepta como documento del estudiante.
/// El campo vacío se acepta aquí: la obligatoriedad se declara con [Required].
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class IdentificadorAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is null || string.IsNullOrWhiteSpace(value.ToString()) || EsValido(value.ToString());

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var texto = value?.ToString();

        if (string.IsNullOrWhiteSpace(texto))
        {
            return ValidationResult.Success;
        }

        var tipo = validationContext.ObjectInstance is FichaForm formulario
            ? formulario.Tipo_Identificador
            : "RUN";

        return EsValido(texto, tipo)
            ? ValidationResult.Success
            : new ValidationResult(
                FormatErrorMessage(validationContext.DisplayName),
                new[] { validationContext.MemberName ?? validationContext.DisplayName });
    }

    /// <summary>Valida un RUN o un IPE. Se usa también al buscar por documento.</summary>
    public static bool EsValido(string? documento, string? tipo = "RUN") =>
        DocumentoIdentidad.EsDocumentoEstudiante(documento, tipo);

    public override string FormatErrorMessage(string name) =>
        "El documento no es válido: revise el RUN (módulo 11) o el número IPE.";
}
