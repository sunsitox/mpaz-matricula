using System.Net;
using System.Text.Json;
using MarcelaPaz.Web.Servicios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarcelaPaz.Tests;

/// <summary>
/// Sincronización con el web service externo: con token válido registra
/// (POST /api/registro/); si el web service falla o está caído, devuelve
/// falso y rige el respaldo en Neon. La red se inyecta con un manejador
/// falso para no llamar al servicio real.
/// </summary>
public class RegistroWebServiceTests
{
    private sealed class ManejadorFalso : HttpMessageHandler
    {
        private readonly HttpStatusCode _codigo;
        private readonly Exception? _error;

        public HttpRequestMessage? UltimaPeticion { get; private set; }
        public string? UltimoCuerpo { get; private set; }

        public ManejadorFalso(HttpStatusCode codigo) => _codigo = codigo;

        public ManejadorFalso(Exception error) => _error = error;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage peticion, CancellationToken ct)
        {
            UltimaPeticion = peticion;
            UltimoCuerpo = await peticion.Content!.ReadAsStringAsync(ct);

            if (_error is not null)
            {
                throw _error;
            }

            return new HttpResponseMessage(_codigo)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    private static RegistroWebService Crear(ManejadorFalso manejador, bool conToken = true)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WebService:BaseUrl"] = "https://api.empaz.cl",
            ["WebService:Token"] = conToken ? "token-de-prueba" : null,
        }).Build();

        return new RegistroWebService(
            new HttpClient(manejador),
            configuracion,
            NullLogger<RegistroWebService>.Instance);
    }

    [Fact]
    public async Task ConTokenValido_RegistraPorPost()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.Created);
        var servicio = Crear(manejador);

        var registrado = await servicio.RegistrarAsync("Ana Prueba", "ana@mpaz.cl", "Clave2027x");

        Assert.True(registrado);
        Assert.Equal(HttpMethod.Post, manejador.UltimaPeticion!.Method);
        Assert.Equal("https://api.empaz.cl/api/registro/", manejador.UltimaPeticion.RequestUri!.ToString());
        Assert.Equal("Token", manejador.UltimaPeticion.Headers.Authorization!.Scheme);
        Assert.Equal("token-de-prueba", manejador.UltimaPeticion.Headers.Authorization.Parameter);

        using var cuerpo = JsonDocument.Parse(manejador.UltimoCuerpo!);
        Assert.Equal("Ana Prueba", cuerpo.RootElement.GetProperty("nombre").GetString());
        Assert.Equal("ana@mpaz.cl", cuerpo.RootElement.GetProperty("correo").GetString());
        Assert.Equal("Clave2027x", cuerpo.RootElement.GetProperty("contrasena").GetString());
    }

    [Fact]
    public async Task SiElWebServiceRechaza_NoRegistra()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.BadRequest);
        var servicio = Crear(manejador);

        var registrado = await servicio.RegistrarAsync("Ana Prueba", "ana@mpaz.cl", "Clave2027x");

        Assert.False(registrado);
    }

    [Fact]
    public async Task SiElWebServiceEstaCaido_DevuelveFalso()
    {
        var manejador = new ManejadorFalso(new HttpRequestException("red caída"));
        var servicio = Crear(manejador);

        var registrado = await servicio.RegistrarAsync("Ana Prueba", "ana@mpaz.cl", "Clave2027x");

        Assert.False(registrado);
    }

    [Fact]
    public async Task SinToken_NoConfigurado()
    {
        var servicio = Crear(new ManejadorFalso(HttpStatusCode.Created), conToken: false);

        Assert.False(servicio.Configurado);
        Assert.False(await servicio.RegistrarAsync("Ana Prueba", "ana@mpaz.cl", "Clave2027x"));
    }
}
