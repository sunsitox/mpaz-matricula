using MarcelaPaz.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MarcelaPaz.Web.Seguridad;

/// <summary>
/// Emisor SSO del portal (prototipo del login único): convierte el login de
/// Identity en credenciales OpenID Connect para los demás sistemas del colegio.
/// Sólo desarrollo: en producción se detiene el arranque hasta configurar el
/// certificado real del emisor.
/// </summary>
public static class EmisorSso
{
    /// <summary>Alcance reservado para el futuro web service de datos.</summary>
    public const string AlcanceApi = "mpaz_api";

    public static IServiceCollection AddEmisorSso(
        this IServiceCollection servicios,
        IConfiguration configuracion,
        IHostEnvironment entorno)
    {
        servicios.AddOpenIddict()
            .AddCore(opciones =>
            {
                opciones.UseEntityFrameworkCore().UseDbContext<SsoDbContext>();
            })
            .AddServer(opciones =>
            {
                opciones.SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token");

                opciones.RegisterScopes("openid", "profile", "email", AlcanceApi);

                opciones.AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow();

                if (entorno.IsDevelopment())
                {
                    opciones.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
                }
                else
                {
                    throw new InvalidOperationException(
                        "El emisor SSO aún no está configurado para producción: falta el certificado de firma.");
                }

                var aspNetCore = opciones.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough();

                if (entorno.IsDevelopment())
                {
                    // El perfil de desarrollo corre en http local. Sólo prototipo.
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            });

        return servicios;
    }

    /// <summary>
    /// Registra el cliente y los scopes de prueba. Sólo en desarrollo: el
    /// registro real de clientes (Asistencia, web, Notas) es tarea del despliegue.
    /// </summary>
    public static async Task SembrarAsync(IServiceProvider proveedor)
    {
        var entorno = proveedor.GetRequiredService<IHostEnvironment>();

        if (!entorno.IsDevelopment())
        {
            return;
        }

        var configuracion = proveedor.GetRequiredService<IConfiguration>();
        var id = configuracion["Sso:ClientePruebaId"];
        var secreto = configuracion["Sso:ClientePruebaSecreto"];
        var retorno = configuracion["Sso:ClientePruebaRetorno"];

        if (string.IsNullOrWhiteSpace(id)
            || string.IsNullOrWhiteSpace(secreto)
            || string.IsNullOrWhiteSpace(retorno))
        {
            return;
        }

        var scopes = proveedor.GetRequiredService<IOpenIddictScopeManager>();

        if (await scopes.FindByNameAsync(AlcanceApi) is null)
        {
            await scopes.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = AlcanceApi,
                Resources = { AlcanceApi },
            });
        }

        var aplicaciones = proveedor.GetRequiredService<IOpenIddictApplicationManager>();

        // El descriptor se reescribe en cada arranque de desarrollo: así una
        // rotación del secreto en user-secrets queda aplicada sin recrear la base.
        var existente = await aplicaciones.FindByClientIdAsync(id);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = id,
            ClientType = ClientTypes.Confidential,
            ClientSecret = secreto,
            RedirectUris = { new Uri(retorno) },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + "openid",
                Permissions.Prefixes.Scope + "profile",
                Permissions.Prefixes.Scope + "email",
                Permissions.Prefixes.Scope + AlcanceApi,
            },
        };

        if (existente is null)
        {
            await aplicaciones.CreateAsync(descriptor);
        }
        else
        {
            await aplicaciones.UpdateAsync(existente, descriptor);
        }
    }
}
