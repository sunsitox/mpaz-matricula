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
                pagina.DefaultTextStyle(estilo => estilo.FontSize(7.7f).FontColor(Texto));

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
                    fila.ConstantItem(18, Unit.Millimetre).Image(ficha.Logo).FitArea();
                }

                fila.RelativeItem().PaddingLeft(6).AlignMiddle().Column(titulo =>
                {
                    titulo.Item()
                        .Text("ESCUELA ESCRITORA MARCELA PAZ")
                        .FontSize(12).Bold().FontColor(Azul);
                    titulo.Item()
                        .Text("San Bernardo · Región Metropolitana")
                        .FontSize(6.4f).FontColor(Gris);
                    titulo.Item()
                        .Text("Ficha de matrícula del estudiante")
                        .FontSize(7).SemiBold().FontColor(Dorado);
                });

                fila.ConstantItem(44, Unit.Millimetre).AlignMiddle().Border(0.7f)
                    .BorderColor(Azul).Padding(3).Column(identificacion =>
                    {
                        identificacion.Item()
                            .Text("FOLIO").FontSize(5.6f).SemiBold().FontColor(Gris);
                        identificacion.Item()
                            .Text(ficha.Folio).FontSize(8).Bold().FontColor(Azul);
                        identificacion.Item()
                            .Text($"Año escolar {ficha.Anio}").FontSize(6).FontColor(Gris);
                        identificacion.Item()
                            .Text($"Versión {ficha.Version}").FontSize(6).FontColor(Gris);
                    });
            });

            columna.Item().PaddingTop(3).LineHorizontal(1.6f).LineColor(Dorado);

            // Banda del título del documento.
            columna.Item().PaddingTop(3).Background(Azul).PaddingVertical(3).PaddingHorizontal(7)
                .Row(banda =>
                {
                    banda.RelativeItem()
                        .Text($"FICHA DE MATRÍCULA · AÑO ESCOLAR {ficha.Anio}")
                        .FontSize(8.8f).Bold().FontColor(Colors.White);

                    banda.ConstantItem(70, Unit.Millimetre).AlignRight()
                        .Text($"Trámite: {ficha.TipoTramite}")
                        .FontSize(6.9f).FontColor(Colors.White);
                });

            columna.Item().PaddingTop(2).Row(meta =>
            {
                meta.RelativeItem()
                    .Text($"Generado el {ficha.GeneradoEn:dd-MM-yyyy} a las {ficha.GeneradoEn:HH:mm}")
                    .FontSize(6).FontColor(GrisClaro);

                meta.RelativeItem().AlignRight()
                    .Text("Documento para revisión y firma del apoderado")
                    .FontSize(6).FontColor(GrisClaro);
            });

            if (!string.IsNullOrWhiteSpace(ficha.Detalle))
            {
                columna.Item().Text(ficha.Detalle).FontSize(6).FontColor(Gris);
            }

            columna.Item().PaddingTop(4);
        });
    }

    // ── Cuerpo en dos columnas ───────────────────────────────────────────────

    private static void Contenido(IContainer contenedor, DocumentoFicha ficha)
    {
        // Se elige el corte que deja las dos columnas más parejas, estimando la
        // altura de cada bloque por sus campos y el largo de los valores (los
        // textos largos envuelven y ocupan más líneas).
        var pesos = ficha.Secciones
            .Select(seccion => 1.5
                + seccion.Campos.Sum(campo => 1 + campo.Valor.Length / 38.0 + campo.Etiqueta.Length / 60.0))
            .ToArray();

        var total = pesos.Sum();
        var corte = Math.Max(1, Math.Min(ficha.Secciones.Count - 1, 1));
        var mejor = double.MaxValue;
        var acumulado = 0.0;

        for (var k = 1; k < ficha.Secciones.Count; k++)
        {
            acumulado += pesos[k - 1];
            var diferencia = Math.Abs(acumulado - (total - acumulado));

            if (diferencia < mejor)
            {
                mejor = diferencia;
                corte = k;
            }
        }

        var izquierda = ficha.Secciones.Take(corte).ToList();
        var derecha = ficha.Secciones.Skip(corte).ToList();

        contenedor.Column(columna =>
        {
            // El marco se estira hasta el pie para que la ficha ocupe la hoja
            // completa y las firmas queden siempre al final de la página.
            // ScaleToFit reduce el contenido proporcionalmente sólo si no cabe en la
            // hoja: así la ficha completa (todos los bloques y autorizaciones) queda
            // siempre en una página A4, sin cortar datos.
            columna.Item().Border(0.8f).BorderColor(Borde).Padding(5).ScaleToFit().Column(marco =>
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

                if (ficha.Autorizaciones.Count > 0)
                {
                    marco.Item().PaddingTop(3).Row(fila =>
                    {
                        for (var i = 0; i < ficha.Autorizaciones.Count; i++)
                        {
                            if (i > 0)
                            {
                                fila.ConstantItem(6);
                            }

                            var autorizacion = ficha.Autorizaciones[i];

                            fila.RelativeItem().Column(texto =>
                            {
                                texto.Item().Row(titulo =>
                                {
                                    titulo.RelativeItem().Text(autorizacion.Titulo)
                                        .FontSize(6.7f).SemiBold().FontColor(Azul);

                                    titulo.AutoItem().Element(marca =>
                                        Opcion(marca, "Sí", autorizacion.Respuesta == true));
                                    titulo.ConstantItem(5);
                                    titulo.AutoItem().Element(marca =>
                                        Opcion(marca, "No", autorizacion.Respuesta == false));
                                });
                                texto.Item().Text(autorizacion.Texto).FontSize(6.3f).FontColor(Gris);
                            });
                        }
                    });

                    marco.Item().PaddingTop(1)
                        .Text("La firma del apoderado/a al pie de esta ficha respalda las autorizaciones marcadas.")
                        .FontSize(5.8f).FontColor(GrisClaro);
                }
            });
        });
    }

    /// <summary>Opción del checklist: texto y casilla que se marca con una X.</summary>
    private static void Opcion(IContainer contenedor, string texto, bool marcada)
    {
        contenedor.Row(fila =>
        {
            fila.AutoItem().AlignMiddle().Text(texto).FontSize(6.2f).FontColor(Texto);
            fila.ConstantItem(2);
            fila.ConstantItem(9).Height(9).Border(0.7f).BorderColor(Gris)
                .AlignCenter().AlignMiddle()
                .Text(marcada ? "X" : string.Empty)
                .FontSize(6).Bold().FontColor(Azul);
        });
    }

    private static void Seccion(IContainer contenedor, SeccionFicha seccion)
    {
        contenedor.PaddingBottom(4.2f).Column(columna =>
        {
            columna.Item()
                .Background(FondoSeccion)
                .BorderLeft(1.6f)
                .BorderColor(Azul)
                .PaddingVertical(2.3f)
                .PaddingHorizontal(4.5f)
                .Text(seccion.Titulo.ToUpperInvariant())
                .FontSize(7.9f)
                .Bold()
                .FontColor(Azul);

            columna.Item().Border(0.5f).BorderColor(Borde).Table(tabla =>
            {
                tabla.ColumnsDefinition(columnas =>
                {
                    columnas.ConstantColumn(26, Unit.Millimetre);
                    columnas.RelativeColumn();
                });

                foreach (var campo in seccion.Campos)
                {
                    tabla.Cell()
                        .PaddingVertical(2f)
                        .PaddingHorizontal(4)
                        .Background("#F7F9FC")
                        .Text(campo.Etiqueta)
                        .FontSize(6.7f)
                        .SemiBold()
                        .FontColor(Gris);

                    tabla.Cell()
                        .PaddingVertical(2f)
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
                .FontSize(6.2f)
                .FontColor(Gris);

            columna.Item().PaddingTop(3).Row(fila =>
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
                .PaddingTop(3)
                .Text("Este documento no acredita matrícula formalizada hasta su firma y registro en Secretaría.")
                .FontSize(5.8f)
                .FontColor(GrisClaro);
        });
    }

    private static void BloqueFirma(IContainer contenedor, string titulo, string ayuda)
    {
        contenedor.Border(0.6f).BorderColor(Borde).Padding(4).Column(columna =>
        {
            columna.Item().Text(titulo).FontSize(6.6f).SemiBold().FontColor(Azul);
            columna.Item().PaddingTop(14).LineHorizontal(0.7f).LineColor(Gris);
            columna.Item().PaddingTop(2).Text(ayuda).FontSize(5.8f).FontColor(GrisClaro);
            columna.Item().PaddingTop(8).Text("Fecha:").FontSize(5.8f).FontColor(GrisClaro);
        });
    }

    private static void Pie(IContainer contenedor, DocumentoFicha ficha)
    {
        contenedor.Column(columna =>
        {
            columna.Item().BorderTop(0.8f).BorderColor(Azul).PaddingTop(3).Element(Firmas);

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
