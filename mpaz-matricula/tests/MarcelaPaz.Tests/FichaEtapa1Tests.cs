using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Models.Validaciones;
using MarcelaPaz.Web.Servicios;
using System.ComponentModel.DataAnnotations;

namespace MarcelaPaz.Tests;

/// <summary>
/// Pruebas de la Etapa I que no necesitan base de datos: identificación del
/// estudiante (Bloque A / MAT-02), validaciones del formulario y armado del
/// documento desde la copia congelada (MAT-08).
/// </summary>
public class FichaEtapa1Tests
{
    [Theory]
    [InlineData("12.345.678-5")]
    [InlineData("12345678-5")]
    [InlineData("6.532.187-4")]
    public void RunValido_AceptaConOSinPuntos(string run)
    {
        Assert.True(RutChileno.EsValido(run));
        Assert.Equal("12345678-5", RutChileno.Limpiar("12.345.678-5"));
    }

    [Theory]
    [InlineData("12.345.678-9")]
    [InlineData("0-0")]
    [InlineData("abc")]
    [InlineData("")]
    public void RunInvalido_SeRechaza(string run)
    {
        Assert.False(RutChileno.EsValido(run));
    }

    [Fact]
    public void IpeIpa_SeAceptaComoDocumentoProvisorio()
    {
        Assert.True(IdentificadorAttribute.EsValido("IPE-2027-001", "IPE"));
        Assert.True(IdentificadorAttribute.EsValido("IPA-15", "IPA"));
        Assert.False(IdentificadorAttribute.EsValido("IP", "IPE"));
        Assert.False(IdentificadorAttribute.EsValido("IPE 2027", "IPE"));
    }

    [Fact]
    public void DocumentoDeEstudiante_RespetaElTipoElegido()
    {
        var ipe = new FichaForm
        {
            Tipo_Identificador = "IPE",
            Cedula_Identidad = "IPE-2027-001",
        };

        var runInvalido = new FichaForm
        {
            Tipo_Identificador = "RUN",
            Cedula_Identidad = "IPE-2027-001",
        };

        Assert.True(EsValido(ipe, nameof(FichaForm.Cedula_Identidad)));
        Assert.False(IdentificadorAttribute.EsValido("IPE-2027-001", "RUN"));
        Assert.False(EsValido(runInvalido, nameof(FichaForm.Cedula_Identidad)));
    }

    [Fact]
    public void Paso1_ExigeLosDatosMinimosDelEstudiante()
    {
        var formulario = new FichaForm();
        var resultados = new List<ValidationResult>();
        var contexto = new ValidationContext(formulario);

        Validator.TryValidateObject(formulario, contexto, resultados, validateAllProperties: true);

        var campos = resultados
            .SelectMany(resultado => resultado.MemberNames)
            .ToHashSet();

        Assert.Contains(nameof(FichaForm.Nombres), campos);
        Assert.Contains(nameof(FichaForm.Apellido_Paterno), campos);
        Assert.Contains(nameof(FichaForm.Cedula_Identidad), campos);
        Assert.Contains(nameof(FichaForm.Fecha_Nacimiento), campos);
        Assert.Contains(nameof(FichaForm.Curso_Nivel), campos);
    }

    [Fact]
    public void ConfirmacionPendiente_NoInvalidaLosPasosAnteriores()
    {
        // La confirmación del paso 7 se exige en el controlador; si se validara
        // como atributo del modelo, cualquier POST de los pasos 1 a 6 fallaría.
        var formulario = new FichaForm
        {
            Curso_Nivel = "1º básico",
            Curso_Letra = "A",
            Curso_Anio = "2027",
            Tipo_Identificador = "RUN",
            Cedula_Identidad = "12.345.678-5",
            Nombres = "Prueba",
            Apellido_Paterno = "Test",
            Fecha_Nacimiento = "2015-05-10",
            Nacionalidad = "Chilena",
            Domicilio = "Calle 1",
            Comuna = "San Bernardo",
            Confirmo_Revision = false,
        };

        var resultados = new List<ValidationResult>();
        var valido = Validator.TryValidateObject(
            formulario,
            new ValidationContext(formulario),
            resultados,
            validateAllProperties: true);

        Assert.True(valido, string.Join("; ", resultados.Select(r => r.ErrorMessage)));
    }

