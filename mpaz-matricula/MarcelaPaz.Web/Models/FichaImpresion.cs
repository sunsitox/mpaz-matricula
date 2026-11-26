using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Infrastructure.Documentos;
using System.Globalization;

namespace MarcelaPaz.Web.Models;

/// <summary>
/// Traduce el borrador del wizard (claves «sección: campo») al contenido que
/// imprime <see cref="FichaMatriculaPdf"/>. No decide nada del trámite: sólo
/// ordena lo que el funcionario ya capturó.
/// </summary>
public static class FichaImpresion
{
    // Mismo orden en que aparecen los bloques en el formulario.
    private static readonly (string Titulo, string[] Claves)[] Bloques =
    {
        ("Curso y año", new[] { "curso: nivel", "curso: letra", "año escolar" }),
        ("Estudiante", new[]
        {
            "nombres", "apellido paterno", "apellido materno", "cédula de identidad",
            "fecha nacimiento", "nacionalidad", "domicilio", "comuna",
        }),
        ("Madre", new[]
        {
            "madre: nombre apellido", "madre: cédula identidad", "madre: teléfono",
            "madre: escolaridad", "madre: domicilio", "madre: correo", "restricción madre (SI/NO)",
            "restricción madre: tipo", "restricción madre: detalle",
        }),
        ("Padre", new[]
        {
            "padre: nombre apellido", "padre: cédula identidad", "padre: teléfono",
            "padre: escolaridad", "padre: domicilio", "padre: correo", "restricción padre (SI/NO)",
            "restricción padre: tipo", "restricción padre: detalle",
        }),
        ("Apoderado principal", new[]
        {
            "apoderado principal: nombre apellido", "apoderado principal: cédula identidad",
            "apoderado principal: teléfono", "apoderado principal: vínculo", "apoderado principal: correo",
        }),
        ("Apoderado secundario", new[]
        {
            "apoderado secundario: nombre apellido", "apoderado secundario: cédula identidad",
            "apoderado secundario: teléfono", "apoderado secundario: vínculo", "apoderado secundario: correo",
        }),
        ("Contactos de emergencia", new[]
        {
            "contacto emergencia 01: identificación", "contacto emergencia 01: teléfono", "contacto emergencia 01: vínculo",
            "contacto emergencia 02: identificación", "contacto emergencia 02: teléfono", "contacto emergencia 02: vínculo",
        }),
        ("Personas autorizadas para el retiro", new[]
        {
            "retiro autorizado 01: rut", "retiro autorizado 01: vínculo",
            "retiro autorizado 02: rut", "retiro autorizado 02: vínculo",
            "retiro autorizado 03: rut", "retiro autorizado 03: vínculo",
        }),
        ("Autorizaciones", new[]
        {
            "salida solo al finalizar jornada (SI/NO)", "salida al baño (SI/NO)",
        }),
        ("Antecedentes médicos", new[]
        {
            "situación médica (SI/NO)",
            "alergias",
            "contacto sanitario: nombre", "contacto sanitario: teléfono",
            "situación médica 01: identificación", "situación médica 01: detalle", "situación médica 01: en tratamiento",
            "situación médica 02: identificación", "situación médica 02: detalle", "situación médica 02: en tratamiento",
            "situación médica 03: identificación", "situación médica 03: detalle", "situación médica 03: en tratamiento",
        }),
        ("PIE y apoyos", new[]
        {
            "PIE: participa (SI/NO/PENDIENTE)", "PIE: diagnosticado (SI/NO)", "PIE: apoyos o diagnóstico",
        }),
    };

    /// <summary>Textos de las autorizaciones mientras Dirección aprueba los definitivos.</summary>
    public const string TextoParacetamol =
        "Autorizo que se administre paracetamol a mi hijo/a en el establecimiento, únicamente con "
        + "indicación médica y conforme al protocolo TENS aprobado, con indicación y supervigilancia médica "
        + "(Código Sanitario). Esta respuesta no constituye una orden médica "
        + "ni habilita su administración sin esos requisitos.";

