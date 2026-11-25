using MarcelaPaz.Infrastructure.Documentos;
using MarcelaPaz.Web.Servicios;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace MarcelaPaz.Tests;

/// <summary>
/// Certificado de alumno regular: folio legible, código con formato y hash
/// estable, el PDF se genera en una sola página y la redacción cambia si la
/// matrícula es de un año posterior al de emisión.
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

    public static DocumentoCertificado Muestra(
        string nombre = "Ana Ejemplo Prueba",
        string documento = "RUN 12.345.678-9",
        bool futura = false,
        byte[]? logo = null,
        string? vigencia = "Válido por 30 días, hasta el 10 de diciembre de 2026.") => new()
    {
        Sostenedor = "Servicio Local de Educación Pública Del Pino",
        Establecimiento = "Escuela Básica \"Escritora Marcela Paz\"",
        Rbd = "10571-6",
        Reconocimiento = "Reconocido oficialmente por el Ministerio de Educación de Chile",
        Direccion = "Ramón Liborio Carvallo 736, San Bernardo",
        AlumnoNombre = nombre,
        AlumnoDocumento = documento,
        Curso = "3° Básico A",
        Anio = 2027,
        MatriculaFutura = futura,
        LugarFecha = "San Bernardo, 10 de noviembre de 2026",
        Vigencia = vigencia,
        FirmanteNombre = "María Teresa Lagos Sandoval",
        FirmanteCargo = "Director/a",
        Folio = "CAR-2027-000001",
        Codigo = "ABCD-1234-EFGH",
        UrlValidacion = "https://ejemplo.cl/c/ABCD1234EFGH",
        Qr = GeneradorQr.Png("https://ejemplo.cl/c/ABCD1234EFGH"),
        Logo = logo,
    };

    [Fact]
    public void Vigencia_EsDeTreintaDiasYVenceAlCumplirlos()
    {
        var emitido = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(30, EmisionCertificado.DiasVigencia);
        Assert.Equal(emitido.AddDays(30), EmisionCertificado.VenceEn(emitido));
        Assert.True(EmisionCertificado.EstaVigente(emitido, emitido.AddDays(29)));
        Assert.True(EmisionCertificado.EstaVigente(emitido, emitido.AddDays(30).AddSeconds(-1)));
        Assert.False(EmisionCertificado.EstaVigente(emitido, emitido.AddDays(30)));
        Assert.False(EmisionCertificado.EstaVigente(emitido, emitido.AddDays(31)));
    }

    [Fact]
    public void CodigoParaUrl_CompactaElCodigo()
    {
        Assert.Equal(
            "ABCD1234EFGH",
            EmisionCertificado.CodigoParaUrl(" abcd-1234-efgh "));
    }

    [Fact]
    public void Pdf_CabeEnUnaSolaPagina()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var archivo = new CertificadoAlumnoRegularPdf().Generar(Muestra()).Generar();
        var paginas = ((IDocument)new CertificadoAlumnoRegularPdf().Generar(Muestra())).GenerateImages();

        Assert.True(archivo.Length > 1000);
        Assert.Equal("%PDF"u8.ToArray(), archivo[..4]);
        Assert.Single(paginas);
    }

    [Fact]
    public void Pdf_MatriculaFutura_CabeEnUnaSolaPagina()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var paginas = ((IDocument)new CertificadoAlumnoRegularPdf().Generar(Muestra(futura: true))).GenerateImages();

        Assert.Single(paginas);
    }
}
