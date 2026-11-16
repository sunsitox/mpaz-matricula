using MarcelaPaz.Infrastructure.Persistence;

namespace MarcelaPaz.Tests;

/// <summary>
/// Conexión en caliente: al arrancar se elige el servicio web y, si deja de
/// responder, el portal sigue por la conexión directa sin reiniciarse.
/// La comprobación de red se inyecta para no abrir conexiones.
/// </summary>
public class ConexionVivaTests
{
    private const string Pooler =
        "Host=ep-demo-pooler.c-9.us-east-2.aws.neon.tech;Port=5432;Database=mpaz;"
        + "Username=neondb_owner;Password=secreta;Ssl Mode=require";

    private static bool EsDirecta(string cadena) =>
        !cadena.Contains("-pooler", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void ArrancaPorServicioWeb_SiResponde()
    {
        using var viva = new ConexionViva(Pooler, probar: _ => true);

        Assert.False(viva.EsRespaldo);
        Assert.Contains("-pooler", viva.CadenaActual);
        Assert.Null(viva.Aviso);
    }

    [Fact]
    public void ArrancaPorDirecta_SiElServicioWebNoResponde()
    {
        using var viva = new ConexionViva(Pooler, probar: EsDirecta);

        Assert.True(viva.EsRespaldo);
        Assert.True(EsDirecta(viva.CadenaActual));
        Assert.NotNull(viva.Aviso);
    }

    [Fact]
    public void SiElServicioWebCae_CambiaAlaDirectaSinReiniciar()
    {
        var servicioWebVivo = true;

        using var viva = new ConexionViva(
            Pooler,
            probar: cadena => EsDirecta(cadena) || servicioWebVivo);

        Assert.Contains("-pooler", viva.CadenaActual);

        servicioWebVivo = false;
        var quedoOperativa = viva.Verificar();

        Assert.True(quedoOperativa);
        Assert.True(viva.EsRespaldo);
        Assert.True(EsDirecta(viva.CadenaActual));
    }

    [Fact]
    public void SiLaDirectaCae_VuelveAlServicioWeb()
    {
        var directaViva = true;
        var servicioWebVivo = false;

        using var viva = new ConexionViva(
            Pooler,
            probar: cadena => EsDirecta(cadena) ? directaViva : servicioWebVivo);

        // Arranca por la directa porque el servicio web todavía no responde.
        Assert.True(viva.EsRespaldo);

        directaViva = false;
        servicioWebVivo = true;
        var quedoOperativa = viva.Verificar();

        Assert.True(quedoOperativa);
        Assert.False(viva.EsRespaldo);
        Assert.Contains("-pooler", viva.CadenaActual);
    }

    [Fact]
    public void SiNingunCaminoResponde_ConservaElQueTiene()
    {
        var todoVivo = true;

        using var viva = new ConexionViva(Pooler, probar: _ => todoVivo);
        var original = viva.CadenaActual;
        todoVivo = false;

        Assert.False(viva.Verificar());

        Assert.Equal(original, viva.CadenaActual);
    }
}
