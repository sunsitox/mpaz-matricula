using System.Security.Claims;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Seguridad;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Emisor SSO (prototipo del login único): el funcionario ya entró al portal
/// con su correo y clave; aquí se le emite la credencial OpenID Connect que
/// aceptarán Asistencia, la web y Notas.
/// Sin consentimiento explícito por ser primera parte y prototipo: entrar al
/// portal equivale a aceptar.
/// </summary>
public class AutorizacionController : Controller
{
    private readonly UserManager<IdentityUser> _usuarios;
    private readonly MatriculaDbContext _db;

    public AutorizacionController(UserManager<IdentityUser> usuarios, MatriculaDbContext db)
    {
        _usuarios = usuarios;
        _db = db;
    }

    [Authorize]
    [IgnoreAntiforgeryToken]
    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> Autorizar()
    {
        var solicitud = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("Petición OpenID Connect inválida.");

        var usuario = await _usuarios.GetUserAsync(User);

        var funcionario = usuario is null
            ? null
            : await _db.Funcionarios.AsNoTracking().FirstOrDefaultAsync(
                f => f.Correo.ToLower() == (usuario.Email ?? string.Empty).ToLower());

        if (usuario is null || funcionario is null || !funcionario.Activo
            || string.IsNullOrWhiteSpace(funcionario.SsoSujeto))
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var identidad = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

        identidad.AddClaim(new Claim(Claims.Subject, funcionario.SsoSujeto).ConDestinos());
        identidad.AddClaim(new Claim(Claims.Name, funcionario.Nombre).ConDestinos());
        identidad.AddClaim(new Claim(Claims.Email, usuario.Email ?? funcionario.Correo).ConDestinos());

        var principal = new ClaimsPrincipal(identidad);
        principal.SetScopes(solicitud.GetScopes());

        if (solicitud.HasScope(EmisorSso.AlcanceApi))
        {
            principal.SetResources(EmisorSso.AlcanceApi);
        }

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}

/// <summary>Cada dato del token viaja en el access token y en el id_token.</summary>
file static class Destinos
{
    public static Claim ConDestinos(this Claim reclamo)
    {
        reclamo.SetDestinations(
            new[] { Destinations.AccessToken, Destinations.IdentityToken });
        return reclamo;
    }
}
