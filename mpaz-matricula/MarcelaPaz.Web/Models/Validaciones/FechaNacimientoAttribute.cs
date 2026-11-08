using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace MarcelaPaz.Web.Models.Validaciones;

/// <summary>
/// Exige una fecha de nacimiento real, escrita como yyyy-MM-dd o dd-MM-yyyy,
/// anterior a hoy y con una edad razonable para ingresar a la escuela.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class FechaNacimientoAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        var texto = value?.ToString();

        if (string.IsNullOrWhiteSpace(texto))
        {
            return true;
        }

        if (!DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            && !DateTime.TryParse(texto, CultureInfo.CurrentCulture, DateTimeStyles.None, out fecha))
        {
            return false;
        }

        var hoy = DateTime.Today;

        return fecha < hoy && fecha > hoy.AddYears(-30);
    }

    public override string FormatErrorMessage(string name) =>
        "La fecha de nacimiento no es válida: debe ser anterior a hoy y corresponder a un estudiante.";
}
