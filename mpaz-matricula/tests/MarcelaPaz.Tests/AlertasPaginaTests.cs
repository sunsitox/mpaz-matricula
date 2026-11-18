using MarcelaPaz.Web.Models;

namespace MarcelaPaz.Tests;

public class AlertasPaginaTests
{
    [Fact]
    public void ContadoresSeparanVencidasYPorVencer()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var pagina = new AlertasPagina
        {
            Alertas =
            [
                new AlertaVencimiento { VenceEl = hoy.AddDays(-5), Vencida = true },
                new AlertaVencimiento { VenceEl = hoy.AddDays(-1), Vencida = true },
                new AlertaVencimiento { VenceEl = hoy, Vencida = false },
                new AlertaVencimiento { VenceEl = hoy.AddDays(30), Vencida = false },
            ],
        };

        Assert.Equal(2, pagina.Vencidas);
        Assert.Equal(2, pagina.PorVencer);
    }

    [Theory]
    [InlineData(0, "Vence hoy")]
    [InlineData(1, "Vence en 1 día")]
    [InlineData(-1, "Venció hace 1 día")]
    public void PlazoDiceEstadoRelativo(int desplazamiento, string esperado)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var alerta = new AlertaVencimiento
        {
            VenceEl = hoy.AddDays(desplazamiento),
            Vencida = desplazamiento < 0,
        };

        Assert.Equal(esperado, alerta.Plazo);
    }
}
