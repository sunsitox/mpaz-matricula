using System.ComponentModel.DataAnnotations;

namespace MarcelaPaz.Web.Models;

/// <summary>
/// Datos que cambian al editar un funcionario: nombre y roles vigentes.
/// El correo es la identidad de la cuenta y no se edita desde esta pantalla;
/// la contraseña se administra con «Restablecer contraseña».
/// </summary>
public class FuncionarioEditarForm
{
    [Required(ErrorMessage = "Ingrese el nombre completo.")]
    [StringLength(150)]
    [Display(Name = "Nombre completo")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Códigos de rol elegidos (SECRETARIA, TENS, ADMIN_TI, ...).</summary>
    public List<string> Roles { get; set; } = new();
}
