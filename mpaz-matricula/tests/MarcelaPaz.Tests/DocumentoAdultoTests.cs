using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Models.Validaciones;

namespace MarcelaPaz.Tests;

/// <summary>
/// Regla de identificadores: estudiante RUN/IPE, adulto RUN/IPA.
/// Datos ficticios, sin rangos numéricos inventados.
/// </summary>
public class DocumentoAdultoTests
{
    [Theory]
    [InlineData("12.345.678-5")]
    [InlineData("6.532.187-4")]
    [InlineData("IPA-2026-001")]
    [InlineData("IPA-15")]
    public void Adulto_AceptaRunOipa(string documento)
    {
        Assert.True(DocumentoAdultoAttribute.EsValido(documento));
    }

    [Theory]
    [InlineData("IPE-2027-001")]
    [InlineData("ipe-2027-001")]
    [InlineData("12.345.678-9")]
    [InlineData("IP")]
    public void Adulto_RechazaIpeYRunInvalido(string documento)
    {
        Assert.False(DocumentoAdultoAttribute.EsValido(documento));
    }

    [Fact]
    public void Estudiante_MantieneRechazoDeIpa()
    {
        Assert.False(IdentificadorAttribute.EsValido("IPA-2026-001", "IPA"));
        Assert.False(IdentificadorAttribute.EsValido("IPA-2026-001", "IPE"));
        Assert.True(IdentificadorAttribute.EsValido("IPE-2027-001", "IPE"));
    }

    [Fact]
    public void Paso2_ApoderadoConIpa_NoDaErrores()
    {
        var form = new FichaForm
        {
            Madre_Nombre_Apellido = "Ana Ficticia",
            Madre_Cedula_Identidad = "IPA-2026-001",
            Madre_Telefono = "+56912345678",
            Restriccion_Madre = "No",
        };

        Assert.Empty(ValidadorFicha.Validar(2, form));
    }

    [Fact]
    public void Paso2_ApoderadoConIpe_SeRechaza()
    {
        var form = new FichaForm
        {
            Madre_Nombre_Apellido = "Ana Ficticia",
            Madre_Cedula_Identidad = "IPE-2027-001",
            Madre_Telefono = "+56912345678",
            Restriccion_Madre = "No",
        };

        Assert.Contains(ValidadorFicha.Validar(2, form),
            e => e.Campo == nameof(FichaForm.Madre_Cedula_Identidad));
    }

    [Fact]
    public void Paso3_PrincipalConIpa_NoDaErrores()
    {
        var form = new FichaForm
        {
            Principal_Nombre_Apellido = "Apoderado Ficticio",
            Principal_Cedula_Identidad = "IPA-2026-002",
            Principal_Telefono = "+56912345678",
            Principal_Vinculo = "Tío",
        };

        Assert.Empty(ValidadorFicha.Validar(3, form));
    }

    [Fact]
    public void Paso5_RetiroConIpa_NoDaErroresYRechazaIpe()
    {
        var conIpa = new FichaForm
        {
            Tipo_Identificador = "RUN",
            Cedula_Identidad = "12.345.678-5",
            SalidaSolo = "No",
            Retiro1_Rut = "IPA-2026-003",
            Retiro1_Vinculo = "Tío",
        };

        Assert.Empty(ValidadorFicha.Validar(5, conIpa));

        var conIpe = new FichaForm
        {
            Tipo_Identificador = "RUN",
            Cedula_Identidad = "12.345.678-5",
            SalidaSolo = "No",
            Retiro1_Rut = "IPE-2027-001",
            Retiro1_Vinculo = "Tío",
        };

        Assert.Contains(ValidadorFicha.Validar(5, conIpe),
            e => e.Campo == nameof(FichaForm.Retiro1_Rut));
    }
}
