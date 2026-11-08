using MarcelaPaz.Web.Servicios;

namespace MarcelaPaz.Tests;

/// <summary>
/// Pruebas de regresión del expediente documental (MAT-06/07/09): formatos
/// aceptados, firma real del archivo y límite de tamaño alineado con el DDL.
/// </summary>
public class ExpedienteDocumentosTests
{
    [Theory]
    [InlineData(".pdf", "application/pdf")]
    [InlineData(".PDF", "application/pdf")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    public void MimePermitido_AceptaLosFormatosDelEsquema(string extension, string esperado)
    {
        Assert.Equal(esperado, ExpedienteDocumentos.MimePermitido(extension));
    }

    [Theory]
    [InlineData(".docx")]
    [InlineData(".gif")]
    [InlineData(".exe")]
    [InlineData("")]
    public void MimePermitido_RechazaOtrosFormatos(string extension)
    {
        Assert.Null(ExpedienteDocumentos.MimePermitido(extension));
    }

    [Fact]
    public void FirmaPdf_SeReconocePorSusPrimerosBytes()
    {
        Assert.True(ExpedienteDocumentos.FirmaValida("%PDF-1.7"u8, "application/pdf"));
        Assert.False(ExpedienteDocumentos.FirmaValida("<html>"u8, "application/pdf"));
    }

    [Fact]
    public void FirmaJpeg_SeReconocePorSusPrimerosBytes()
    {
        Assert.True(ExpedienteDocumentos.FirmaValida(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg"));
        Assert.False(ExpedienteDocumentos.FirmaValida(new byte[] { 0xFF, 0xD8 }, "image/jpeg"));
    }

    [Fact]
    public void FirmaPng_SeReconocePorSusPrimerosBytes()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        Assert.True(ExpedienteDocumentos.FirmaValida(png, "image/png"));
        Assert.False(ExpedienteDocumentos.FirmaValida(new byte[] { 0x89, 0x50 }, "image/png"));
    }

    [Fact]
    public void UnPdfRenombradoComoImagen_SeRechaza()
    {
        Assert.False(ExpedienteDocumentos.FirmaValida("%PDF-1.7"u8, "image/png"));
    }

    [Fact]
    public void LimiteDeTamano_CoincideConElDdl()
    {
        Assert.Equal(15 * 1024 * 1024, ExpedienteDocumentos.LimiteBytes);
    }
}
