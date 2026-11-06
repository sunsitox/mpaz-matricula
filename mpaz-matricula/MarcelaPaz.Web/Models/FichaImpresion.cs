using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Infrastructure.Documentos;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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
        }),
        ("Padre", new[]
        {
            "padre: nombre apellido", "padre: cédula identidad", "padre: teléfono",
            "padre: escolaridad", "padre: domicilio", "padre: correo",
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
            "autorización de paracetamol (SI/NO)",
        }),
        ("Antecedentes médicos", new[]
        {
            "situación médica (SI/NO)",
            "situación médica 01: identificación", "situación médica 01: detalle", "situación médica 01: en tratamiento",
            "situación médica 02: identificación", "situación médica 02: detalle", "situación médica 02: en tratamiento",
            "situación médica 03: identificación", "situación médica 03: detalle", "situación médica 03: en tratamiento",
        }),
        ("PIE y apoyos", new[]
        {
            "PIE: participa (SI/NO/PENDIENTE)", "PIE: diagnosticado (SI/NO)", "PIE: apoyos o diagnóstico",
        }),
    };

    /// <summary>
    /// Consentimientos que se imprimen con la ficha para que el apoderado los marque
    /// y firme. Los textos quedan pendientes de la revisión de Dirección y Convivencia.
    /// </summary>
    private static readonly AutorizacionFicha[] AutorizacionesFormulario =
    {
        new AutorizacionFicha
        {
            Titulo = "Autorización de uso de imagen",
            Texto = "Autorizo el registro y uso de imágenes de mi hijo/a con fines institucionales "
                  + "(registro interno, sitio web y redes sociales del establecimiento), sin exponer datos sensibles. "
                  + "Puedo revocar esta autorización por escrito en cualquier momento.",
        },
        new AutorizacionFicha
        {
            Titulo = "Autorización para el trabajo con Convivencia Escolar",
            Texto = "Autorizo que el equipo de Convivencia Escolar acompañe e intervenga con mi hijo/a cuando sea "
                  + "necesario, con comunicación previa a la familia y posibilidad de revocar esta autorización por escrito.",
        },
    };

    /// <summary>Etiquetas propias para claves cuyo nombre técnico no se lee bien.</summary>
    private static readonly Dictionary<string, string> EtiquetasPropias = new()
    {
        ["PIE: participa (SI/NO/PENDIENTE)"] = "Participa PIE",
        ["PIE: diagnosticado (SI/NO)"] = "Diagnóstico PIE",
        ["PIE: apoyos o diagnóstico"] = "Apoyos o diagnóstico",
        ["salida solo al finalizar jornada (SI/NO)"] = "Salida solo",
    };

    public static DocumentoFicha DesdeBorrador(
        IReadOnlyDictionary<string, string> datos,
        int anio,
        DateTime generadoEn,
        byte[]? logo = null,
        string version = "borrador (sin registrar)")
    {
        var secciones = new List<SeccionFicha>();

        foreach (var (titulo, claves) in Bloques)
        {
            var campos = claves
                .Where(datos.ContainsKey)
                .Select(clave => new CampoFicha
                {
                    Etiqueta = Etiqueta(clave),
                    // Los RUN se imprimen con puntos, aunque la base los guarde sin ellos.
                    Valor = ClavesRut.EsClave(clave) ? RutChileno.Formatear(datos[clave]) : datos[clave],
                })
                .ToList();

            if (campos.Count > 0)
            {
                secciones.Add(new SeccionFicha { Titulo = titulo, Campos = campos });
            }
        }

        return new DocumentoFicha
        {
            Folio = Folio(datos, anio, generadoEn),
            Anio = anio,
            TipoTramite = datos.GetValueOrDefault("modo") ?? "Matrícula",
            Version = version,
            GeneradoEn = generadoEn,
            Detalle = datos.GetValueOrDefault("modo: detalle"),
            Logo = logo,
            Secciones = secciones,
            Autorizaciones = AutorizacionesFormulario,
        };
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

    /// <summary>
    /// Folio estable a partir del contenido: mientras el borrador no se registre,
    /// el mismo expediente conserva su folio al reimprimir.
    /// </summary>
    private static string Folio(IReadOnlyDictionary<string, string> datos, int anio, DateTime generadoEn)
    {
        var semilla = string.Join('|', datos.OrderBy(par => par.Key).Select(par => $"{par.Key}={par.Value}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(semilla)));

        return $"{anio}-{generadoEn:yyyyMMdd}-{hash[..4]}";
    }
}
