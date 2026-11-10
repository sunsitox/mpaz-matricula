using MarcelaPaz.Infrastructure.Documentos;
using MarcelaPaz.Web.Servicios;

namespace MarcelaPaz.Tests;

/// <summary>
/// Certificado de alumno regular: folio legible, código con formato y hash
/// estable, y el PDF se genera con el membrete y la verificación.
/// </summary>
public class CertificadoAlumnoRegularTests
{
    [Fact]
    public void Folio_EsCorrelativoPorAnio()
    {
        Assert.Equal("CAR-2027-000014", EmisionCertificado.FormatoFolio(2027, 14));
    }

    [Fact]
    public void Codigo_TieneFormatoEnGruposDeCuatro()
    {
        var codigo = EmisionCertificado.GenerarCodigo();
        var partes = codigo.Split('-');

        Assert.Equal(3, partes.Length);
        Assert.All(partes, parte => Assert.Equal(4, parte.Length));
    }

    [Fact]
    public void HashCodigo_EsEstableYToleraGuiones()
    {
        var codigo = EmisionCertificado.GenerarCodigo();

        Assert.Equal(
            EmisionCertificado.HashCodigo(codigo),
            EmisionCertificado.HashCodigo(codigo.Replace("-", string.Empty).ToLowerInvariant()));
        Assert.NotEqual(codigo, EmisionCertificado.HashCodigo(codigo));
    }

    [Fact]
    public void Pdf_SeGeneraConMembreteYVerificacion()
    {
        var pdf = new CertificadoAlumnoRegularPdf();
        var qr = GeneradorQr.Png("https://ejemplo.cl/validar?folio=CAR-2027-000001");

        var archivo = pdf.Generar(new DocumentoCertificado
        {
            Sostenedor = "Servicio Local de Educación Pública Del Pino",
            Establecimiento = "Escuela Básica \"Escritora Marcela Paz\"",
            Rbd = "10571-6",
            Reconocimiento = "Reconocido oficialmente por el Ministerio de Educación de Chile",
            Direccion = "Ramón Liborio Carvallo 736, San Bernardo",
            Contacto = "colegio.e.marcelapaz@gmail.com",
            AlumnoNombre = "Ana Ejemplo Prueba",
            AlumnoRut = "12.345.678-9",
            FechaNacimiento = "1 de enero de 2015",
            Curso = "3° Básico A",
            Anio = 2027,
            Proposito = EmisionCertificado.PropositoGenerico,
            LugarFecha = "San Bernardo, 10 de noviembre de 2027",
            FirmanteNombre = "La Dirección",
            FirmanteCargo = "Dirección",
            Folio = "CAR-2027-000001",
            Codigo = "ABCD-EFGH-IJKL",
            UrlValidacion = "https://ejemplo.cl/validar?folio=CAR-2027-000001",
            Qr = qr,
        });

        Assert.True(archivo.Length > 1000);
        Assert.Equal("%PDF"u8.ToArray(), archivo[..4]);
    }
}