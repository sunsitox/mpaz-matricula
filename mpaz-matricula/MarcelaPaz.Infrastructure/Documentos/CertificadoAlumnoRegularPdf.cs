using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MarcelaPaz.Infrastructure.Documentos;

/// <summary>
/// Certificado de alumno regular (documento formal en una hoja A4):
/// membrete institucional, declaración de matrícula vigente, firma y bloque
/// de verificación (folio + código + QR). Recibe el contenido ya armado y
/// no consulta la base de datos.
/// </summary>
public sealed class CertificadoAlumnoRegularPdf
{
    private const string Azul = "#0C255B";
    private const string Gris = "#52606D";
    private const string Borde = "#C9D1DC";
    private const string Texto = "#1F2933";

    static CertificadoAlumnoRegularPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generar(DocumentoCertificado certificado)
    {
        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.MarginTop(1.4f, Unit.Centimetre);
                pagina.MarginBottom(1.2f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(10).FontColor(Texto));

                pagina.Header().Element(contenedor => Membrete(contenedor, certificado));
                pagina.Content().Element(contenedor => Cuerpo(contenedor, certificado));
                pagina.Footer().Element(contenedor => Verificacion(contenedor, certificado));
            });
        }).GeneratePdf();
    }

    // ── Membrete institucional ──────────────────────────────────────────────

    private static void Membrete(IContainer contenedor, DocumentoCertificado certificado)
    {
        contenedor.Column(columna =>
        {
            columna.Item().Row(fila =>
            {
                if (certificado.Logo is { Length: > 0 })
                {
                    fila.ConstantItem(20, Unit.Millimetre).Image(certificado.Logo).FitArea();
                }

                fila.RelativeItem().Column(bloque =>
                {
                    bloque.Item().AlignCenter()
                        .Text(certificado.Sostenedor.ToUpperInvariant())
                        .FontSize(8).SemiBold().FontColor(Gris);
                    bloque.Item().AlignCenter()
                        .Text(certificado.Establecimiento.ToUpperInvariant())
                        .FontSize(12.5f).Bold().FontColor(Azul);
                    bloque.Item().AlignCenter()
                        .Text($"RBD: {certificado.Rbd}")
                        .FontSize(8.5f).SemiBold();
                    if (!string.IsNullOrWhiteSpace(certificado.Reconocimiento))
                    {
                        bloque.Item().AlignCenter()
                            .Text(certificado.Reconocimiento)
                            .FontSize(7.5f).Italic().FontColor(Gris);
                    }

                    bloque.Item().AlignCenter()
                        .Text($"{certificado.Direccion} · {certificado.Contacto}")
                        .FontSize(7.5f).FontColor(Gris);
                });
            });

            columna.Item().PaddingTop(6).LineHorizontal(1).LineColor(Azul);
        });
    }

    // ── Declaración ─────────────────────────────────────────────────────────

    private static void Cuerpo(IContainer contenedor, DocumentoCertificado certificado)
    {
        contenedor.PaddingTop(10).Column(columna =>
        {
            columna.Item().AlignCenter()
                .Text("CERTIFICADO DE ALUMNO REGULAR")
                .FontSize(14).Bold().FontColor(Azul).LetterSpacing(1.5f);
            columna.Item().AlignCenter()
                .PaddingTop(2)
                .Text($"Folio N° {certificado.Folio}")
                .FontSize(9).SemiBold().FontColor(Gris);

            columna.Item().PaddingTop(14).Text(texto =>
            {
                texto.Line("La Dirección del establecimiento certifica que:").SemiBold();
            });

            columna.Item().PaddingTop(8).Text(texto =>
            {
                texto.Span("Don(ña) ");
                texto.Span(certificado.AlumnoNombre).Bold();
                texto.Span($", RUN {certificado.AlumnoRut}");
                if (!string.IsNullOrWhiteSpace(certificado.FechaNacimiento))
                {
                    texto.Span($", nacido(a) el {certificado.FechaNacimiento}");
                }

                texto.Span(", se encuentra matriculado(a) en calidad de ");
                texto.Span("alumno(a) regular").Bold().Underline();
                texto.Span(" de este establecimiento, cursando ");
                texto.Span(certificado.Curso).Bold();
                texto.Span($", correspondiente al año académico {certificado.Anio}.");
            });

            columna.Item().PaddingTop(8).Text(texto =>
            {
                texto.Span("Finalidad: ").SemiBold();
                texto.Span(certificado.Proposito);
            });

            columna.Item().PaddingTop(16).AlignRight()
                .Text(certificado.LugarFecha).SemiBold();

            // La firma vive en el flujo del contenido para que nunca quede
            // huérfana en otra hoja: el cuerpo se estira hasta ella.
            columna.Item().PaddingTop(44).AlignCenter().Column(firma =>
            {
                firma.Item().Width(62, Unit.Millimetre).LineHorizontal(0.7f).LineColor(Texto);
                firma.Item().PaddingTop(3).AlignCenter()
                    .Text(certificado.FirmanteNombre).FontSize(10).SemiBold();
                firma.Item().AlignCenter()
                    .Text(certificado.FirmanteCargo).FontSize(9).FontColor(Gris);
                firma.Item().AlignCenter()
                    .Text(certificado.Establecimiento).FontSize(8).FontColor(Gris);
            });
        });
    }

    // ── Verificación y timbre ───────────────────────────────────────────────

    private static void Verificacion(IContainer contenedor, DocumentoCertificado certificado)
    {
        contenedor.BorderTop(0.7f).BorderColor(Borde).PaddingTop(6).Row(fila =>
        {
            if (certificado.Qr is { Length: > 0 })
            {
                fila.ConstantItem(24, Unit.Millimetre).Image(certificado.Qr).FitArea();
            }

            fila.RelativeItem().PaddingLeft(8).Column(datos =>
            {
                datos.Item().Text($"Folio: {certificado.Folio}").FontSize(8).SemiBold();
                datos.Item().Text($"Código de verificación: {certificado.Codigo}").FontSize(8);
                datos.Item().Text($"Verifique en: {certificado.UrlValidacion}").FontSize(7.5f).FontColor(Gris);
            });

            fila.ConstantItem(58, Unit.Millimetre).Column(timbre =>
            {
                timbre.Item().Border(0.7f).BorderColor(Borde).Height(30, Unit.Millimetre)
                    .AlignCenter().AlignMiddle()
                    .Text("Timbre del establecimiento").FontSize(7.5f).FontColor(Gris);
            });
        });
    }
}