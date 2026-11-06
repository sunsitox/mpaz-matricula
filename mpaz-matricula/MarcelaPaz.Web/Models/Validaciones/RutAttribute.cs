using System.ComponentModel.DataAnnotations;
using MarcelaPaz.Domain.Identificacion;

namespace MarcelaPaz.Web.Models.Validaciones;

/// <summary>
/// Valida un RUN chileno escrito con o sin puntos («11.111.111-K» o «11111111K»).
/// Los campos vacíos se aceptan: esta versión sólo revisa el formato, no exige
/// completar el campo.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class RutAttribute : ValidationAttribute
{
    public RutAttribute()
        : base("Ingrese un RUN válido, por ejemplo 11.111.111-1.")
    {
    }

    public override bool IsValid(object? value)
    {
        var texto = value?.ToString();

        return string.IsNullOrWhiteSpace(texto) || RutChileno.EsValido(texto);
    }

    public override string FormatErrorMessage(string name) =>
        "El RUN no es válido: revise el número y el dígito verificador.";
}
