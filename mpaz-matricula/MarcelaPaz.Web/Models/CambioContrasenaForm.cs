using System.ComponentModel.DataAnnotations;

namespace MarcelaPaz.Web.Models;

/// <summary>
/// Cambio de la contraseña inicial compartida. Se exige en el primer ingreso
/// mientras la cuenta siga usando el valor de <c>Seguridad:ContrasenaInicial</c>.
/// </summary>
public class CambioContrasenaForm
{
    [Required(ErrorMessage = "Ingrese su contraseña actual.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string Actual { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese la contraseña nueva.")]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "La contraseña debe tener al menos 10 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña nueva")]
    public string Nueva { get; set; } = string.Empty;

    [Required(ErrorMessage = "Repita la contraseña nueva.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Nueva), ErrorMessage = "Las contraseñas nuevas no coinciden.")]
    [Display(Name = "Repetir contraseña nueva")]
    public string Confirmar { get; set; } = string.Empty;
}