    public const string TextoImagen =
        "Autorizo el registro y uso de imágenes de mi hijo/a con fines institucionales "
        + "(registro interno, sitio web y redes sociales del establecimiento), sin exponer datos sensibles. "
        + "Puedo revocar esta autorización por escrito en cualquier momento.";

    public const string TextoConvivencia =
        "Autorizo que el equipo de Convivencia Escolar acompañe e intervenga con mi hijo/a cuando sea "
        + "necesario, con comunicación previa a la familia y posibilidad de revocar esta autorización por escrito.";

    private const string ClaveImagen = "autorización de uso de imagen (SI/NO)";

    private const string ClaveConvivencia = "autorización de convivencia escolar (SI/NO)";

    /// <summary>
    /// Consentimientos que se imprimen con la ficha. La casilla marcada sale de la
    /// respuesta del formulario; los textos quedan pendientes de la revisión de
    /// Dirección y Convivencia.
    /// </summary>
    private static IReadOnlyList<AutorizacionFicha> AutorizacionesDe(IReadOnlyDictionary<string, string> datos) =>
    [
        new AutorizacionFicha
        {
            Titulo = "Autorización de paracetamol",
            Texto = TextoParacetamol,
            Respuesta = RespuestaSiNo(datos, "autorización de paracetamol (SI/NO)"),
        },
        new AutorizacionFicha
        {
            Titulo = "Autorización de uso de imagen",
            Texto = TextoImagen,
            Respuesta = RespuestaSiNo(datos, ClaveImagen),
        },
        new AutorizacionFicha
        {
            Titulo = "Autorización para el trabajo con Convivencia Escolar",
            Texto = TextoConvivencia,
            Respuesta = RespuestaSiNo(datos, ClaveConvivencia),
        },
    ];

    /// <summary>«Si» → true, «No» → false; sin respuesta → null (casilla sin marcar).</summary>
    private static bool? RespuestaSiNo(IReadOnlyDictionary<string, string> datos, string clave)
    {
        var valor = datos.GetValueOrDefault(clave)?.Trim();

        if (string.IsNullOrEmpty(valor))
        {
            return null;
        }

        return valor.StartsWith('S') || valor.StartsWith('s') ? true
             : valor.StartsWith('N') || valor.StartsWith('n') ? false
             : null;
    }

    /// <summary>Etiquetas propias para claves cuyo nombre técnico no se lee bien.</summary>
    private static readonly Dictionary<string, string> EtiquetasPropias = new()
    {
        ["PIE: participa (SI/NO/PENDIENTE)"] = "Participa PIE",
        ["PIE: diagnosticado (SI/NO)"] = "Diagnóstico PIE",
        ["PIE: apoyos o diagnóstico"] = "Apoyos o diagnóstico",
        ["salida solo al finalizar jornada (SI/NO)"] = "Salida solo",
        ["restricción madre: tipo"] = "Restricción: tipo",
        ["restricción madre: detalle"] = "Restricción: detalle",
        ["restricción padre: tipo"] = "Restricción: tipo",
        ["restricción padre: detalle"] = "Restricción: detalle",
        ["alergias"] = "Alergias",
        ["contacto sanitario: nombre"] = "Contacto sanitario",
        ["contacto sanitario: teléfono"] = "Teléfono del contacto sanitario",
    };