    [Fact]
    public void FichaCerrada_UsaFolioYVersionDelSnapshot()
    {
        var datos = new Dictionary<string, string>
        {
            ["nombres"] = "Antonia",
            ["apellido paterno"] = "Contreras",
            ["cédula de identidad"] = "22123456-7",
            ["PIE: participa (SI/NO/PENDIENTE)"] = "Si",
            ["autorización de uso de imagen (SI/NO)"] = "No",
        };

        var documento = FichaImpresion.DesdeBorrador(
            datos,
            2027,
            new DateTime(2026, 11, 7, 10, 30, 0),
            logo: null,
            version: "versión 2 · vigente",
            folio: "MP-2027-00042");

        Assert.Equal("MP-2027-00042", documento.Folio);
        Assert.Equal("versión 2 · vigente", documento.Version);
        Assert.Contains(documento.Secciones, seccion => seccion.Titulo == "PIE y apoyos");
        Assert.Contains(documento.Autorizaciones, autorizacion => autorizacion.Respuesta == false);
    }

    [Fact]
    public void Snapshot_IlegibleNoRompeLaConsulta()
    {
        Assert.Empty(RegistroFicha.DatosDesdeSnapshot("no es json"));
        Assert.Empty(RegistroFicha.DatosDesdeSnapshot(null));

        var datos = RegistroFicha.DatosDesdeSnapshot("""{"campos":{"nombres":"Ana"}}""");

        Assert.Equal("Ana", datos["nombres"]);
    }

    [Fact]
    public void Paso4_SinContactos_ExigeAlMenosUno()
    {
        var errores = ValidadorFicha.Validar(4, new FichaForm());

        Assert.Contains(errores, error => error.Campo is null);
    }

