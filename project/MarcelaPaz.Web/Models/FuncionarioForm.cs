using System.ComponentModel.DataAnnotations;

namespace MarcelaPaz.Web.Models;

/// <summary>Datos que Administración TI entrega al crear una cuenta.</summary>
public class FuncionarioForm
{
    [Required(ErrorMessage = "Ingrese el nombre completo.")]
    [StringLength(150)]
    [Display(Name = "Nombre completo")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese el correo institucional.")]
    [EmailAddress(ErrorMessage = "Revise el formato del correo.")]
    [StringLength(255)]
    [Display(Name = "Correo institucional")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Defina una contraseña inicial.")]
    [StringLength(100, MinimumLength = 10, ErrorMessage = "Use al menos 10 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña inicial")]
    public string Contrasena { get; set; } = string.Empty;

    /// <summary>Códigos de rol elegidos (SECRETARIA, TENS, ADMIN_TI, ...).</summary>
    public List<string> Roles { get; set; } = new();
}
