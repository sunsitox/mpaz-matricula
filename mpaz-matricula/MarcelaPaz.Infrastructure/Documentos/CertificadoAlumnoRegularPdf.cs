using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MarcelaPaz.Infrastructure.Documentos;

/// <summary>
/// Certificado de alumno regular en hoja A4 completa: el contenido ocupa
/// todo el alto disponible (un flexible intermedio empuja la firma hacia
/// abajo y el pie de verificación va fijo al fondo), para que el papel se
/// vea formal y sin vacíos.
/// </summary>
public sealed class CertificadoAlumnoRegularPdf : IDocument
{
    private static readonly string Azul = Colors.Blue.Darken3;
    private static readonly string Gris = Colors.Grey.Darken2;
    private static readonly string GrisClaro = Colors.Grey.Lighten2;

    private DocumentoCertificado _certificado = null!;

    public CertificadoAlumnoRegularPdf Generar(DocumentoCertificado certificado)
    {
        _certificado = certificado;
        return this;
    }

    public byte[] Generar()
    {
        return Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.MarginLeft(56);
                pagina.MarginRight(56);
                pagina.MarginTop(44);
                pagina.MarginBottom(36);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(12));

                pagina.Content().Element(Cuerpo);
                pagina.Footer().Element(Verificacion);
            });
        }).GeneratePdf();
    }

    public void Compose(IDocumentContainer contenedor)
    {
        contenedor.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.MarginLeft(56);
            pagina.MarginRight(56);
            pagina.MarginTop(44);
            pagina.MarginBottom(36);
            pagina.DefaultTextStyle(estilo => estilo.FontSize(12));

            pagina.Content().Element(Cuerpo);
            pagina.Footer().Element(Verificacion);
        });
    }

    private void Cuerpo(IContainer contenedor)
    {
        contenedor.Column(columna =>
        {
            columna.Spacing(0);

            columna.Item().Element(Membrete);

            columna.Item().PaddingTop(8).Column(reglas =>
            {
                reglas.Item().LineHorizontal(2.2f).LineColor(Azul);
                reglas.Item().PaddingTop(2).LineHorizontal(0.7f).LineColor(GrisClaro);
            });

            columna.Item().PaddingTop(10).AlignRight()
                .Text($"Folio N° {_certificado.Folio}").FontSize(10).FontColor(Gris).Bold();

            columna.Item().PaddingTop(6).AlignCenter()
                .Text("CERTIFICADO DE ALUMNO REGULAR")
                .FontSize(16.5f).Bold().FontColor(Azul).LetterSpacing(0.12f);

            columna.Item().PaddingTop(14).Text(declaracion =>
            {
                declaracion.DefaultTextStyle(estilo => estilo.FontSize(12.5f).LineHeight(1.65f));
                declaracion.Justify();
                declaracion.Span("La Dirección de la ");
                declaracion.Span(_certificado.Establecimiento).Bold();
                declaracion.Span($", RBD N° {_certificado.Rbd}, dependiente del {_certificado.Sostenedor}, certifica que el(la) alumno(a) ");
                declaracion.Span(_certificado.AlumnoNombre.ToUpperInvariant()).Bold();
                declaracion.Span($", RUN N° {_certificado.AlumnoRut}, nacido(a) el {_certificado.FechaNacimiento}, se encuentra matriculado(a) en {_certificado.Curso} de este establecimiento, correspondiente al año académico {_certificado.Anio}.");
            });

            columna.Item().PaddingTop(10).Text(_certificado.Proposito)
                .FontSize(12.5f).LineHeight(1.65f).Justify();

            columna.Item().PaddingTop(10).Text(_certificado.LugarFecha)
                .FontSize(12.5f);

            // Margen fijo calculado para que la firma caiga al último tercio
            // y la hoja se vea completa (Extend empujaba la firma a otra página).
            columna.Item().PaddingTop(100).Element(Firmas);
        });
    }

    private void Membrete(IContainer contenedor)
    {
        contenedor.Column(columna =>
        {
            if (_certificado.Logo is { Length: > 0 })
            {
                columna.Item().AlignCenter().Height(64).Image(_certificado.Logo, ImageScaling.FitHeight);
            }

            columna.Item().PaddingTop(4).AlignCenter()
                .Text(_certificado.Sostenedor.ToUpperInvariant())
                .FontSize(9).FontColor(Gris).LetterSpacing(0.15f);

            columna.Item().PaddingTop(2).AlignCenter()
                .Text(_certificado.Establecimiento)
                .FontSize(15).Bold();

            columna.Item().AlignCenter()
                .Text(_certificado.Reconocimiento)
                .FontSize(9).Italic().FontColor(Gris);

            columna.Item().PaddingTop(2).AlignCenter()
                .Text($"{_certificado.Direccion} · {_certificado.Contacto}")
                .FontSize(9).FontColor(Gris);
        });
    }

    private void Firmas(IContainer contenedor)
    {
        contenedor.Row(fila =>
        {
            fila.RelativeItem().Column(columna =>
            {
                columna.Item().PaddingTop(40).AlignCenter().Width(230).LineHorizontal(0.8f).LineColor(Gris);
                columna.Item().AlignCenter()
                    .Text(_certificado.FirmanteNombre).FontSize(12.5f).Bold();
                columna.Item().AlignCenter()
                    .Text($"{_certificado.FirmanteCargo} · {_certificado.Establecimiento}")
                    .FontSize(10).FontColor(Gris);
            });

            fila.ConstantItem(128).Column(columna =>
            {
                columna.Item().Height(104).Border(0.8f).BorderColor(GrisClaro)
                    .AlignCenter().AlignMiddle()
                    .Text("Timbre del establecimiento").FontSize(8).FontColor(Gris);
            });
        });
    }

    private void Verificacion(IContainer contenedor)
    {
        contenedor
            .BorderTop(0.7f).BorderColor(GrisClaro)
            .PaddingTop(8)
            .Column(columna =>
            {
                columna.Item().Text("VERIFICACIÓN DE AUTENTICIDAD")
                    .FontSize(8.5f).Bold().FontColor(Gris).LetterSpacing(1.2f);

                columna.Item().PaddingTop(4).Row(fila =>
                {
                    if (_certificado.Qr is { Length: > 0 })
                    {
                        fila.ConstantItem(72).Image(_certificado.Qr);
                        fila.ConstantItem(10);
                    }

                    fila.RelativeItem().Column(datos =>
                    {
                        datos.Item().Text(texto =>
                        {
                            texto.Span("Folio: ").Bold();
                            texto.Span(_certificado.Folio);
                            texto.Span("   ·   Código: ").Bold();
                            texto.Span(_certificado.Codigo);
                        });
                        datos.Item().Text($"Valide este documento en: {_certificado.UrlValidacion}");
                        datos.Item().Text("La versión digital es válida con su folio y código; el papel además lleva timbre y firma.")
                            .Italic().FontColor(Gris);
                    });
                });
            });
    }
}
