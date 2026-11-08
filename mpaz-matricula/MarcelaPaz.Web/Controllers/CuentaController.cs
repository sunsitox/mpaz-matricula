using System.Data.Common;
using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Único acceso al sistema: funcionarios con correo institucional y contraseña.
/// El apoderado no interactúa con el portal (fuera de alcance inicial, sección 15).
/// </summary>
public class CuentaController : Controller
{
    private readonly SignInManager<IdentityUser> _sesion;
    private readonly UserManager<IdentityUser> _usuarios;
    private readonly CuentasFuncionario _cuentas;
    private readonly MatriculaDbContext _db;
    private readonly IAuditoriaAcceso _auditoria;
    private readonly IConfiguration _configuracion;
    private readonly ILogger<CuentaController> _log;

    public CuentaController(
        SignInManager<IdentityUser> sesion,
        UserManager<IdentityUser> usuarios,
        CuentasFuncionario cuentas,
        MatriculaDbContext db,
        IAuditoriaAcceso auditoria,
        IConfiguration configuracion,
        ILogger<CuentaController> log)
    {
        _sesion = sesion;
        _usuarios = usuarios;
        _cuentas = cuentas;
        _db = db;
        _auditoria = auditoria;
        _configuracion = configuracion;
        _log = log;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginForm());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginForm form, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var correo = form.Correo.Trim();

        Funcionario? funcionario;
        IdentityUser? usuario;

        try
        {
            funcionario = await _db.Funcionarios
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Correo.ToLower() == correo.ToLower());

            // El funcionario viene de la base y puede no tener cuenta todavía (o
            // traer un enlace de prueba): AsegurarAsync la crea o la vuelve a enlazar.
            usuario = funcionario is null || !funcionario.Activo
                ? null
                : (await _cuentas.AsegurarAsync(funcionario)).Usuario;
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            _log.LogError(ex, "No se pudo consultar la base de datos al iniciar sesión");
            ModelState.AddModelError(string.Empty, "No se pudo conectar con la base de datos. Avise a Administración TI.");
            return View(form);
        }

        if (funcionario is null || !funcionario.Activo || usuario is null)
        {
            await _auditoria.RegistrarAsync(
                "DENEGADO", "GENERAL", "Cuenta/Login",
                detalle: "{\"motivo\":\"credenciales o cuenta no habilitada\"}");
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return View(form);
        }

        var resultado = await _sesion.PasswordSignInAsync(usuario, form.Contrasena, false, lockoutOnFailure: true);

        if (resultado.IsLockedOut)
        {
            await _auditoria.RegistrarAsync(
                "DENEGADO", "GENERAL", "Cuenta/Login", funcionario.Id,
                "{\"motivo\":\"bloqueo temporal por intentos fallidos\"}");
            ModelState.AddModelError(string.Empty, "La cuenta quedó bloqueada por 15 minutos tras varios intentos fallidos.");
            return View(form);
        }

        if (!resultado.Succeeded)
        {
            await _auditoria.RegistrarAsync(
                "DENEGADO", "GENERAL", "Cuenta/Login", funcionario.Id,
                "{\"motivo\":\"contraseña incorrecta\"}");
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return View(form);
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salida()
    {
        await _sesion.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult SinPermiso() => View();

    /// <summary>
    /// Cambio obligatorio de la contraseña inicial compartida (S-09). El
    /// middleware de Program.cs envía aquí mientras la cuenta la siga usando.
    /// </summary>
    [HttpGet]
    [Authorize]
    public IActionResult CambiarContrasena() => View(new CambioContrasenaForm());

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarContrasena(CambioContrasenaForm form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var usuario = await _usuarios.GetUserAsync(User);

        if (usuario is null)
        {
            await _sesion.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        var inicial = _configuracion["Seguridad:ContrasenaInicial"];

        if (!string.IsNullOrWhiteSpace(inicial)
            && string.Equals(form.Nueva, inicial, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(form.Nueva),
                "La contraseña nueva no puede ser la contraseña inicial compartida.");
            return View(form);
        }

        var resultado = await _usuarios.ChangePasswordAsync(usuario, form.Actual, form.Nueva);

        if (!resultado.Succeeded)
        {
            foreach (var error in resultado.Errors)
            {
                ModelState.AddModelError(string.Empty, Traducir(error));
            }

            return View(form);
        }

        // La marca de cambio obligatorio puede venir de la contraseña inicial o
        // de un restablecimiento hecho por Administración; se retira al cambiarla.
        var marcas = await _usuarios.GetClaimsAsync(usuario);
        var marca = marcas.FirstOrDefault(c => c.Type == "debe_cambiar_contrasena");

        if (marca is not null)
        {
            await _usuarios.RemoveClaimAsync(usuario, marca);
        }

        await _auditoria.RegistrarAsync(
            "REEMPLAZAR",
            "GENERAL",
            "cuenta",
            detalle: "{\"evento\":\"cambio_contrasena_inicial\"}");

        // Se cierra la sesión para que el claim de cambio obligatorio no quede
        // en la cookie actual.
        await _sesion.SignOutAsync();
        TempData["Aviso"] = "Contraseña actualizada. Ingrese de nuevo con su contraseña nueva.";

        return RedirectToAction(nameof(Login));
    }

    private static string Traducir(IdentityError error) => error.Code switch
    {
        "PasswordMismatch" => "La contraseña actual no es correcta.",
        "PasswordTooShort" => "La contraseña debe tener al menos 10 caracteres.",
        "PasswordRequiresDigit" => "La contraseña debe incluir al menos un número.",
        "PasswordRequiresUpper" => "La contraseña debe incluir al menos una mayúscula.",
        "PasswordRequiresLower" => "La contraseña debe incluir al menos una minúscula.",
        "PasswordRequiresNonAlphanumeric" => "La contraseña debe incluir un símbolo.",
        _ => error.Description,
    };
}
