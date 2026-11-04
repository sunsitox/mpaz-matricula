using System.ComponentModel.DataAnnotations;

namespace MarcelaPaz.Web.Models;

/// <summary>Credenciales del funcionario. No se pide RUT ni datos del estudiante.</summary>
public class LoginForm
{
    [Required(ErrorMessage = "Ingrese su correo institucional.")]
    [EmailAddress(ErrorMessage = "Revise el formato del correo.")]
    [Display(Name = "Correo institucional")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese su contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Contrasena { get; set; } = string.Empty;
}
