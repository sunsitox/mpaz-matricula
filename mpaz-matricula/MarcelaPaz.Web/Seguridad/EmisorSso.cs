using MarcelaPaz.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using System.Security.Cryptography.X509Certificates;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MarcelaPaz.Web.Seguridad;

/// <summary>
/// Indica si el emisor SSO quedó activo en este despliegue. La pantalla de
/// autorización lo consulta para no ofrecer endpoints que no existen.
/// </summary>
public sealed class ConfiguracionSso
{
    public ConfiguracionSso(bool habilitado) => Habilitado = habilitado;

    public bool Habilitado { get; }
}

/// <summary>
/// Emisor SSO del portal (prototipo del login único): convierte el login de
/// Identity en credenciales OpenID Connect para los demás sistemas del colegio.
///
/// El emisor se habilita con <c>Sso:Habilitado</c>. Por omisión está activo sólo
/// en Desarrollo: un portal en producción puede desplegarse sin SSO mientras el
/// colegio no entregue el certificado de firma, en vez de impedir el arranque.
/// Si se habilita en producción, se exige el certificado configurado.
/// </summary>
public static class EmisorSso
{
    /// <summary>Alcance reservado para el futuro web service de datos.</summary>
    public const string AlcanceApi = "mpaz_api";

    /// <summary>
    /// ¿El emisor está activo? Se puede forzar con <c>Sso:Habilitado</c>;
    /// sin ese valor, sólo se activa en Desarrollo.
    /// </summary>
    public static bool EstaHabilitado(IConfiguration configuracion, IHostEnvironment entorno) =>
        configuracion.GetValue("Sso:Habilitado", entorno.IsDevelopment());

    public static IServiceCollection AddEmisorSso(
        this IServiceCollection servicios,
        IConfiguration configuracion,
        IHostEnvironment entorno)
    {
        var habilitado = EstaHabilitado(configuracion, entorno);

        servicios.AddSingleton(new ConfiguracionSso(habilitado));

        if (!habilitado)
        {
            // Sin emisor no se registra OpenIddict: el resto del portal no
            // depende de él y el despliegue sigue funcionando.
            return servicios;
        }

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
                    var certificado = CertificadoFirma(configuracion)
                        ?? throw new InvalidOperationException(
                            "El emisor SSO está habilitado en producción y falta el certificado de firma: "
                            + "configure Sso:CertificadoPfx (con Sso:CertificadoClave) o "
                            + "Sso:CertificadoPem/Sso:CertificadoPemClave; o deje Sso:Habilitado=false.");

                    opciones.AddSigningCertificate(certificado)
                        .AddEncryptionCertificate(certificado);
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
    /// Certificado real del emisor: PKCS#12 (<c>Sso:CertificadoPfx</c>) o par
    /// PEM (<c>Sso:CertificadoPem</c> + <c>Sso:CertificadoPemClave</c>). El
    /// archivo se mantiene fuera del repositorio, igual que el resto de claves.
    /// </summary>
    private static X509Certificate2? CertificadoFirma(IConfiguration configuracion)
    {
        var pfx = configuracion["Sso:CertificadoPfx"];

        if (!string.IsNullOrWhiteSpace(pfx))
        {
            var clave = configuracion["Sso:CertificadoClave"];

            return X509CertificateLoader.LoadPkcs12FromFile(
                pfx, string.IsNullOrWhiteSpace(clave) ? null : clave);
        }

        var pem = configuracion["Sso:CertificadoPem"];
        var clavePem = configuracion["Sso:CertificadoPemClave"];

        if (!string.IsNullOrWhiteSpace(pem) && !string.IsNullOrWhiteSpace(clavePem))
        {
            return X509Certificate2.CreateFromPemFile(pem, clavePem);
        }

        return null;
    }

    /// <summary>
    /// Registra el cliente y los scopes de prueba. Sólo en desarrollo y con el
    /// emisor activo: el registro real de clientes (Asistencia, web, Notas) es
    /// tarea del despliegue.
    /// </summary>
    public static async Task SembrarAsync(IServiceProvider proveedor)
    {
        var entorno = proveedor.GetRequiredService<IHostEnvironment>();
        var habilitado = proveedor.GetRequiredService<ConfiguracionSso>().Habilitado;

        if (!entorno.IsDevelopment() || !habilitado)
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
