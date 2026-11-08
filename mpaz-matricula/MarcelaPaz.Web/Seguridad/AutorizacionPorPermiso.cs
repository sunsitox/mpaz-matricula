using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MarcelaPaz.Web.Seguridad;

/// <summary>
/// Interruptor temporal de seguridad. Con <see cref="ExigirInicioSesion"/> en false
/// las políticas dejan pasar sin sesión, para revisar la interfaz mientras se
/// ajustan los estilos. En true vuelve el acceso obligatorio.
/// </summary>
public sealed class ConfiguracionSeguridad
{
    public ConfiguracionSeguridad(bool exigirInicioSesion)
    {
        ExigirInicioSesion = exigirInicioSesion;
    }

    public bool ExigirInicioSesion { get; }
}

/// <summary>
/// Exige un permiso concreto (área + acción) calculado desde <c>rol_permiso</c>.
/// El nombre de la política se escribe "Permiso:AREA:ACCION".
/// </summary>
public sealed class PermisoRequirement : IAuthorizationRequirement
{
    public PermisoRequirement(string area, string accion)
    {
        Area = area;
        Accion = accion;
    }

    public string Area { get; }

    public string Accion { get; }
}

/// <summary>
/// Crea políticas a partir de su nombre, sin tener que registrar cada una.
/// Las políticas que no empiezan con "Permiso:" se delegan a las estándar.
/// </summary>
public sealed class PermisoPolicyProvider : IAuthorizationPolicyProvider
{
    public const string Prefijo = "Permiso:";

    /// <summary>Política que no exige autenticación ni permisos (modo revisión).</summary>
    private static readonly AuthorizationPolicy Abierta =
        new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();

    private readonly DefaultAuthorizationPolicyProvider _respaldo;
    private readonly ConfiguracionSeguridad _seguridad;

    public PermisoPolicyProvider(IOptions<AuthorizationOptions> opciones, ConfiguracionSeguridad seguridad)
    {
        _respaldo = new DefaultAuthorizationPolicyProvider(opciones);
        _seguridad = seguridad;
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _seguridad.ExigirInicioSesion ? _respaldo.GetDefaultPolicyAsync() : Task.FromResult(Abierta);

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _seguridad.ExigirInicioSesion ? _respaldo.GetFallbackPolicyAsync() : Task.FromResult<AuthorizationPolicy?>(Abierta);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string nombre)
    {
        if (!_seguridad.ExigirInicioSesion)
        {
            return Task.FromResult<AuthorizationPolicy?>(Abierta);
        }

        if (nombre.StartsWith(Prefijo, StringComparison.Ordinal))
        {
            var partes = nombre.Split(':');
            if (partes.Length == 3)
            {
                var politica = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermisoRequirement(partes[1], partes[2]))
                    .Build();

                return Task.FromResult<AuthorizationPolicy?>(politica);
            }
        }

        return _respaldo.GetPolicyAsync(nombre);
    }
}

/// <summary>
/// Verifica el permiso en el servidor y, si falta, registra el intento denegado
/// en <c>registros_acceso</c> antes de responder 403.
/// </summary>
public sealed class PermisoAuthorizationHandler : AuthorizationHandler<PermisoRequirement>
{
    private readonly IAuditoriaAcceso _auditoria;
    private readonly IHttpContextAccessor _contexto;
    private readonly VigilanteSesion _vigilante;

    public PermisoAuthorizationHandler(
        IAuditoriaAcceso auditoria,
        IHttpContextAccessor contexto,
        VigilanteSesion vigilante)
    {
        _auditoria = auditoria;
        _contexto = contexto;
        _vigilante = vigilante;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext contexto,
        PermisoRequirement requisito)
    {
        var esperado = $"{requisito.Area}:{requisito.Accion}";

        if (!contexto.User.HasClaim("permiso", esperado))
        {
            await DenegarAsync(requisito, esperado, "permiso ausente");
            return;
        }

        // El permiso del claim se emitió al iniciar sesión: antes de conceder nada
        // se confirma que el funcionario sigue activo y con los mismos permisos.
        if (!await _vigilante.SigueVigenteAsync(contexto.User))
        {
            await DenegarAsync(requisito, esperado, "sesión revocada");
            return;
        }

        contexto.Succeed(requisito);
    }

    private async Task DenegarAsync(PermisoRequirement requisito, string esperado, string motivo)
    {
        var http = _contexto.HttpContext;
        var ruta = (http?.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                   ?? http?.Request.Path.ToString()
                   ?? "desconocido";

        await _auditoria.RegistrarAsync(
            accion: "DENEGADO",
            area: requisito.Area,
            recurso: Truncar(ruta, 63),
            detalle: $"{{\"permiso_requerido\":\"{esperado}\",\"motivo\":\"{motivo}\"}}");
    }

    private static string Truncar(string valor, int largo) =>
        valor.Length <= largo ? valor : valor[..largo];
}
