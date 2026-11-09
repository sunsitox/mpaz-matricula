using MarcelaPaz.Web.Servicios;

namespace MarcelaPaz.Tests;

/// <summary>
/// Reglas de estado de las autorizaciones del apoderado (MAT-04): vencimiento
/// por fecha, estado efectivo que se muestra y condición para revocar.
/// </summary>
public class AutorizacionesTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 8);

    [Fact]
    public void OtorgadaConPlazoVencido_SeConsideraVencida()
    {
        Assert.True(Autorizaciones.EstaVencida("OTORGADA", Hoy.AddDays(-1), Hoy));
        Assert.Equal("VENCIDA", Autorizaciones.EstadoEfectivo("OTORGADA", Hoy.AddDays(-1), Hoy));
        Assert.Equal("Vencida", Autorizaciones.EtiquetaEstado(
            Autorizaciones.EstadoEfectivo("OTORGADA", Hoy.AddDays(-1), Hoy)));
    }

    [Theory]
    [InlineData("OTORGADA", 0)]
    [InlineData("OTORGADA", 5)]
    [InlineData("REVOCADA", -3)]
    [InlineData("RECHAZADA", -3)]
    public void EstadosVigentesOSinPlazo_NoSeMarcanVencidos(string estado, int dias)
    {
        Assert.False(Autorizaciones.EstaVencida(estado, Hoy.AddDays(dias), Hoy));
        Assert.Equal(estado, Autorizaciones.EstadoEfectivo(estado, Hoy.AddDays(dias), Hoy));
    }

    [Fact]
    public void OtorgadaSinFechaDeTermino_SigueVigente()
    {
        Assert.False(Autorizaciones.EstaVencida("OTORGADA", null, Hoy));
        Assert.Equal("OTORGADA", Autorizaciones.EstadoEfectivo("OTORGADA", null, Hoy));
    }

    [Theory]
    [InlineData("OTORGADA", "Otorgada")]
    [InlineData("RECHAZADA", "Rechazada")]
    [InlineData("REVOCADA", "Revocada")]
    [InlineData("VENCIDA", "Vencida")]
    [InlineData("PENDIENTE", "Pendiente")]
    public void Etiquetas_SeMuestranEnEspanol(string estado, string etiqueta)
    {
        Assert.Equal(etiqueta, Autorizaciones.EtiquetaEstado(estado));
    }

    [Fact]
    public void SoloUnaAutorizacionOtorgadaPuedeRevocarse()
    {
        Assert.True(Autorizaciones.PuedeRevocarse("OTORGADA"));
        Assert.False(Autorizaciones.PuedeRevocarse("VENCIDA"));
        Assert.False(Autorizaciones.PuedeRevocarse("REVOCADA"));
        Assert.False(Autorizaciones.PuedeRevocarse("RECHAZADA"));
    }
}
