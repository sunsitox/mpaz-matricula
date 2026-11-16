using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MarcelaPaz.Infrastructure.Documentos;

/// <summary>
/// Certificado de alumno regular en una sola página A4. Lleva sólo lo que el
/// documento necesita para ser válido: identificación del establecimiento
/// (nombre, RBD, reconocimiento oficial y dirección), nombre y RUN del alumno,
/// curso y año académico, folio, lugar y fecha, y la firma con su cargo. El
/// QR al pie permite comprobar el papel sin agregar datos al texto.
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
        return Document.Create(Compose).GeneratePdf();
    }

    public void Compose(IDocumentContainer contenedor)
    {
        contenedor.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.MarginLeft(56);
            pagina.MarginRight(56);
            pagina.MarginTop(42);
            pagina.MarginBottom(38);
            pagina.DefaultTextStyle(estilo => estilo.FontSize(12));

            pagina.Content().Element(Cuerpo);

            // La firma cierra la hoja: el contenido superior fluye libre y el
            // pie queda siempre abajo, con espacio para el timbre manual.
            pagina.Footer().Element(Pie);
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
                reglas.Item().LineHorizontal(2.4f).LineColor(Azul);
                reglas.Item().PaddingTop(2).LineHorizontal(0.7f).LineColor(GrisClaro);
            });

            columna.Item().PaddingTop(16).AlignCenter()
                .Text("CERTIFICADO DE ALUMNO REGULAR")
                .FontSize(20).Bold().FontColor(Azul).LetterSpacing(0.1f);

            columna.Item().PaddingTop(6).AlignCenter()
                .Text($"Folio N° {_certificado.Folio}")
                .FontSize(10.5f).FontColor(Gris).LetterSpacing(0.05f);

            columna.Item().PaddingTop(30).Text(declaracion =>
            {
                declaracion.DefaultTextStyle(estilo => estilo.FontSize(12).LineHeight(1.6f));
                declaracion.AlignLeft();
                declaracion.Span("La Dirección del establecimiento certifica que ");

                declaracion.Span(_certificado.AlumnoNombre.ToUpperInvariant()).Bold();
                declaracion.Span($", {_certificado.AlumnoDocumento}, ");

                if (_certificado.MatriculaFutura)
                {
                    declaracion.Span("se encuentra matriculado(a) en este establecimiento para cursar ");
                    declaracion.Span(_certificado.Curso).Bold();
                    declaracion.Span($" durante el año académico {_certificado.Anio}.");
                }
                else
                {
                    declaracion.Span("es alumno(a) regular de este establecimiento y cursa ");
                    declaracion.Span(_certificado.Curso).Bold();
                    declaracion.Span($" durante el año académico {_certificado.Anio}.");
                }
            });

            columna.Item().PaddingTop(18)
                .Text("Se extiende el presente certificado a solicitud del apoderado(a), para los fines que estime conveniente.")
                .FontSize(12).LineHeight(1.6f).AlignLeft();

            columna.Item().PaddingTop(22)
                .Text(_certificado.LugarFecha).FontSize(12).AlignLeft();

            // El papel declara su propio plazo: quien lo recibe sabe hasta cuándo
            // puede comprobarlo en el portal.
            if (!string.IsNullOrWhiteSpace(_certificado.Vigencia))
            {
                columna.Item().PaddingTop(4)
                    .Text(_certificado.Vigencia).FontSize(10).FontColor(Gris).AlignLeft();
            }
        });
    }

    /// <summary>
    /// Membrete: identifica al establecimiento emisor con los datos que lo
    /// hacen verificable (RBD y reconocimiento oficial).
    /// </summary>
    private void Membrete(IContainer contenedor)
    {
        // Escudo arriba a la izquierda y los datos del establecimiento a su
        // lado: deja la franja superior limpia y el ancho libre a la derecha.
        contenedor.Row(fila =>
        {
            if (_certificado.Logo is { Length: > 0 })
            {
                fila.ConstantItem(64).AlignMiddle().Height(54)
                    .Image(_certificado.Logo).FitHeight();
            }

            fila.RelativeItem().AlignMiddle().Column(columna =>
            {
                columna.Item().Text(_certificado.Sostenedor.ToUpperInvariant())
                    .FontSize(8.5f).FontColor(Gris).LetterSpacing(0.15f);

                columna.Item().PaddingTop(1).Text(_certificado.Establecimiento)
                    .FontSize(15).Bold();

                columna.Item().Text(_certificado.Reconocimiento)
                    .FontSize(9).Italic().FontColor(Gris);

                columna.Item().PaddingTop(2)
                    .Text($"{_certificado.Direccion} · RBD {_certificado.Rbd}")
                    .FontSize(9).FontColor(Gris);
            });
        });
    }

    private void Pie(IContainer contenedor)
    {
        contenedor.Column(pie =>
        {
            pie.Item().PaddingTop(10).LineHorizontal(0.6f).LineColor(GrisClaro);

            pie.Item().PaddingTop(14).Row(fila =>
            {
                // La firma ocupa el bloque central de la hoja; el resto de la
                // línea queda libre para el timbre manual.
                fila.RelativeItem(3).Column(columna =>
                {
                    columna.Item().AlignCenter().Width(232).LineHorizontal(0.9f).LineColor(Gris);
                    columna.Item().PaddingTop(5).AlignCenter()
                        .Text(_certificado.FirmanteNombre).FontSize(12.5f).SemiBold();
                    columna.Item().AlignCenter()
                        .Text(_certificado.FirmanteCargo).FontSize(10.5f).FontColor(Gris);
                });

                fila.RelativeItem(1).Column(columna =>
                {
                    columna.Item().AlignCenter().Width(84).Height(84).Image(_certificado.Qr);
                    columna.Item().PaddingTop(4).AlignCenter()
                        .Text("Verifique este certificado").FontSize(8).FontColor(Gris);
                    columna.Item().AlignCenter()
                        .Text($"Cód. {_certificado.Codigo}").FontSize(8).SemiBold().FontColor(Gris);
                });
            });
        });
    }
}
