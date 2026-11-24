using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MarcelaPaz.Infrastructure.Documentos;

/// <summary>
/// Ficha de salud del funcionario con el mismo contenido del formulario del
/// colegio: identificación, contactos de emergencia, previsión y centro
/// médico, antecedentes y decisión sobre transfusión. Se genera desde los
/// datos registrados en el portal, sin archivos adjuntos.
/// </summary>
public sealed class FichaSaludFuncionarioPdf
{
    private static readonly string Azul = Colors.Blue.Darken3;
    private static readonly string Gris = Colors.Grey.Darken2;
    private static readonly string GrisClaro = Colors.Grey.Lighten2;
    private static readonly string GrisFondo = Colors.Grey.Lighten4;

    public byte[] Generar(DocumentoFichaSalud ficha) =>
        Document.Create(contenedor => Compose(contenedor, ficha)).GeneratePdf();

    private static void Compose(IDocumentContainer contenedor, DocumentoFichaSalud ficha)
    {
        contenedor.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.MarginLeft(44);
            pagina.MarginRight(44);
            pagina.MarginTop(36);
            pagina.MarginBottom(34);
            pagina.DefaultTextStyle(estilo => estilo.FontSize(9.5f).LineHeight(1.35f));

            pagina.Header().Element(encabezado => Membrete(encabezado, ficha));
            pagina.Content().Element(cuerpo => Cuerpo(cuerpo, ficha));
            pagina.Footer().Element(pie => Pie(pie, ficha));
        });
    }

    private static void Membrete(IContainer contenedor, DocumentoFichaSalud ficha)
    {
        contenedor.Row(fila =>
        {
            if (ficha.Logo is { Length: > 0 })
            {
                fila.ConstantItem(52).Height(44).AlignMiddle()
                    .Image(ficha.Logo).FitHeight();
            }

            fila.RelativeItem().PaddingLeft(ficha.Logo is { Length: > 0 } ? 10 : 0)
                .AlignMiddle().Column(columna =>
                {
                    columna.Item().Text(ficha.Sostenedor.ToUpperInvariant())
                        .FontSize(8).FontColor(Gris).LetterSpacing(0.12f);
                    columna.Item().Text(ficha.Establecimiento).FontSize(13).Bold();
                    columna.Item().Text(ficha.DireccionEstablecimiento)
                        .FontSize(8.5f).FontColor(Gris);
                });
        });
    }

    private static void Cuerpo(IContainer contenedor, DocumentoFichaSalud ficha)
    {
        contenedor.PaddingTop(10).Column(columna =>
        {
            columna.Spacing(6);

            columna.Item().PaddingTop(2).LineHorizontal(2).LineColor(Azul);
            columna.Item().PaddingTop(6).LineHorizontal(0.6f).LineColor(GrisClaro);

            columna.Item().PaddingTop(10).AlignCenter()
                .Text("HISTORIAL MÉDICO Y CONTACTOS DE EMERGENCIA")
                .FontSize(14).Bold().FontColor(Azul).LetterSpacing(0.06f);

            columna.Item().PaddingTop(4).Text(
                "Siendo la salud y la vida de todo el personal de la escuela un elemento central, " +
                "solicitamos la información personal referida a su estado de salud y a la forma en " +
                "que desea que se atienda una emergencia. La información se mantendrá bajo estricta " +
                "confidencialidad y se tratará según la normativa de protección de datos personales.")
                .FontSize(8.8f).LineHeight(1.35f).FontColor(Gris);

            columna.Item().PaddingTop(8).Element(c => Titulo(c, "Identificación"));
            columna.Item().Element(c => Tabla(c,
                ("Nombre", ficha.FuncionarioNombre),
                ("RUN / pasaporte", Documento(ficha)),
                ("Teléfonos", ficha.TelefonoPersonal),
                ("Dirección personal", ficha.DireccionPersonal)));

            columna.Item().PaddingTop(8).Element(c => Titulo(c, "En caso de emergencia médica, la persona de contacto será"));
            columna.Item().Element(c => Tabla(c,
                ("1° persona de contacto", ficha.Contacto1Nombre),
                ("Teléfono", ficha.Contacto1Telefono),
                ("2° persona de contacto", ficha.Contacto2Nombre),
                ("Teléfono", ficha.Contacto2Telefono)));

            columna.Item().PaddingTop(8).Element(c => Titulo(c, "Sistema de salud y centro médico"));
            columna.Item().Element(c => Tabla(c,
                ("Sistema de salud", ficha.SistemaSalud),
                ("Centro médico preferente", ficha.CentroMedicoPreferente),
                ("Ubicación", ficha.CentroUbicacion),
                ("Teléfono", ficha.CentroTelefono),
                ("Ambulancia en convenio", ficha.AmbulanciaConvenio),
                ("Teléfono", ficha.AmbulanciaTelefono),
                ("Seguro complementario", ficha.SeguroComplementario),
                ("Otros", ficha.Otros)));

            columna.Item().PaddingTop(10).Text(
                "En la eventualidad de una emergencia médica, se intentará trasladar al funcionario " +
                "al centro médico de su elección; si ello no es posible o pusiera en riesgo su vida, " +
                "será trasladado al hospital más cercano, aplicando las leyes N° 19.560, 19.966 y 19.937 " +
                "(leyes de Atención de Urgencia).")
                .FontSize(8.6f).LineHeight(1.35f).FontColor(Gris);

            columna.Item().PaddingTop(10).Element(c => Titulo(c, "Antecedentes de salud"));
            columna.Item().Element(c => Tabla(c,
                ("Alergias / alergia a medicamento", ficha.AlergiasMedicamentos),
                ("Enfermedades crónicas", ficha.EnfermedadesCronicas),
                ("Medicamentos de uso frecuente", ficha.MedicamentosFrecuentes)));

            columna.Item().PaddingTop(10).Element(c => Titulo(c, "Transfusión de sangre"));
            columna.Item().Text(texto =>
            {
                texto.Span("Debido a consideraciones religiosas o de otra índole, ¿acepta usted transfusiones de sangre?  ").FontSize(9);
                texto.Span(Transfusion(ficha.AceptaTransfusion)).Bold().FontSize(9.5f);
            });

            if (!string.IsNullOrWhiteSpace(ficha.MotivoTransfusion))
            {
                columna.Item().PaddingTop(3)
                    .Text($"Motivo: {ficha.MotivoTransfusion}").FontSize(8.8f).FontColor(Gris);
            }

            columna.Item().PaddingTop(8).Text(
                "Esta información eventualmente podría ser compartida con profesionales de la salud para " +
                "atender una situación de emergencia. Esta autorización podrá ser revocada o modificada en " +
                "cualquier momento, sin expresión de causa. La información proporcionada no altera ni " +
                "reemplaza las obligaciones y procedimientos de la ley N° 16.744 sobre accidentes laborales " +
                "y enfermedades profesionales.")
                .FontSize(8.4f).LineHeight(1.35f).FontColor(Gris);

            columna.Item().PaddingTop(26).AlignCenter().Width(250)
                .Column(firma =>
                {
                    firma.Item().LineHorizontal(0.8f).LineColor(Gris);
                    firma.Item().PaddingTop(4).AlignCenter().Text("FIRMA").FontSize(8.5f).FontColor(Gris);
                    firma.Item().PaddingTop(2).AlignCenter()
                        .Text(ficha.FuncionarioNombre).FontSize(9.5f).SemiBold();
                });
        });
    }

    private static void Pie(IContainer contenedor, DocumentoFichaSalud ficha)
    {
        contenedor.Column(pie =>
        {
            pie.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor(GrisClaro);
            pie.Item().PaddingTop(4).Row(fila =>
            {
                fila.RelativeItem().Text(
                    $"Ficha registrada el {ficha.RegistradoEn.ToLocalTime():dd-MM-yyyy HH:mm} por {ficha.RegistradoPor}.")
                    .FontSize(7.8f).FontColor(Gris);

                fila.ConstantItem(120).AlignRight()
                    .Text("Documento confidencial").FontSize(7.8f).FontColor(Gris);
            });
        });
    }

    private static void Titulo(IContainer contenedor, string titulo) =>
        contenedor.PaddingBottom(3).Text(titulo).FontSize(9.5f).Bold().FontColor(Azul);

    private static void Tabla(IContainer contenedor, params (string Etiqueta, string? Valor)[] filas)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.ConstantColumn(165);
                columnas.RelativeColumn();
            });

            foreach (var (etiqueta, valor) in filas)
            {
                tabla.Cell().Element(CeldaEtiqueta).Text(etiqueta).FontSize(8.8f).SemiBold();
                tabla.Cell().Element(CeldaValor)
                    .Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor.Trim())
                    .FontSize(8.8f);
            }
        });
    }

    private static IContainer CeldaEtiqueta(IContainer contenedor) =>
        contenedor.Border(0.5f).BorderColor(GrisClaro)
            .Background(GrisFondo).PaddingVertical(3.5f).PaddingHorizontal(6);

    private static IContainer CeldaValor(IContainer contenedor) =>
        contenedor.Border(0.5f).BorderColor(GrisClaro)
            .PaddingVertical(3.5f).PaddingHorizontal(6);

    private static string Documento(DocumentoFichaSalud ficha)
    {
        if (!string.IsNullOrWhiteSpace(ficha.Rut))
        {
            return $"RUN {ficha.Rut}";
        }

        return string.IsNullOrWhiteSpace(ficha.Pasaporte)
            ? "—"
            : $"Pasaporte {ficha.Pasaporte}";
    }

    private static string Transfusion(string? respuesta) => respuesta?.Trim().ToLowerInvariant() switch
    {
        "si" => "SÍ",
        "no" => "NO",
        _ => "SIN RESPUESTA",
    };
}
