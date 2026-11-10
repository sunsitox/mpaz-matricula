using QRCoder;

namespace MarcelaPaz.Infrastructure.Documentos;

/// <summary>
/// QR en PNG para los documentos emitidos. Sin estado: recibe el contenido
/// y devuelve los bytes listos para incrustar en el PDF.
/// </summary>
public static class GeneradorQr
{
    public static byte[] Png(string contenido, int pixelesPorModulo = 8)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(datos);
        return qr.GetGraphic(pixelesPorModulo);
    }
}