    public static DocumentoFicha DesdeBorrador(
        IReadOnlyDictionary<string, string> datos,
        int anio,
        DateTime generadoEn,
        byte[]? logo = null,
        string version = "",
        string? folio = null)
    {
        var secciones = new List<SeccionFicha>();

        // Apartados condicionales: si la respuesta es «No», el detalle no se
        // imprime; si es «Sí», aparece. Lo no respondido tampoco aparece.
        var sinRestriccionMadre = EsNo(datos, "restricción madre (SI/NO)");
        var sinRestriccionPadre = EsNo(datos, "restricción padre (SI/NO)");
        var sinSituacionMedica = EsNo(datos, "situación médica (SI/NO)");
        var pieNoParticipa = EsNo(datos, "PIE: participa (SI/NO/PENDIENTE)");

        foreach (var (titulo, claves) in Bloques)
        {
            var campos = claves
                .Where(clave => Visible(clave, datos, sinRestriccionMadre, sinRestriccionPadre, sinSituacionMedica, pieNoParticipa))
                .Select(clave => new CampoFicha
                {
                    Etiqueta = Etiqueta(clave),
                    // Los RUN se imprimen con puntos, aunque la base los guarde sin ellos.
                    Valor = ClavesRut.EsClave(clave) ? RutChileno.Formatear(datos[clave]) : datos[clave].Trim(),
                })
                .ToList();

            if (campos.Count > 0)
            {
                secciones.Add(new SeccionFicha { Titulo = titulo, Campos = campos });
            }
        }

        return new DocumentoFicha
        {
            // Sin folio registrado el documento sigue siendo un borrador: no se
            // inventa un número que después no exista en la base.
            Folio = folio ?? string.Empty,
            Anio = anio,
            TipoTramite = datos.GetValueOrDefault("modo") ?? "Matrícula",
            Version = version,
            GeneradoEn = generadoEn,
            Detalle = datos.GetValueOrDefault("modo: detalle"),
            Logo = logo,
            Secciones = secciones,
            Autorizaciones = AutorizacionesDe(datos),
        };
    }

    /// <summary>
    /// Decide si una clave del borrador se imprime: con respuesta «No» el
    /// detalle no aparece; sin respuesta tampoco; «Ninguna» en alergias
    /// equivale a sin respuesta.
    /// </summary>
    private static bool Visible(
        string clave,
        IReadOnlyDictionary<string, string> datos,
        bool sinRestriccionMadre,
        bool sinRestriccionPadre,
        bool sinSituacionMedica,
        bool pieNoParticipa)
    {
        var valor = datos.GetValueOrDefault(clave);

        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        if (clave == "alergias" && EsNegacion(valor))
        {
            return false;
        }

        if (sinRestriccionMadre && clave.StartsWith("restricción madre:", StringComparison.Ordinal))
        {
            return false;
        }

        if (sinRestriccionPadre && clave.StartsWith("restricción padre:", StringComparison.Ordinal))
        {
            return false;
        }

        if (sinSituacionMedica && clave.StartsWith("situación médica 0", StringComparison.Ordinal))
        {
            return false;
        }

        if (pieNoParticipa && (clave == "PIE: diagnosticado (SI/NO)" || clave == "PIE: apoyos o diagnóstico"))
        {
            return false;
        }

        return true;
    }

    private static bool EsNo(IReadOnlyDictionary<string, string> datos, string clave) =>
        datos.GetValueOrDefault(clave)?.Trim().StartsWith('N') == true
        || datos.GetValueOrDefault(clave)?.Trim().StartsWith('n') == true;

    /// <summary>«Ninguna», «ninguno», «no», «sin alergias»: no hay nada que imprimir.</summary>
    private static bool EsNegacion(string valor)
    {
        var limpio = valor.Trim().Trim('«', '»', '"', '.', ' ').ToLowerInvariant();

        return limpio is "no" or "ninguna" or "ninguno"
            || limpio.StartsWith("ningun", StringComparison.Ordinal)
            || limpio.StartsWith("sin alerg", StringComparison.Ordinal);
    }

    /// <summary>«madre: cédula identidad» → «Cédula identidad».</summary>
    private static string Etiqueta(string clave)
    {
        if (EtiquetasPropias.TryGetValue(clave, out var propia))
        {
            return propia;
        }

        var texto = clave.Contains(':') ? clave[(clave.IndexOf(':') + 1)..] : clave;
        texto = texto.Replace(" (SI/NO)", string.Empty).Trim();

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(texto);
    }

}
