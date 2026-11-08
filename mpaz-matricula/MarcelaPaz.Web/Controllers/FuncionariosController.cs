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
    private readonly ILogger<FuncionariosController> _log;

    public FuncionariosController(
        MatriculaDbContext db,
        UserManager<IdentityUser> usuarios,
        EscrituraAuditada escritura,
        VigilanteSesion vigilante,
        ILogger<FuncionariosController> log)
    {
        _db = db;
        _usuarios = usuarios;
        _escritura = escritura;
        _vigilante = vigilante;
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

    private async Task CargarRolesAsync()
    {
        ViewData["Roles"] = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Nombre)
            .ToListAsync();
    }
}
