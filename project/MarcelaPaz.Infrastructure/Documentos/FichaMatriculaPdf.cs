using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MarcelaPaz.Infrastructure.Documentos;

/// <summary>
/// Ficha de matrícula imprimible (requisito MAT-08: PDF fiel al formulario con
/// folio, año, versión y campos de firma). Recibe el contenido ya armado y no
/// consulta la base de datos.
///
/// La ficha está pensada para imprimirse en una sola hoja A4: el encabezado
/// institucional, los datos en dos columnas y los bloques de firma al pie.
/// </summary>
public sealed class FichaMatriculaPdf
{
    private const string Azul = "#0C255B";
    private const string Dorado = "#D7A62C";
    private const string Gris = "#52606D";
    private const string GrisClaro = "#7B8794";
    private const string Borde = "#C9D1DC";
    private const string FondoSeccion = "#EEF1F7";
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
                pagina.Margin(0.9f, Unit.Centimetre);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(7.6f).FontColor(Texto));

                pagina.Header().Element(contenedor => Encabezado(contenedor, ficha));
                pagina.Content().Element(contenedor => Contenido(contenedor, ficha));

                // Las firmas viven en el pie: siempre quedan al final de la hoja
                // y el cuerpo se estira para ocupar el espacio disponible.
                pagina.Footer().Element(contenedor => Pie(contenedor, ficha));
            });
        }).GeneratePdf();
    }

    // ── Encabezado institucional ─────────────────────────────────────────────

    private static void Encabezado(IContainer contenedor, DocumentoFicha ficha)
    {
        contenedor.Column(columna =>
        {
            columna.Item().Row(fila =>
            {
                if (ficha.Logo is { Length: > 0 })
                {
                    fila.ConstantItem(21, Unit.Millimetre).Image(ficha.Logo).FitArea();
                }

                fila.RelativeItem().PaddingLeft(8).AlignMiddle().Column(titulo =>
                {
                    titulo.Item()
                        .Text("ESCUELA ESCRITORA MARCELA PAZ")
                        .FontSize(12).Bold().FontColor(Azul);
                    titulo.Item()
                        .Text("San Bernardo · Región Metropolitana")
                        .FontSize(7).FontColor(Gris);
                    titulo.Item()
                        .PaddingTop(1)
                        .Text("Ficha de matrícula del estudiante")
                        .FontSize(7.5f).SemiBold().FontColor(Dorado);
                });

                fila.ConstantItem(46, Unit.Millimetre).AlignMiddle().Border(0.7f)
                    .BorderColor(Azul).Padding(4).Column(identificacion =>
                    {
                        identificacion.Item()
                            .Text("FOLIO").FontSize(6).SemiBold().FontColor(Gris);
                        identificacion.Item()
                            .Text(ficha.Folio).FontSize(8.5f).Bold().FontColor(Azul);
                        identificacion.Item()
                            .PaddingTop(2)
                            .Text($"Año escolar {ficha.Anio}").FontSize(6.5f).FontColor(Gris);
                        identificacion.Item()
                            .Text($"Versión {ficha.Version}").FontSize(6.5f).FontColor(Gris);
                    });
            });

            columna.Item().PaddingTop(4).LineHorizontal(1.6f).LineColor(Dorado);

            // Banda del título del documento.
            columna.Item().PaddingTop(4).Background(Azul).PaddingVertical(4).PaddingHorizontal(8)
                .Row(banda =>
                {
                    banda.RelativeItem()
                        .Text($"FICHA DE MATRÍCULA · AÑO ESCOLAR {ficha.Anio}")
                        .FontSize(9.5f).Bold().FontColor(Colors.White);

                    banda.ConstantItem(70, Unit.Millimetre).AlignRight()
                        .Text($"Trámite: {ficha.TipoTramite}")
                        .FontSize(7.5f).FontColor(Colors.White);
                });

            columna.Item().PaddingTop(3).Row(meta =>
            {
                meta.RelativeItem()
                    .Text($"Generado el {ficha.GeneradoEn:dd-MM-yyyy} a las {ficha.GeneradoEn:HH:mm}")
                    .FontSize(6.5f).FontColor(GrisClaro);

                meta.RelativeItem().AlignRight()
                    .Text("Documento para revisión y firma del apoderado")
                    .FontSize(6.5f).FontColor(GrisClaro);
            });

            if (!string.IsNullOrWhiteSpace(ficha.Detalle))
            {
                columna.Item().PaddingTop(1).Text(ficha.Detalle).FontSize(6.5f).FontColor(Gris);
            }

            columna.Item().PaddingTop(5);
        });
    }

    // ── Cuerpo en dos columnas ───────────────────────────────────────────────

    private static void Contenido(IContainer contenedor, DocumentoFicha ficha)
    {
        var total = ficha.Secciones.Sum(seccion => seccion.Campos.Count);
        var izquierda = new List<SeccionFicha>();
        var derecha = new List<SeccionFicha>();
        var acumulado = 0;

        // Se reparten los bloques por cantidad de campos para equilibrar columnas.
        foreach (var seccion in ficha.Secciones)
        {
            if (acumulado < total / 2)
            {
                izquierda.Add(seccion);
                acumulado += seccion.Campos.Count;
            }
            else
            {
                derecha.Add(seccion);
            }
        }

        contenedor.Column(columna =>
        {
            // El marco se estira hasta el pie para que la ficha ocupe la hoja
            // completa y las firmas queden siempre al final de la página.
            columna.Item().Extend().Border(0.8f).BorderColor(Borde).Padding(6).Column(marco =>
            {
                marco.Item().Row(fila =>
                {
                    fila.RelativeItem().Column(izq =>
                    {
                        foreach (var seccion in izquierda)
                        {
                            izq.Item().Element(bloque => Seccion(bloque, seccion));
                        }
                    });

                    fila.ConstantItem(8);

                    fila.RelativeItem().Column(der =>
                    {
                        foreach (var seccion in derecha)
                        {
                            der.Item().Element(bloque => Seccion(bloque, seccion));
                        }
                    });
                });
            });
        });
    }

    private static void Seccion(IContainer contenedor, SeccionFicha seccion)
    {
        contenedor.PaddingBottom(4).Column(columna =>
        {
            columna.Item()
                .Background(FondoSeccion)
                .BorderLeft(1.6f)
                .BorderColor(Azul)
                .PaddingVertical(2.4f)
                .PaddingHorizontal(5)
                .Text(seccion.Titulo.ToUpperInvariant())
                .FontSize(7.8f)
                .Bold()
                .FontColor(Azul);

            columna.Item().Border(0.5f).BorderColor(Borde).Table(tabla =>
            {
                tabla.ColumnsDefinition(columnas =>
                {
                    columnas.ConstantColumn(24, Unit.Millimetre);
                    columnas.RelativeColumn();
                });

                foreach (var campo in seccion.Campos)
                {
                    tabla.Cell()
                        .PaddingVertical(2)
                        .PaddingHorizontal(4)
                        .Background("#F7F9FC")
                        .Text(campo.Etiqueta)
                        .FontSize(6.6f)
                        .SemiBold()
                        .FontColor(Gris);

                    tabla.Cell()
                        .PaddingVertical(2)
                        .PaddingHorizontal(4)
                        .Text(campo.Valor)
                        .FontSize(8.2f)
                        .FontColor(Texto);
                }
            });
        });
    }

    // ── Firmas y declaración ────────────────────────────────────────────────

    private static void Firmas(IContainer contenedor)
    {
        contenedor.Column(columna =>
        {
            columna.Item()
                .Text("Declaro que los datos registrados en esta ficha son fieles a la información entregada al establecimiento.")
                .FontSize(6.8f)
                .FontColor(Gris);

            columna.Item().PaddingTop(4).Row(fila =>
            {
                fila.RelativeItem().Element(bloque =>
                    BloqueFirma(bloque, "Apoderado/a", "Nombre completo, RUN y firma"));
                fila.ConstantItem(8);
                fila.RelativeItem().Element(bloque =>
                    BloqueFirma(bloque, "Funcionario/a que recepciona", "Nombre, RUN, firma y timbre"));
                fila.ConstantItem(8);
                fila.RelativeItem().Element(bloque =>
                    BloqueFirma(bloque, "Timbre del establecimiento", "Secretaría / Dirección"));
            });

            columna.Item()
                .PaddingTop(4)
                .Text("Este documento no acredita matrícula formalizada hasta su firma y registro en Secretaría.")
                .FontSize(6)
                .FontColor(GrisClaro);
        });
    }

    private static void BloqueFirma(IContainer contenedor, string titulo, string ayuda)
    {
        contenedor.Border(0.6f).BorderColor(Borde).Padding(5).Column(columna =>
        {
            columna.Item().Text(titulo).FontSize(7).SemiBold().FontColor(Azul);
            columna.Item().PaddingTop(20).LineHorizontal(0.7f).LineColor(Gris);
            columna.Item().PaddingTop(2).Text(ayuda).FontSize(6).FontColor(GrisClaro);
            columna.Item().PaddingTop(10).Text("Fecha:").FontSize(6).FontColor(GrisClaro);
        });
    }

    private static void Pie(IContainer contenedor, DocumentoFicha ficha)
    {
        contenedor.Column(columna =>
        {
            columna.Item().BorderTop(0.8f).BorderColor(Azul).PaddingTop(5).Element(Firmas);

            columna.Item().PaddingTop(4).Row(fila =>
            {
                fila.RelativeItem()
                    .Text($"Escuela Escritora Marcela Paz · Ficha de matrícula {ficha.Anio} · Folio {ficha.Folio}")
                    .FontSize(6)
                    .FontColor(GrisClaro);

                fila.ConstantItem(90).AlignRight().Text(texto =>
                {
                    texto.DefaultTextStyle(estilo => estilo.FontSize(6).FontColor(GrisClaro));
                    texto.Span("Página ");
                    texto.CurrentPageNumber();
                    texto.Span(" de ");
                    texto.TotalPages();
                });
            });
        });
    }
}
