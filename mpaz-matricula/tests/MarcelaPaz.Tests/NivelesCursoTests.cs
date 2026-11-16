using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Web.Models;
using System.ComponentModel.DataAnnotations;

namespace MarcelaPaz.Tests;

/// <summary>
/// El colegio va de pre-kínder a 8º básico: el catálogo, la clave del curso en
/// la base («PK A», «K A», «1° A») y el nombre que se muestra deben coincidir.
/// </summary>
public class NivelesCursoTests
{
    [Fact]
    public void Catalogo_VaDePreKinderAOctavoBasico()
    {
        Assert.Equal(NivelesCurso.PreKinder, NivelesCurso.Todos[0]);
        Assert.Equal(NivelesCurso.Kinder, NivelesCurso.Todos[1]);
        Assert.Equal("8º básico", NivelesCurso.Todos[^1]);
        Assert.Equal(10, NivelesCurso.Todos.Count);
    }

    [Theory]
    [InlineData("Pre-kínder", "PK")]
    [InlineData("pre kinder", "PK")]
    [InlineData("NT1", "PK")]
    [InlineData("Kínder", "K")]
    [InlineData("kinder", "K")]
    [InlineData("NT2", "K")]
    [InlineData("1º básico", "1")]
    [InlineData("8º básico", "8")]
    public void Token_CoincideConElNombreDelCurso(string nivel, string esperado)
    {
        Assert.Equal(esperado, NivelesCurso.Token(nivel));
    }

    [Theory]
    [InlineData("9º básico")]
    [InlineData("1º medio")]
    [InlineData("")]
    [InlineData(null)]
    public void Token_RechazaNivelesFueraDelColegio(string? nivel)
    {
        Assert.Null(NivelesCurso.Token(nivel));
    }

    [Theory]
    [InlineData("PK A", "Pre-kínder")]
    [InlineData("PK B", "Pre-kínder")]
    [InlineData("K A", "Kínder")]
    [InlineData("K B", "Kínder")]
    [InlineData("1° A", "1º básico")]
    [InlineData("8° B", "8º básico")]
    public void DesdeNombre_ProponeElNivelDelCursoAnterior(string curso, string esperado)
    {
        Assert.Equal(esperado, NivelesCurso.DesdeNombre(curso));
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("B", true)]
    [InlineData("C", false)]
    [InlineData("D", false)]
    public void Formulario_SoloAceptaLetrasAyB(string letra, bool valido)
    {
        var formulario = new FichaForm { Curso_Letra = letra };
        var resultados = new List<ValidationResult>();

        Validator.TryValidateObject(
            formulario, new ValidationContext(formulario), resultados, validateAllProperties: true);

        var rechazada = resultados.Any(resultado =>
            resultado.MemberNames.Contains(nameof(FichaForm.Curso_Letra)));

        Assert.Equal(valido, !rechazada);
    }

    [Theory]
    [InlineData("8º básico", true)]
    [InlineData("7º básico", false)]
    [InlineData("1º básico", false)]
    [InlineData("Pre-kínder", false)]
    [InlineData(null, false)]
    public void EsUltimo_SoloOctavoBasicoEgresa(string? nivel, bool esperado)
    {
        Assert.Equal(esperado, NivelesCurso.EsUltimo(nivel));
    }

    [Fact]
    public void Orden_PoneParvulariaAntesDeBasica()
    {
        Assert.True(NivelesCurso.Orden("PK A") < NivelesCurso.Orden("K A"));
        Assert.True(NivelesCurso.Orden("K A") < NivelesCurso.Orden("1° A"));
        Assert.True(NivelesCurso.Orden("8° B") < NivelesCurso.Orden("Sin curso"));
    }
}
