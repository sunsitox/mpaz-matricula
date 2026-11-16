using MarcelaPaz.Web.Models;

namespace MarcelaPaz.Tests;

/// <summary>
/// Aceptación del Bloque C (MAT-03): contactos de emergencia y personas
/// autorizadas para el retiro. Cubre las reglas del paso 4 y 5, la exigencia de
/// al menos un autorizado cuando el estudiante no sale solo, y que ambos bloques
/// lleguen a la ficha impresa con sus datos.
/// </summary>
public class BloqueCTests
{
    private static FichaForm FormularioBase() => new()
    {
        Nombres = "Antonia",
        Apellido_Paterno = "Rojas",
        Tipo_Identificador = "RUN",
        Cedula_Identidad = "12.345.678-5",
    };

    [Fact]
    public void Paso4_SinContactos_ExigeAlMenosUno()
    {
        var errores = ValidadorFicha.Validar(4, FormularioBase());

        Assert.Contains(errores, error => error.Campo is null
            && error.Mensaje.Contains("contacto de emergencia", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Paso4_ContactoIncompleto_ExigeTelefonoYVinculo()
    {
        var form = FormularioBase();
        form.Contacto1_Identificacion = "María Rojas";

        var errores = ValidadorFicha.Validar(4, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Contacto1_Telefono));
        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Contacto1_Vinculo));
        Assert.DoesNotContain(errores, error => error.Campo == nameof(FichaForm.Contacto1_Identificacion));
    }

    [Fact]
    public void Paso4_ContactoCompleto_NoDejaErrores()
    {
        var form = FormularioBase();
        form.Contacto1_Identificacion = "María Rojas";
        form.Contacto1_Telefono = "+56912345678";
        form.Contacto1_Vinculo = "Abuela";

        var errores = ValidadorFicha.Validar(4, form);

        Assert.Empty(errores);
    }

    [Fact]
    public void Paso5_SalidaSoloNo_SinAutorizados_ExigeAlMenosUno()
    {
        var form = FormularioBase();
        form.SalidaSolo = "No";

        var errores = ValidadorFicha.Validar(5, form);

        Assert.Contains(errores, error => error.Campo is null
            && error.Mensaje.Contains("autorizada", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Paso5_ConUnAutorizadoCompleto_NoDejaErrores()
    {
        var form = FormularioBase();
        form.SalidaSolo = "No";
        form.Retiro1_Rut = "6.532.187-4";
        form.Retiro1_Vinculo = "Tía";

        var errores = ValidadorFicha.Validar(5, form);

        Assert.Empty(errores);
    }

    [Fact]
    public void Paso5_AutorizadoSinVinculo_SeRechaza()
    {
        var form = FormularioBase();
        form.SalidaSolo = "Si";
        form.Retiro1_Rut = "6.532.187-4";

        var errores = ValidadorFicha.Validar(5, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Retiro1_Vinculo));
    }

    [Fact]
    public void Impresion_IncluyeContactosYRetiros()
    {
        var datos = new Dictionary<string, string>
        {
            ["nombres"] = "Antonia",
            ["contacto emergencia 01: identificación"] = "María Rojas",
            ["contacto emergencia 01: teléfono"] = "+56912345678",
            ["contacto emergencia 01: vínculo"] = "Abuela",
            ["retiro autorizado 01: rut"] = "6.532.187-4",
            ["retiro autorizado 01: vínculo"] = "Tía",
            ["salida solo al finalizar jornada (SI/NO)"] = "No",
        };

        var documento = FichaImpresion.DesdeBorrador(datos, 2027, new DateTime(2026, 11, 12, 9, 0, 0));

        var contactos = documento.Secciones.FirstOrDefault(seccion => seccion.Titulo == "Contactos de emergencia");
        var retiros = documento.Secciones.FirstOrDefault(
            seccion => seccion.Titulo == "Personas autorizadas para el retiro");

        Assert.NotNull(contactos);
        Assert.NotNull(retiros);
        Assert.Contains(contactos!.Campos, campo => campo.Valor == "María Rojas");
        Assert.Contains(contactos.Campos, campo => campo.Valor == "+56912345678");
        Assert.Contains(retiros!.Campos, campo => campo.Valor == "6.532.187-4");
        Assert.Contains(retiros.Campos, campo => campo.Valor == "Tía");
    }
}