    [Fact]
    public void Paso4_ContactoParcial_ExigeNombreTelefonoYVinculo()
    {
        var errores = ValidadorFicha.Validar(4, new FichaForm
        {
            Contacto1_Identificacion = "María Soto",
        });

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Contacto1_Telefono));
        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Contacto1_Vinculo));
        Assert.DoesNotContain(errores, error => error.Campo == nameof(FichaForm.Contacto1_Identificacion));
    }

    [Fact]
    public void Paso4_ContactoCompleto_NoDaErrores()
    {
        var errores = ValidadorFicha.Validar(4, new FichaForm
        {
            Contacto1_Identificacion = "María Soto",
            Contacto1_Telefono = "+56912345678",
            Contacto1_Vinculo = "Tía",
        });

        Assert.Empty(errores);
    }

    [Fact]
    public void Paso5_SinRespuesta_ExigeSalidaSolo()
    {
        var errores = ValidadorFicha.Validar(5, new FichaForm());

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.SalidaSolo));
    }

    [Fact]
    public void Paso5_SinSalidaSolo_ExigePersonaDeRetiro()
    {
        var errores = ValidadorFicha.Validar(5, new FichaForm { SalidaSolo = "No" });

        Assert.Contains(errores, error => error.Campo is null);
    }

    [Fact]
    public void Paso5_RetiroParcial_ExigeRutYVinculo()
    {
        var soloRut = ValidadorFicha.Validar(5, new FichaForm
        {
            SalidaSolo = "No",
            Retiro1_Rut = "12.345.678-5",
        });

        Assert.Contains(soloRut, error => error.Campo == nameof(FichaForm.Retiro1_Vinculo));

        var soloVinculo = ValidadorFicha.Validar(5, new FichaForm
        {
            SalidaSolo = "No",
            Retiro1_Vinculo = "Abuela",
        });

        Assert.Contains(soloVinculo, error => error.Campo == nameof(FichaForm.Retiro1_Rut));

        var rutInvalido = ValidadorFicha.Validar(5, new FichaForm
        {
            SalidaSolo = "No",
            Retiro1_Rut = "12.345.678-9",
            Retiro1_Vinculo = "Abuela",
        });

        Assert.Contains(rutInvalido, error => error.Campo == nameof(FichaForm.Retiro1_Rut));
    }

    [Fact]
    public void Paso2_SinRespuesta_ExigeRestriccion()
    {
        var errores = ValidadorFicha.Validar(2, new FichaForm
        {
            Madre_Nombre_Apellido = "Ana",
            Madre_Cedula_Identidad = "12.345.678-5",
            Madre_Telefono = "+56912345678",
        });

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Restriccion_Madre));
    }

    [Fact]
    public void Paso2_RestriccionSi_ExigeTipoYDetalle()
    {
        var incompleta = ValidadorFicha.Validar(2, new FichaForm
        {
            Madre_Nombre_Apellido = "Ana",
            Madre_Cedula_Identidad = "12.345.678-5",
            Madre_Telefono = "+56912345678",
            Restriccion_Madre = "Si",
        });

        Assert.Contains(incompleta, error => error.Campo == nameof(FichaForm.Restriccion_Madre_Tipo));
        Assert.Contains(incompleta, error => error.Campo == nameof(FichaForm.Restriccion_Madre_Detalle));

        var completa = ValidadorFicha.Validar(2, new FichaForm
        {
            Madre_Nombre_Apellido = "Ana",
            Madre_Cedula_Identidad = "12.345.678-5",
            Madre_Telefono = "+56912345678",
            Restriccion_Madre = "Si",
            Restriccion_Madre_Tipo = "Judicial",
            Restriccion_Madre_Detalle = "Orden del tribunal",
        });

        Assert.DoesNotContain(completa, error => error.Campo == nameof(FichaForm.Restriccion_Madre));
        Assert.DoesNotContain(completa, error => error.Campo == nameof(FichaForm.Restriccion_Madre_Tipo));
        Assert.DoesNotContain(completa, error => error.Campo == nameof(FichaForm.Restriccion_Madre_Detalle));
    }

    [Fact]
    public void Paso6_SituacionParcial_ExigeDetalleYTratamiento()
    {
        var errores = ValidadorFicha.Validar(6, new FichaForm
        {
            TieneSituacionMedica = "Si",
            Medica1_Identificacion = "Asma",
            Alergias = "Ninguna",
            Contacto_Sanitario_Nombre = "Pedro",
            Contacto_Sanitario_Telefono = "+56912345678",
            ParticipaPie = "No",
        });

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Medica1_Situacion));
        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Medica1_Tratamiento));
        Assert.DoesNotContain(errores, error => error.Campo == nameof(FichaForm.Medica1_Identificacion));
    }

    [Fact]
    public void Paso6_Pie_ExigeDiagnosticoYApoyos()
    {
        var sinDiagnostico = ValidadorFicha.Validar(6, new FichaForm
        {
            TieneSituacionMedica = "No",
            Alergias = "Ninguna",
            Contacto_Sanitario_Nombre = "Pedro",
            Contacto_Sanitario_Telefono = "+56912345678",
            ParticipaPie = "Si",
        });

        Assert.Contains(sinDiagnostico, error => error.Campo == nameof(FichaForm.PieDiagnostico));

        var sinApoyos = ValidadorFicha.Validar(6, new FichaForm
        {
            TieneSituacionMedica = "No",
            Alergias = "Ninguna",
            Contacto_Sanitario_Nombre = "Pedro",
            Contacto_Sanitario_Telefono = "+56912345678",
            ParticipaPie = "Si",
            PieDiagnostico = "Si",
        });

        Assert.Contains(sinApoyos, error => error.Campo == nameof(FichaForm.PieApoyos));
    }

    [Fact]
    public void Pdf_YaNoIncluyeParacetamol()
    {
        var datos = new Dictionary<string, string>
        {
            ["nombres"] = "Antonia",
            ["autorización de paracetamol (SI/NO)"] = "Si",
            ["autorización de uso de imagen (SI/NO)"] = "Si",
        };

        var documento = FichaImpresion.DesdeBorrador(datos, 2027, new DateTime(2026, 11, 7, 10, 30, 0));

        Assert.DoesNotContain(documento.Secciones.SelectMany(seccion => seccion.Campos),
            campo => campo.Etiqueta.Contains("aracetamol"));
        Assert.Equal(2, documento.Autorizaciones.Count);
    }

    private static bool EsValido(FichaForm formulario, string propiedad)
    {
        var resultados = new List<ValidationResult>();
        var contexto = new ValidationContext(formulario);
        Validator.TryValidateObject(formulario, contexto, resultados, validateAllProperties: true);

        return !resultados.Any(resultado => resultado.MemberNames.Contains(propiedad));
    }
}
