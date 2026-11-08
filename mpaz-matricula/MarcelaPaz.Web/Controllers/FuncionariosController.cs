using MarcelaPaz.Domain.Entidades;
using System.Data.Common;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Administración de cuentas: la sección 10 asigna "cuentas y configuración" a
/// Administración TI. No hay registro abierto y nunca se borra un funcionario:
/// se desactiva la cuenta o se cierra el rol con fecha.
/// </summary>
[Authorize(Policy = PermisoPolicyProvider.Prefijo + "ADMIN:ADMINISTRAR")]
[Route("admin/funcionarios")]
public class FuncionariosController : Controller
{
    private readonly MatriculaDbContext _db;
    private readonly UserManager<IdentityUser> _usuarios;
    private readonly EscrituraAuditada _escritura;
    private readonly VigilanteSesion _vigilante;
    private readonly IAuditoriaAcceso _auditoria;
    private readonly ILogger<FuncionariosController> _log;

    public FuncionariosController(
        MatriculaDbContext db,
        UserManager<IdentityUser> usuarios,
        EscrituraAuditada escritura,
        VigilanteSesion vigilante,
        IAuditoriaAcceso auditoria,
        ILogger<FuncionariosController> log)
    {
        _db = db;
        _usuarios = usuarios;
        _escritura = escritura;
        _vigilante = vigilante;
        _auditoria = auditoria;
        _log = log;
    }

