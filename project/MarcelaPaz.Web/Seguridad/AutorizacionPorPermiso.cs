using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace MarcelaPaz.Web.Seguridad;

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

    private readonly DefaultAuthorizationPolicyProvider _respaldo;

    public PermisoPolicyProvider(IOptions<AuthorizationOptions> opciones)
    {
        _respaldo = new DefaultAuthorizationPolicyProvider(opciones);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _respaldo.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _respaldo.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string nombre)
    {
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

    public PermisoAuthorizationHandler(IAuditoriaAcceso auditoria, IHttpContextAccessor contexto)
    {
        _auditoria = auditoria;
        _contexto = contexto;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext contexto,
        PermisoRequirement requisito)
    {
        var esperado = $"{requisito.Area}:{requisito.Accion}";

        if (contexto.User.HasClaim("permiso", esperado))
        {
            contexto.Succeed(requisito);
            return;
        }

        var http = _contexto.HttpContext;
        var ruta = (http?.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                   ?? http?.Request.Path.ToString()
                   ?? "desconocido";

        await _auditoria.RegistrarAsync(
            accion: "DENEGADO",
            area: requisito.Area,
            recurso: Truncar(ruta, 63),
            detalle: $"{{\"permiso_requerido\":\"{esperado}\"}}");

        // Sin context.Fail() el resultado sigue siendo denegado por defecto.
    }

    private static string Truncar(string valor, int largo) =>
        valor.Length <= largo ? valor : valor[..largo];
}
