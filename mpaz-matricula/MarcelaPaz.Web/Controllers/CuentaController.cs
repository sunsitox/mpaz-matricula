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
    private readonly CuentasFuncionario _cuentas;
    private readonly MatriculaDbContext _db;
    private readonly IAuditoriaAcceso _auditoria;
    private readonly ILogger<CuentaController> _log;

    public CuentaController(
        SignInManager<IdentityUser> sesion,
        CuentasFuncionario cuentas,
        MatriculaDbContext db,
        IAuditoriaAcceso auditoria,
        ILogger<CuentaController> log)
    {
        _sesion = sesion;
        _cuentas = cuentas;
        _db = db;
        _auditoria = auditoria;
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
}
