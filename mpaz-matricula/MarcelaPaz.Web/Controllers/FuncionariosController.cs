using MarcelaPaz.Domain.Entidades;
using System.Data.Common;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
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
    private readonly RegistroWebService _registroWebService;
    private readonly ILogger<FuncionariosController> _log;

    public FuncionariosController(
        MatriculaDbContext db,
        UserManager<IdentityUser> usuarios,
        EscrituraAuditada escritura,
        VigilanteSesion vigilante,
        IAuditoriaAcceso auditoria,
        RegistroWebService registroWebService,
        ILogger<FuncionariosController> log)
    {
        _db = db;
        _usuarios = usuarios;
        _escritura = escritura;
        _vigilante = vigilante;
        _auditoria = auditoria;
        _registroWebService = registroWebService;
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
                select new { fr.FuncionarioId, RolId = r.Id, r.Codigo, r.Nombre, fr.Desde })
            .ToListAsync();

        // El bloqueo por intentos vive en Identity: sólo se ofrece
        // «Desbloquear» a quien realmente está bloqueado. En una sola
        // consulta a las cuentas, sin una búsqueda por funcionario.
        var ahora = DateTimeOffset.UtcNow;
        var cuentasBloqueadas = await _usuarios.Users
            .Where(u => u.LockoutEnd != null && u.LockoutEnd > ahora)
            .Select(u => new { u.Id, u.Email })
            .ToListAsync();
        var idsBloqueados = cuentasBloqueadas.Select(u => u.Id).ToHashSet();
        var correosBloqueados = cuentasBloqueadas
            .Where(u => u.Email is not null)
            .Select(u => u.Email!.Trim().ToLowerInvariant())
            .ToHashSet();

        var listado = funcionarios
            .Select(f => new FuncionarioListado
            {
                Id = f.Id,
                Nombre = f.Nombre,
                Correo = f.Correo,
                Activo = f.Activo,
                Bloqueado = idsBloqueados.Contains(f.SsoSujeto ?? "")
                            || correosBloqueados.Contains(f.Correo.Trim().ToLowerInvariant()),
                Roles = roles
                    .Where(r => r.FuncionarioId == f.Id)
                    // Orden de agregación: el rol asignado antes se lista primero.
                    .OrderBy(r => r.Desde)
                    .ThenBy(r => r.Codigo)
                    .Select(r => new FuncionarioListado.RolAsignado
                    {
                        RolId = r.RolId,
                        Codigo = r.Codigo,
                        Nombre = r.Nombre,
                        Desde = r.Desde,
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

        // Sincronización con el web service: el funcionario ya quedó en Neon,
        // así que si el web service está caído el portal sigue operando con
        // el respaldo.
        var sincronizado = await _registroWebService.RegistrarAsync(
            funcionario.Nombre, correo, form.Contrasena);

        await _auditoria.RegistrarAsync(
            "REEMPLAZAR",
            "GENERAL",
            "cuenta",
            funcionarioId: FuncionarioActualId,
            detalle: $"{{\"evento\":\"crear_funcionario\",\"funcionario\":{funcionario.Id},\"web_service\":{(sincronizado ? "registrado" : "respaldo_neon")}}}");

        TempData["Aviso"] = sincronizado
            ? $"Funcionario {funcionario.Nombre} creado y registrado en el web service."
            : $"Funcionario {funcionario.Nombre} creado en el portal (web service no disponible; datos en Neon).";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("editar")]
    public async Task<IActionResult> Editar(int id)
    {
        var funcionario = await _db.Funcionarios.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (funcionario is null)
        {
            return NotFound();
        }

        ViewData["CorreoFuncionario"] = funcionario.Correo;
        ViewData["FuncionarioId"] = funcionario.Id;
        await CargarRolesAsync();

        return View(new FuncionarioEditarForm
        {
            Nombre = funcionario.Nombre,
            Roles = await RolesVigentesAsync(funcionario.Id),
        });
    }

    [HttpPost("editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, FuncionarioEditarForm form)
    {
        var funcionario = await _db.Funcionarios.Include(f => f.Roles)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (funcionario is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ViewData["CorreoFuncionario"] = funcionario.Correo;
            ViewData["FuncionarioId"] = funcionario.Id;
            await CargarRolesAsync();
            return View(form);
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var todosLosRoles = await _db.Roles.ToListAsync();
        var codigos = todosLosRoles.ToDictionary(r => r.Id, r => r.Codigo);
        var elegidos = todosLosRoles
            .Where(r => form.Roles.Contains(r.Codigo))
            .ToList();
        var idsElegidos = elegidos.Select(r => r.Id).ToHashSet();

        // Como en «desactivar»: nadie se quita a sí mismo el rol ADMIN_TI.
        // «Vigente» con la misma regla del resto del sistema: Hasta es
        // inclusiva (nulo o no anterior a hoy).
        var vigentes = funcionario.Roles.Where(r => r.Hasta is null || r.Hasta >= hoy).ToList();
        var tieneAdminTi = vigentes.Any(r => codigos.TryGetValue(r.RolId, out var c) && c == "ADMIN_TI");
        var mantieneAdminTi = elegidos.Any(r => r.Codigo == "ADMIN_TI");
        if (funcionario.Id == FuncionarioActualId && tieneAdminTi && !mantieneAdminTi)
        {
            TempData["Aviso"] = "No puede quitarse a sí mismo el rol Administración TI.";
            return RedirectToAction(nameof(Index));
        }

        var cambios = new List<string>();

        var nombre = form.Nombre.Trim();
        if (!string.Equals(nombre, funcionario.Nombre, StringComparison.Ordinal))
        {
            funcionario.Nombre = nombre;
            cambios.Add("nombre");
        }

        // funcionario_rol tiene clave (funcionario_id, rol_id): hay una sola
        // fila por rol. Cerrar con Hasta inclusiva deja el último día válido
        // en ayer; si el rol se asignó hoy, la restricción hasta >= desde no
        // acepta ayer y la fila se elimina (el trigger de auditoría la registra).
        foreach (var relacion in vigentes.Where(r => !idsElegidos.Contains(r.RolId)))
        {
            if (relacion.Desde >= hoy)
            {
                funcionario.Roles.Remove(relacion);
            }
            else
            {
                relacion.Hasta = hoy.AddDays(-1);
            }
            cambios.Add($"cerrar_rol:{relacion.RolId}");
        }

        // Reabrir un rol cerrado es devolver su fila a vigente (Hasta = nulo);
        // insertar otra violaría la clave primaria.
        foreach (var rol in elegidos.Where(r => !vigentes.Any(v => v.RolId == r.Id)))
        {
            var previa = funcionario.Roles.FirstOrDefault(r => r.RolId == rol.Id);
            if (previa is not null)
            {
                previa.Hasta = null;
            }
            else
            {
                funcionario.Roles.Add(new FuncionarioRol { RolId = rol.Id, Desde = hoy });
            }
            cambios.Add($"abrir_rol:{rol.Id}");
        }

        try
        {
            await _escritura.EjecutarAsync(FuncionarioActualId, _ => Task.CompletedTask);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo editar el funcionario {Id}", funcionario.Id);
            ModelState.AddModelError(string.Empty, "No se pudieron guardar los cambios. Intente nuevamente.");
            ViewData["CorreoFuncionario"] = funcionario.Correo;
            ViewData["FuncionarioId"] = funcionario.Id;
            await CargarRolesAsync();
            return View(form);
        }

        // Sólo los roles obligan a cerrar sesiones: el nombre no cambia los
        // permisos, así que corregir un nombre no expulsa a la persona.
        if (cambios.Any(c => c.StartsWith("abrir_rol:", StringComparison.Ordinal)
                            || c.StartsWith("cerrar_rol:", StringComparison.Ordinal)))
        {
            await RevocarSesionesAsync(funcionario);
        }

        await _auditoria.RegistrarAsync(
            "REEMPLAZAR",
            "GENERAL",
            "cuenta",
            funcionarioId: FuncionarioActualId,
            detalle: $"{{\"evento\":\"editar_funcionario\",\"funcionario\":{funcionario.Id},\"cambios\":\"{string.Join(',', cambios)}\"}}");

        TempData["Aviso"] = cambios.Count == 0
            ? $"Funcionario {funcionario.Nombre} sin cambios."
            : $"Funcionario {funcionario.Nombre} actualizado.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<string>> RolesVigentesAsync(int funcionarioId)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        return await (
            from fr in _db.FuncionarioRoles.AsNoTracking()
            join r in _db.Roles on fr.RolId equals r.Id
            where fr.FuncionarioId == funcionarioId && (fr.Hasta == null || fr.Hasta >= hoy)
            select r.Codigo).ToListAsync();
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

        try
        {
            await _escritura.EjecutarAsync(FuncionarioActualId, _ =>
            {
                funcionario.Activo = activo;
                return Task.CompletedTask;
            });
        }
        catch (DbException ex)
        {
            _log.LogError(ex, "No se pudo cambiar el estado del funcionario {Id}", funcionario.Id);
            TempData["Aviso"] = "No se pudo conectar con la base de datos. Intente nuevamente.";
            return RedirectToAction(nameof(Index));
        }

                if (!activo)
        {
            // S-03: desactivar debe cortar el acceso ya otorgado, no esperar a
            // que caduque la cookie.
            await RevocarSesionesAsync(funcionario);
        }

        await _auditoria.RegistrarAsync(
            "REEMPLAZAR",
            "GENERAL",
            "cuenta",
            funcionarioId: FuncionarioActualId,
            detalle: $"{{\"evento\":\"cambiar_estado\",\"funcionario\":{funcionario.Id},\"activo\":{(activo ? "true" : "false")}}}");

        TempData["Aviso"] = activo
            ? $"Cuenta de {funcionario.Nombre} activada."
            : $"Cuenta de {funcionario.Nombre} desactivada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cerrar-rol")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarRol(int funcionarioId, int rolId)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // «Vigente» con la misma regla del resto del sistema (Hasta inclusiva):
        // un rol cerrado con el código anterior (Hasta = hoy) sigue vigente
        // hasta mañana y debe poder cerrarse.
        var relacion = await _db.FuncionarioRoles
            .FirstOrDefaultAsync(fr => fr.FuncionarioId == funcionarioId && fr.RolId == rolId && (fr.Hasta == null || fr.Hasta >= hoy));

        if (relacion is null)
        {
            TempData["Aviso"] = "Ese rol ya estaba cerrado.";
            return RedirectToAction(nameof(Index));
        }

        // Como en «desactivar» y «editar»: nadie se quita a sí mismo el rol
        // Administración TI.
        if (funcionarioId == FuncionarioActualId)
        {
            var codigo = await _db.Roles
                .Where(r => r.Id == rolId).Select(r => r.Codigo).FirstOrDefaultAsync();
            if (codigo == "ADMIN_TI")
            {
                TempData["Aviso"] = "No puede quitarse a sí mismo el rol Administración TI.";
                return RedirectToAction(nameof(Index));
            }
        }

        // Hasta es inclusiva («vigente» = Hasta es nulo o no anterior a hoy):
        // para que deje de estar vigente de inmediato, el último día válido
        // es ayer. Con Hasta = hoy seguiría vigente todo el día. Si el rol se
        // asignó hoy, ayer violaría la restricción hasta >= desde: la fila se
        // elimina (el trigger de auditoría la registra igual).
        try
        {
            await _escritura.EjecutarAsync(FuncionarioActualId, _ =>
            {
                if (relacion.Desde >= hoy)
                {
                    _db.FuncionarioRoles.Remove(relacion);
                }
                else
                {
                    relacion.Hasta = hoy.AddDays(-1);
                }
                return Task.CompletedTask;
            });
        }
        catch (DbException ex)
        {
            _log.LogError(ex, "No se pudo cerrar el rol {RolId} del funcionario {Id}", rolId, funcionarioId);
            TempData["Aviso"] = "No se pudo conectar con la base de datos. Intente nuevamente.";
            return RedirectToAction(nameof(Index));
        }

        // S-03: los permisos emitidos al iniciar sesión ya no representan al
        // funcionario; se renueva el sello para que la cookie deje de servir.
        var afectado = await _db.Funcionarios.FirstOrDefaultAsync(f => f.Id == funcionarioId);

        if (afectado is not null)
        {
            await RevocarSesionesAsync(afectado);
        }

        await _auditoria.RegistrarAsync(
            "REEMPLAZAR",
            "GENERAL",
            "cuenta",
            funcionarioId: FuncionarioActualId,
            detalle: $"{{\"evento\":\"cerrar_rol\",\"funcionario\":{funcionarioId},\"rol\":{rolId}}}");

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

        IdentityUser? usuario;
        try
        {
            usuario = await BuscarUsuarioAsync(funcionario);
        }
        catch (DbException ex)
        {
            _log.LogError(ex, "No se pudo buscar la cuenta del funcionario {Id}", funcionario.Id);
            TempData["Aviso"] = "No se pudo conectar con la base de datos. Intente nuevamente.";
            return RedirectToAction(nameof(Index));
        }

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

        IdentityUser? usuario;
        try
        {
            usuario = await BuscarUsuarioAsync(funcionario);
        }
        catch (DbException ex)
        {
            _log.LogError(ex, "No se pudo buscar la cuenta del funcionario {Id}", funcionario.Id);
            TempData["Aviso"] = "No se pudo conectar con la base de datos. Intente nuevamente.";
            return RedirectToAction(nameof(Index));
        }

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
