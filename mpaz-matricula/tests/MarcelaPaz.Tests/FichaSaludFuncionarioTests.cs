using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Web.Models;

namespace MarcelaPaz.Tests;

/// <summary>
/// Aceptación de la ficha de salud del funcionario (paso 25, sólo local):
/// documento, contactos de emergencia, sistema de salud y transfusión.
/// </summary>
public class FichaSaludFuncionarioTests
{

    private static FichaSaludFuncionario FichaValida() => new()
    {
        FuncionarioId = 1,
        NombreCompleto = "María Teresa Lagos Sandoval",
        Rut = "12.345.678-5",
        TelefonoPersonal = "+56 9 6123 4567",
        DireccionPersonal = "Ramón Liborio Carvallo 736",
        Contacto1Nombre = "Pedro Lagos",
        Contacto1Telefono = "+56 9 2233 4455",
        SistemaSalud = "Fonasa",
        CentroMedicoPreferente = "CESFAM San Bernardo",
        AceptaTransfusion = "Si",
    };

    [Fact]
    public void FichaCompleta_NoDejaErrores()
    {
        Assert.Empty(ValidadorFichaSalud.Validar(FichaValida()));
    }

    [Fact]
    public void SinRutNiPasaporte_SeRechaza()
    {
        var ficha = FichaValida();
        ficha.Rut = null;

        Assert.Contains(ValidadorFichaSalud.Validar(ficha), e => e.Campo == nameof(FichaSaludFuncionario.Rut));
    }

    [Fact]
    public void ConPasaporteSinRut_SeAcepta()
    {
        var ficha = FichaValida();
        ficha.Rut = null;
        ficha.Pasaporte = "P12345678";

        Assert.Empty(ValidadorFichaSalud.Validar(ficha));
    }

    [Fact]
    public void RutInvalido_SeRechaza()
    {
        var ficha = FichaValida();
        ficha.Rut = "12.345.678-0";

        Assert.Contains(ValidadorFichaSalud.Validar(ficha), e => e.Campo == nameof(FichaSaludFuncionario.Rut));
    }

    [Fact]
    public void SinContacto1_SeRechaza()
    {
        var ficha = FichaValida();
        ficha.Contacto1Nombre = null;
        ficha.Contacto1Telefono = null;

        var errores = ValidadorFichaSalud.Validar(ficha);

        Assert.Contains(errores, e => e.Campo == nameof(FichaSaludFuncionario.Contacto1Nombre));
        Assert.Contains(errores, e => e.Campo == nameof(FichaSaludFuncionario.Contacto1Telefono));
    }

    [Fact]
    public void ContactosConMismoTelefono_SeRechazan()
    {
        var ficha = FichaValida();
        ficha.Contacto2Nombre = "Ana Lagos";
        ficha.Contacto2Telefono = "56922334455";

        var errores = ValidadorFichaSalud.Validar(ficha);

        Assert.Contains(
            errores,
            e => e.Campo == nameof(FichaSaludFuncionario.Contacto2Telefono));
    }

    [Fact]
    public void SistemaSaludFueraDeLista_SeRechaza()
    {
        var ficha = FichaValida();
        ficha.SistemaSalud = "Seguro externo";

        Assert.Contains(ValidadorFichaSalud.Validar(ficha), e => e.Campo == nameof(FichaSaludFuncionario.SistemaSalud));
    }

    [Fact]
    public void TransfusionSinRespuesta_SeRechaza()
    {
        var ficha = FichaValida();
        ficha.AceptaTransfusion = null;

        Assert.Contains(ValidadorFichaSalud.Validar(ficha), e => e.Campo == nameof(FichaSaludFuncionario.AceptaTransfusion));
    }

    [Fact]
    public void RechazaTransfusionSinMotivo_SeRechaza()
    {
        var ficha = FichaValida();
        ficha.AceptaTransfusion = "No";

        Assert.Contains(ValidadorFichaSalud.Validar(ficha), e => e.Campo == nameof(FichaSaludFuncionario.MotivoTransfusion));
    }

    [Fact]
    public void RechazaTransfusionConMotivo_SeAcepta()
    {
        var ficha = FichaValida();
        ficha.AceptaTransfusion = "No";
        ficha.MotivoTransfusion = "Motivo religioso";

        Assert.Empty(ValidadorFichaSalud.Validar(ficha));
    }
}