    private int FuncionarioActualId =>
        int.TryParse(User.FindFirstValue("funcionario_id"), out var id) ? id : 0;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        try
        {
            return await ListarAsync();
        }
        catch (DbException)
        {
            TempData["Aviso"] = "No se pudo conectar con la base de datos. Revise ConnectionStrings:Default.";
            return View(new List<FuncionarioListado>());
        }
    }

    private async Task<IActionResult> ListarAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var funcionarios = await _db.Funcionarios
            .AsNoTracking()
            .OrderBy(f => f.Nombre)
            .ToListAsync();

        var roles = await (
                from fr in _db.FuncionarioRoles.AsNoTracking()
                join r in _db.Roles on fr.RolId equals r.Id
                where fr.Hasta == null || fr.Hasta >= hoy
                select new { fr.FuncionarioId, RolId = r.Id, r.Codigo, r.Nombre })
            .ToListAsync();

        var listado = funcionarios
            .Select(f => new FuncionarioListado
            {
                Id = f.Id,
                Nombre = f.Nombre,
                Correo = f.Correo,
                Activo = f.Activo,
                Roles = roles
                    .Where(r => r.FuncionarioId == f.Id)
                    .Select(r => new FuncionarioListado.RolAsignado
                    {
                        RolId = r.RolId,
                        Codigo = r.Codigo,
                        Nombre = r.Nombre,
                    })
                    .ToList(),
            })
            .ToList();

        return View(listado);
    }

    [HttpGet("crear")]
    public async Task<IActionResult> Crear()
    {
        await CargarRolesAsync();
        return View(new FuncionarioForm());
    }

    [HttpPost("crear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(FuncionarioForm form)
    {
        if (!ModelState.IsValid)
        {
            await CargarRolesAsync();
            return View(form);
        }

        var correo = form.Correo.Trim().ToLowerInvariant();

        if (await _db.Funcionarios.AnyAsync(f => f.Correo.ToLower() == correo))
        {
            ModelState.AddModelError(nameof(form.Correo), "Ese correo ya está registrado.");
            await CargarRolesAsync();
            return View(form);
        }

        var usuario = new IdentityUser
        {
            UserName = correo,
            Email = correo,
            EmailConfirmed = true,
        };

        var creado = await _usuarios.CreateAsync(usuario, form.Contrasena);

        if (!creado.Succeeded)
        {
            foreach (var error in creado.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await CargarRolesAsync();
            return View(form);
        }

        // La persona debe cambiar la contraseña entregada por Administración en
        // su primer ingreso, aunque el valor no sea la contraseña inicial.
        await _usuarios.AddClaimAsync(usuario, new Claim("debe_cambiar_contrasena", "1"));

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var funcionario = new Funcionario
        {
            Nombre = form.Nombre.Trim(),
            Correo = correo,
            SsoSujeto = usuario.Id,
            Activo = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var rolesElegidos = await _db.Roles
            .Where(r => form.Roles.Contains(r.Codigo))
            .ToListAsync();

        foreach (var rol in rolesElegidos)
        {
            funcionario.Roles.Add(new FuncionarioRol { RolId = rol.Id, Desde = hoy });
        }

        try
        {
            await _escritura.EjecutarAsync(FuncionarioActualId, _ =>
            {
                _db.Funcionarios.Add(funcionario);
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo registrar el funcionario {Correo}", correo);

            // Compensación: no dejar credenciales sin funcionario asociado.
            await _usuarios.DeleteAsync(usuario);

            ModelState.AddModelError(string.Empty, "No se pudo guardar el funcionario. Intente nuevamente.");
            await CargarRolesAsync();
            return View(form);
        }

        TempData["Aviso"] = $"Funcionario {funcionario.Nombre} creado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("estado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, bool activo)
    {
        var funcionario = await _db.Funcionarios.FirstOrDefaultAsync(f => f.Id == id);

        if (funcionario is null)
        {
            return NotFound();
        }

        if (funcionario.Id == FuncionarioActualId && !activo)
        {
            TempData["Aviso"] = "No puede desactivar su propia cuenta.";
            return RedirectToAction(nameof(Index));
        }

        await _escritura.EjecutarAsync(FuncionarioActualId, _ =>
        {
            funcionario.Activo = activo;
            return Task.CompletedTask;
        });

        if (!activo)
        {
            // S-03: desactivar debe cortar el acceso ya otorgado, no esperar a
            // que caduque la cookie.
            await RevocarSesionesAsync(funcionario);
        }

        TempData["Aviso"] = activo
            ? $"Cuenta de {funcionario.Nombre} activada."
            : $"Cuenta de {funcionario.Nombre} desactivada.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cerrar-rol")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarRol(int funcionarioId, int rolId)
    {
        var relacion = await _db.FuncionarioRoles
            .FirstOrDefaultAsync(fr => fr.FuncionarioId == funcionarioId && fr.RolId == rolId && fr.Hasta == null);

        if (relacion is null)
        {
            TempData["Aviso"] = "Ese rol ya estaba cerrado.";
            return RedirectToAction(nameof(Index));
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        await _escritura.EjecutarAsync(FuncionarioActualId, _ =>
        {
            relacion.Hasta = hoy;
            return Task.CompletedTask;
        });

        // S-03: los permisos emitidos al iniciar sesión ya no representan al
        // funcionario; se renueva el sello para que la cookie deje de servir.
        var afectado = await _db.Funcionarios.FirstOrDefaultAsync(f => f.Id == funcionarioId);

        if (afectado is not null)
        {
            await RevocarSesionesAsync(afectado);
        }

        TempData["Aviso"] = "Rol cerrado con la fecha de hoy. El historial se conserva.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Renueva el sello de seguridad de la cuenta y olvida la comprobación
    /// memorizada, de modo que la sesión abierta deje de conceder permisos.
    /// </summary>
    private async Task RevocarSesionesAsync(Funcionario funcionario)
    {
        IdentityUser? usuario = null;

        if (!string.IsNullOrWhiteSpace(funcionario.SsoSujeto))
        {
            usuario = await _usuarios.FindByIdAsync(funcionario.SsoSujeto);
        }

        usuario ??= await _usuarios.FindByEmailAsync(funcionario.Correo.Trim().ToLowerInvariant());

        if (usuario is null)
        {
            _log.LogWarning(
                "No hay cuenta de Identity que revocar para el funcionario {Correo}.",
                funcionario.Correo);
            return;
        }

        var resultado = await _usuarios.UpdateSecurityStampAsync(usuario);

        if (!resultado.Succeeded)
        {
            _log.LogWarning(
                "No se pudo invalidar la sesión de {Correo}: {Errores}",
                funcionario.Correo,
                string.Join("; ", resultado.Errors.Select(error => error.Description)));
        }

        _vigilante.Invalidar(funcionario.Id);
    }

    /// <summary>
    /// Restablece la contraseña con un valor temporal que se muestra una sola vez
    /// y obliga a cambiarlo en el próximo ingreso. También retira el bloqueo por
    /// intentos fallidos y cierra las sesiones abiertas del afectado.
    /// </summary>
    [HttpPost("restablecer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestablecerContrasena(int id)
    {
        var funcionario = await _db.Funcionarios.FirstOrDefaultAsync(f => f.Id == id);

        if (funcionario is null)
        {
            return NotFound();
        }

        var usuario = await BuscarUsuarioAsync(funcionario);

        if (usuario is null)
        {
            TempData["Aviso"] = $"El funcionario {funcionario.Nombre} no tiene cuenta de acceso.";
            return RedirectToAction(nameof(Index));
        }

        var clave = GenerarClaveTemporal();
        var token = await _usuarios.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await _usuarios.ResetPasswordAsync(usuario, token, clave);

        if (!resultado.Succeeded)
        {
            _log.LogWarning(
                "No se pudo restablecer la contraseña de {Correo}: {Errores}",
                funcionario.Correo,
                string.Join("; ", resultado.Errors.Select(error => error.Description)));

            TempData["Aviso"] = "No se pudo restablecer la contraseña. Intente nuevamente.";
            return RedirectToAction(nameof(Index));
        }

        var marcas = await _usuarios.GetClaimsAsync(usuario);

        if (!marcas.Any(c => c.Type == "debe_cambiar_contrasena" && c.Value == "1"))
        {
            await _usuarios.AddClaimAsync(usuario, new Claim("debe_cambiar_contrasena", "1"));
        }

        await _usuarios.SetLockoutEndDateAsync(usuario, null);
        await _usuarios.ResetAccessFailedCountAsync(usuario);
        await RevocarSesionesAsync(funcionario);

        await _auditoria.RegistrarAsync(
            "REEMPLAZAR",
            "GENERAL",
            "cuenta",
            funcionarioId: FuncionarioActualId,
            detalle: $"{{\"evento\":\"restablecer_contrasena\",\"funcionario\":{funcionario.Id}}}");

        TempData["ClaveTemporal"] = clave;
        TempData["ClaveTemporalCorreo"] = funcionario.Correo;
        TempData["Aviso"] = $"Contraseña restablecida para {funcionario.Nombre}. Debe cambiarla en el próximo ingreso.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Retira el bloqueo por intentos fallidos sin cambiar la contraseña.</summary>
    [HttpPost("desbloquear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desbloquear(int id)
    {
        var funcionario = await _db.Funcionarios.FirstOrDefaultAsync(f => f.Id == id);

        if (funcionario is null)
        {
            return NotFound();
        }

        var usuario = await BuscarUsuarioAsync(funcionario);

        if (usuario is null)
        {
            TempData["Aviso"] = $"El funcionario {funcionario.Nombre} no tiene cuenta de acceso.";
            return RedirectToAction(nameof(Index));
        }

        await _usuarios.SetLockoutEndDateAsync(usuario, null);
        await _usuarios.ResetAccessFailedCountAsync(usuario);

        await _auditoria.RegistrarAsync(
            "REEMPLAZAR",
            "GENERAL",
            "cuenta",
            funcionarioId: FuncionarioActualId,
            detalle: $"{{\"evento\":\"desbloquear_cuenta\",\"funcionario\":{funcionario.Id}}}");

        TempData["Aviso"] = $"Cuenta de {funcionario.Nombre} desbloqueada.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<IdentityUser?> BuscarUsuarioAsync(Funcionario funcionario)
    {
        var correo = funcionario.Correo.Trim().ToLowerInvariant();

        IdentityUser? usuario = null;

        if (!string.IsNullOrWhiteSpace(funcionario.SsoSujeto))
        {
            usuario = await _usuarios.FindByIdAsync(funcionario.SsoSujeto);
        }

        return usuario
               ?? await _usuarios.FindByEmailAsync(correo)
               ?? await _usuarios.FindByNameAsync(correo);
    }

    /// <summary>Clave temporal legible que cumple la política (mayúscula, minúscula y dígito).</summary>
    private static string GenerarClaveTemporal()
    {
        const string mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string minusculas = "abcdefghijkmnopqrstuvwxyz";
        const string digitos = "23456789";

        var caracteres = new List<char>(13);

        for (var i = 0; i < 4; i++)
        {
            caracteres.Add(mayusculas[RandomNumberGenerator.GetInt32(mayusculas.Length)]);
        }

        for (var i = 0; i < 5; i++)
        {
            caracteres.Add(minusculas[RandomNumberGenerator.GetInt32(minusculas.Length)]);
        }

        for (var i = 0; i < 4; i++)
        {
            caracteres.Add(digitos[RandomNumberGenerator.GetInt32(digitos.Length)]);
        }

        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }

        return new string(caracteres.ToArray());
    }

    private async Task CargarRolesAsync()
    {
        ViewData["Roles"] = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Nombre)
            .ToListAsync();
    }
}
