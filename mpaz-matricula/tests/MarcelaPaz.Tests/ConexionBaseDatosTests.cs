using MarcelaPaz.Infrastructure.Persistence;

namespace MarcelaPaz.Tests;

/// <summary>
/// La conexión tiene dos caminos: el servicio web primero y la conexión directa
/// a Neon como respaldo. La comprobación se inyecta para no abrir conexiones.
/// </summary>
public class ConexionBaseDatosTests
{
    private const string Pooler =
        "Host=ep-demo-pooler.c-9.us-east-2.aws.neon.tech;Port=5432;Database=mpaz;"
        + "Username=neondb_owner;Password=secreta;Ssl Mode=require";

    [Fact]
    public void ServicioWebResponde_SeUsaSinRespaldo()
    {
        var resultado = ConexionBaseDatos.Elegir(Pooler, probar: _ => true);

        Assert.False(resultado.EsRespaldo);
        Assert.Null(resultado.Aviso);
        Assert.Contains("-pooler", resultado.Cadena);
    }

    [Fact]
    public void ServicioWebNoResponde_SeUsaLaConexionDirecta()
    {
        var probadas = new List<string>();

        var resultado = ConexionBaseDatos.Elegir(Pooler, probar: cadena =>
        {
            probadas.Add(cadena);
            return !cadena.Contains("-pooler", StringComparison.OrdinalIgnoreCase);
        });

        Assert.True(resultado.EsRespaldo);
        Assert.Equal(2, probadas.Count);
        Assert.DoesNotContain("-pooler", resultado.Cadena);
        Assert.Contains("ep-demo.c-9.us-east-2.aws.neon.tech", resultado.Cadena);
        // El respaldo se deriva de la principal: no se repiten credenciales.
        Assert.Contains("Password=secreta", resultado.Cadena);
    }

    [Fact]
    public void NingunCaminoResponde_ConservaLaPrincipalYAvisa()
    {
        var resultado = ConexionBaseDatos.Elegir(Pooler, probar: _ => false);

        Assert.False(resultado.EsRespaldo);
        Assert.Contains("-pooler", resultado.Cadena);
        Assert.NotNull(resultado.Aviso);
    }

    [Fact]
    public void ConexionDirectaExplicita_MandaSobreLaDerivada()
    {
        const string directa =
            "Host=otro-host.neon.tech;Port=5432;Database=mpaz;"
            + "Username=neondb_owner;Password=secreta;Ssl Mode=require";

        var resultado = ConexionBaseDatos.Elegir(
            Pooler,
            directa,
            probar: cadena => cadena.Contains("otro-host", StringComparison.Ordinal));

        Assert.True(resultado.EsRespaldo);
        Assert.Contains("otro-host", resultado.Cadena);
    }

    [Fact]
    public void SinConfiguracion_IndicaQueFaltaLaCadena()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ConexionBaseDatos.Elegir(null, probar: _ => true));

        Assert.Contains("ConnectionStrings:Default", error.Message);
    }
}
