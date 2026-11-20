using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Web.Models;

namespace MarcelaPaz.Tests;

/// <summary>
/// El colegio va de Pre-kínder/Kínder a 8º básico: el catálogo, la clave del
/// curso en la base («Pre-kínder», «Kínder», «1° A») y el nombre que se
/// muestra deben coincidir. Pre-kínder y Kínder son cursos neutros, sin letra.
/// </summary>
public class NivelesCursoTests
{
    [Fact]
    public void Catalogo_VaDeParvulariaAOctavoBasico()
    {
        Assert.Equal(NivelesCurso.PreKinder, NivelesCurso.Todos[0]);
        Assert.Equal(NivelesCurso.Kinder, NivelesCurso.Todos[1]);
        Assert.Equal("1º básico", NivelesCurso.Todos[2]);
        Assert.Equal("8º básico", NivelesCurso.Todos[^1]);
        Assert.Equal(10, NivelesCurso.Todos.Count);
    }

    [Theory]
    [InlineData("pre kinder", "PK")]
    [InlineData("Pre-kínder", "PK")]
    [InlineData("NT1", "PK")]
    [InlineData("Kínder", "K")]
    [InlineData("kinder", "K")]
    [InlineData("NT2", "K")]
    [InlineData("Parvularia", "PARV")]
    [InlineData("1º básico", "1")]
    [InlineData("8º básico", "8")]
    public void Token_CoincideConElNivel(string nivel, string esperado)
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
    [InlineData("Pre-kínder", "Pre-kínder")]
    [InlineData("Kínder", "Kínder")]
    [InlineData("PK A", "Pre-kínder")]
    [InlineData("PK B", "Pre-kínder")]
    [InlineData("K A", "Kínder")]
    [InlineData("K B", "Kínder")]
    [InlineData("Parvularia", "Pre-kínder")]
    [InlineData("1° A", "1º básico")]
    [InlineData("8° B", "8º básico")]
    public void DesdeNombre_ProponeElNivelDelCursoAnterior(string curso, string esperado)
    {
        Assert.Equal(esperado, NivelesCurso.DesdeNombre(curso));
    }

    [Theory]
    [InlineData("1º básico", "A", true)]
    [InlineData("1º básico", "B", true)]
    [InlineData("1º básico", "C", false)]
    [InlineData("1º básico", "D", false)]
    [InlineData("1º básico", "", false)]
    [InlineData("Pre-kínder", "", true)]
    [InlineData("Pre-kínder", null, true)]
    [InlineData("Kínder", "", true)]
    [InlineData("Kínder", null, true)]
    public void Formulario_SoloAceptaLetrasAyBEnBasica(string nivel, string? letra, bool valido)
    {
        var formulario = new FichaForm { Curso_Nivel = nivel, Curso_Letra = letra };

        var errores = ValidadorFicha.Validar(1, formulario)
            .Where(error => error.Campo == nameof(FichaForm.Curso_Letra))
            .ToList();

        Assert.Equal(valido, errores.Count == 0);
    }

    [Theory]
    [InlineData("8º básico", true)]
    [InlineData("7º básico", false)]
    [InlineData("1º básico", false)]
    [InlineData("Pre-kínder", false)]
    [InlineData("Kínder", false)]
    [InlineData(null, false)]
    public void EsUltimo_SoloOctavoBasicoEgresa(string? nivel, bool esperado)
    {
        Assert.Equal(esperado, NivelesCurso.EsUltimo(nivel));
    }

    [Fact]
    public void EsParvularia_ReconocePreKinderYKinderSinLetra()
    {
        Assert.True(NivelesCurso.EsParvularia("Pre-kínder"));
        Assert.True(NivelesCurso.EsParvularia("Kínder"));
        Assert.True(NivelesCurso.EsSinLetra("Parvularia"));
        Assert.False(NivelesCurso.EsParvularia("1º básico"));
        Assert.False(NivelesCurso.EsParvularia(null));
    }

    [Fact]
    public void Orden_PonePreKinderYKinderAntesDeBasica()
    {
        Assert.True(NivelesCurso.Orden("Pre-kínder") < NivelesCurso.Orden("Kínder"));
        Assert.True(NivelesCurso.Orden("Kínder") < NivelesCurso.Orden("1° A"));
        Assert.Equal(NivelesCurso.Orden("Pre-kínder"), NivelesCurso.Orden("PK A"));
        Assert.Equal(NivelesCurso.Orden("Kínder"), NivelesCurso.Orden("K B"));
        Assert.True(NivelesCurso.Orden("8° B") < NivelesCurso.Orden("Sin curso"));
    }
}
