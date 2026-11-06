using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Web.Seguridad;

/// <summary>
/// Enlaza la tabla <c>funcionario</c> con las credenciales de Identity.
///
/// Los funcionarios vienen cargados en la base (nombre, correo y rol). Se les
/// crea una cuenta faltante sólo cuando el despliegue entrega una contraseña
/// inicial mediante configuración protegida, y se guarda el id de Identity en
/// <c>sso_sujeto</c>. Si el enlace guardado no corresponde, se vuelve a enlazar.
/// </summary>
public class CuentasFuncionario
{
    private readonly MatriculaDbContext _db;
    private readonly UserManager<IdentityUser> _usuarios;
    private readonly ILogger<CuentasFuncionario> _log;
    private readonly string? _contrasenaInicial;

    public CuentasFuncionario(
        MatriculaDbContext db,
        UserManager<IdentityUser> usuarios,
        IConfiguration configuracion,
        ILogger<CuentasFuncionario> log)
    {
        _db = db;
        _usuarios = usuarios;
        _log = log;
        _contrasenaInicial = configuracion["Seguridad:ContrasenaInicial"];
    }

    /// <summary>
    /// Devuelve la cuenta de Identity del funcionario, creándola si todavía no
    /// existe y dejando el enlace en <c>sso_sujeto</c>. Null si no se pudo crear.
    /// </summary>
    public async Task<(IdentityUser? Usuario, bool Creada)> AsegurarAsync(
        Funcionario funcionario,
        CancellationToken ct = default)
    {
        var correo = funcionario.Correo.Trim().ToLowerInvariant();
        var creada = false;

        IdentityUser? usuario = null;

        if (!string.IsNullOrWhiteSpace(funcionario.SsoSujeto))
        {
            usuario = await _usuarios.FindByIdAsync(funcionario.SsoSujeto);
        }

        // Los datos de prueba traen enlaces que no existen en Identity: se busca
        // por correo antes de crear otra cuenta para el mismo funcionario.
        usuario ??= await _usuarios.FindByEmailAsync(correo)
                    ?? await _usuarios.FindByNameAsync(correo);

        if (usuario is null)
        {
            if (string.IsNullOrWhiteSpace(_contrasenaInicial))
            {
                _log.LogWarning(
                    "El funcionario {Correo} no tiene cuenta y Seguridad:ContrasenaInicial no está configurada.",
                    correo);
                return (null, false);
            }

            usuario = new IdentityUser
            {
                UserName = correo,
                Email = correo,
                EmailConfirmed = true,
            };

            var resultado = await _usuarios.CreateAsync(usuario, _contrasenaInicial);

            if (!resultado.Succeeded)
            {
                _log.LogWarning(
                    "No se pudo crear la cuenta de {Correo}: {Errores}",
                    correo,
                    string.Join("; ", resultado.Errors.Select(error => error.Description)));

                return (null, false);
            }

            creada = true;
            _log.LogInformation("Cuenta creada para el funcionario {Correo}.", correo);
        }

        if (!string.Equals(funcionario.SsoSujeto, usuario.Id, StringComparison.Ordinal))
        {
            funcionario.SsoSujeto = usuario.Id;
            await _db.SaveChangesAsync(ct);
        }

        return (usuario, creada);
    }

    /// <summary>
    /// Recorre los funcionarios activos de la base al iniciar el portal. Es
    /// idempotente: sólo crea cuentas faltantes y corrige enlaces.
    /// </summary>
    public async Task<(int Creadas, int Enlazadas)> SembrarAsync(CancellationToken ct = default)
    {
        var funcionarios = await _db.Funcionarios
            .Where(f => f.Activo)
            .OrderBy(f => f.Id)
            .ToListAsync(ct);

        var creadas = 0;
        var enlazadas = 0;

        foreach (var funcionario in funcionarios)
        {
            if (string.IsNullOrWhiteSpace(funcionario.Correo))
            {
                continue;
            }

            var (usuario, creada) = await AsegurarAsync(funcionario, ct);

            if (usuario is null)
            {
                continue;
            }

            if (creada)
            {
                creadas++;
            }
            else
            {
                enlazadas++;
            }
        }

        return (creadas, enlazadas);
    }
}
