using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MarcelaPaz.Infrastructure.Documentos;

/// <summary>
/// Ficha de matrícula imprimible (requisito MAT-08: PDF fiel al formulario con
/// folio, año, versión y campos de firma). Recibe el contenido ya armado y no
/// consulta la base de datos.
/// </summary>
public sealed class FichaMatriculaPdf
{
    private const string Azul = "#0C255B";
    private const string Dorado = "#D7A62C";
    private const string Gris = "#52606D";
    private const string Borde = "#C9D1DC";
    private const string Texto = "#1F2933";

    static FichaMatriculaPdf()
    {
        // Licencia gratuita para el uso interno del establecimiento.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generar(DocumentoFicha ficha)
    {
        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(1.6f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9).FontColor(Texto));

                pagina.Header().Element(contenedor => Encabezado(contenedor, ficha));
                pagina.Content().Element(contenedor => Contenido(contenedor, ficha));
                pagina.Footer().Element(PieDePagina);
            });
        }).GeneratePdf();
    }

    private static void Encabezado(IContainer contenedor, DocumentoFicha ficha)
    {
        contenedor.Column(columna =>
        {
            columna.Item().Row(fila =>
            {
                if (ficha.Logo is { Length: > 0 })
                {
                    // Escudo institucional con lema, en la esquina superior izquierda.
                    fila.ConstantItem(24, Unit.Millimetre).Image(ficha.Logo).FitArea();
                }

                fila.RelativeItem().PaddingLeft(10).AlignMiddle().Column(titulo =>
                {
                    titulo.Item().Text($"Ficha de matrícula {ficha.Anio}").FontSize(13).SemiBold().FontColor(Azul);
                    titulo.Item().Text("Escuela Escritora Marcela Paz · documento para revisión y firma").FontSize(8.5f).FontColor(Gris);
                });

                fila.ConstantItem(150).AlignMiddle().AlignRight().Column(identificacion =>
                {
                    identificacion.Item().Text($"Folio {ficha.Folio}").SemiBold();
                    identificacion.Item().Text($"Versión {ficha.Version}").FontSize(8).FontColor(Gris);
                });
            });

            columna.Item().PaddingTop(5).LineHorizontal(1.5f).LineColor(Dorado);

            columna.Item().PaddingTop(6).Table(tabla =>
            {
                tabla.ColumnsDefinition(columnas =>
                {
                    columnas.ConstantColumn(52);
                    columnas.RelativeColumn();
                    columnas.ConstantColumn(70);
                    columnas.RelativeColumn();
                    columnas.ConstantColumn(70);
                    columnas.RelativeColumn();
                });

                tabla.Cell().Text("Año").FontSize(8).SemiBold().FontColor(Gris);
                tabla.Cell().Text(ficha.Anio.ToString()).FontSize(8.5f);
                tabla.Cell().Text("Trámite").FontSize(8).SemiBold().FontColor(Gris);
                tabla.Cell().Text(ficha.TipoTramite).FontSize(8.5f);
                tabla.Cell().Text("Generado").FontSize(8).SemiBold().FontColor(Gris);
                tabla.Cell().Text(ficha.GeneradoEn.ToString("dd-MM-yyyy HH:mm")).FontSize(8.5f);
            });

            if (!string.IsNullOrWhiteSpace(ficha.Detalle))
            {
                columna.Item().PaddingTop(4).Text(ficha.Detalle).FontSize(8).FontColor(Gris);
            }
        });
    }

    private static void Contenido(IContainer contenedor, DocumentoFicha ficha)
    {
        contenedor.Column(columna =>
        {
            foreach (var seccion in ficha.Secciones)
            {
                columna.Item().PaddingTop(10).Element(bloque => Seccion(bloque, seccion));
            }

            columna.Item().PaddingTop(24).Element(Firmas);
        });
    }

    private static void Seccion(IContainer contenedor, SeccionFicha seccion)
    {
        contenedor.Column(columna =>
        {
            columna.Item()
                .Background(Azul)
                .PaddingVertical(3)
                .PaddingHorizontal(6)
                .Text(seccion.Titulo.ToUpperInvariant())
                .FontSize(8.5f)
                .SemiBold()
                .FontColor(Colors.White);

            columna.Item().Border(0.6f).BorderColor(Borde).Table(tabla =>
            {
                tabla.ColumnsDefinition(columnas =>
                {
                    columnas.ConstantColumn(105);
                    columnas.RelativeColumn();
                    columnas.ConstantColumn(105);
                    columnas.RelativeColumn();
                });

                for (var i = 0; i < seccion.Campos.Count; i += 2)
                {
                    var izquierda = seccion.Campos[i];
                    tabla.Cell().Padding(3).Text(izquierda.Etiqueta).FontSize(8).SemiBold().FontColor(Gris);
                    tabla.Cell().Padding(3).Text(izquierda.Valor).FontSize(8.5f);

                    if (i + 1 < seccion.Campos.Count)
                    {
                        var derecha = seccion.Campos[i + 1];
                        tabla.Cell().Padding(3).Text(derecha.Etiqueta).FontSize(8).SemiBold().FontColor(Gris);
                        tabla.Cell().Padding(3).Text(derecha.Valor).FontSize(8.5f);
                    }
                    else
                    {
                        tabla.Cell().Padding(3).Text(string.Empty);
                        tabla.Cell().Padding(3).Text(string.Empty);
                    }
                }
            });
        });
    }

    private static void Firmas(IContainer contenedor)
    {
        contenedor.Column(columna =>
        {
            columna.Item().Text("FIRMAS").FontSize(8.5f).SemiBold().FontColor(Azul);

            columna.Item().PaddingTop(4).Row(fila =>
            {
                fila.RelativeItem().Element(bloque =>
                    BloqueFirma(bloque, "Apoderado/a", "Nombre, RUN y firma"));
                fila.ConstantItem(16).Text(string.Empty);
                fila.RelativeItem().Element(bloque =>
                    BloqueFirma(bloque, "Funcionario/a que recepciona", "Nombre, RUN, firma y timbre"));
            });

            columna.Item()
                .PaddingTop(14)
                .Text("Documento generado desde el borrador del formulario. No acredita matrícula formalizada hasta su firma y registro en Secretaría.")
                .FontSize(7.5f)
                .FontColor(Gris);
        });
    }

    private static void BloqueFirma(IContainer contenedor, string titulo, string ayuda)
    {
        contenedor.Border(0.6f).BorderColor(Borde).Padding(8).Column(columna =>
        {
            columna.Item().Text(titulo).FontSize(8).SemiBold();
            columna.Item().PaddingTop(24).LineHorizontal(0.8f).LineColor(Gris);
            columna.Item().PaddingTop(2).Text(ayuda).FontSize(7).FontColor(Gris);
            columna.Item().PaddingTop(12).Text("Fecha:").FontSize(7).FontColor(Gris);
        });
    }

    private static void PieDePagina(IContainer contenedor)
    {
        contenedor.Row(fila =>
        {
            fila.RelativeItem()
                .Text("Portal de Matrícula · uso interno del establecimiento")
                .FontSize(7)
                .FontColor("#7B8794");

            fila.ConstantItem(90).AlignRight().Text(texto =>
            {
                texto.DefaultTextStyle(estilo => estilo.FontSize(7).FontColor("#7B8794"));
                texto.Span("Página ");
                texto.CurrentPageNumber();
                texto.Span(" de ");
                texto.TotalPages();
            });
        });
    }
}
