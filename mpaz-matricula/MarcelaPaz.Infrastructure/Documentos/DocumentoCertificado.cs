namespace MarcelaPaz.Infrastructure.Documentos;

/// <summary>
/// Contenido del certificado de alumno regular. Estructura plana: la capa
/// Web la arma desde la matrícula vigente y este generador no consulta la
/// base de datos.
///
/// Sólo lleva lo que la identificación del documento exige: establecimiento
/// con RBD y reconocimiento oficial, nombre y RUN del alumno, curso y año,
/// folio, lugar y fecha, firmante y su cargo. Se omite el resto de los datos
/// del alumno (fecha de nacimiento, contacto) por minimización de datos
/// (Ley 21.719): el RUN ya identifica sin ambigüedad.
/// </summary>
public sealed class DocumentoCertificado
{
    public required string Sostenedor { get; init; }

    public required string Establecimiento { get; init; }

    /// <summary>Rol Base de Datos del establecimiento; lo usa Mineduc para identificarlo.</summary>
    public required string Rbd { get; init; }

    /// <summary>Línea de reconocimiento oficial (decreto). Null si aún no se confirma el número.</summary>
    public string? Reconocimiento { get; init; }

    public required string Direccion { get; init; }

    public required string AlumnoNombre { get; init; }

    /// <summary>Documento listo para imprimir: "RUN 25.889.900-8" o "IPE 123456".</summary>
    public required string AlumnoDocumento { get; init; }

    public required string Curso { get; init; }

    public required int Anio { get; init; }

    /// <summary>
    /// Año de la matrícula posterior al año de emisión: cambia la redacción
    /// a "se encuentra matriculado/a ... para cursar" en vez de
    /// "es alumno/a regular ... y cursa".
    /// </summary>
    public bool MatriculaFutura { get; init; }

    /// <summary>Ya formateado: "San Bernardo, 10 de noviembre de 2026".</summary>
    public required string LugarFecha { get; init; }

    /// <summary>
    /// Plazo impreso bajo la fecha: "Válido por 30 días, hasta el 10 de
    /// diciembre de 2026." Null si el papel no declara vigencia.
    /// </summary>
    public string? Vigencia { get; init; }

    /// <summary>
    /// Nombre de quien firma según el registro. No se imprime: el certificado
    /// firma institucionalmente con el cargo (ver <see cref="FirmanteCargo"/>).
    /// </summary>
    public required string FirmanteNombre { get; init; }

    /// <summary>Cargo impreso bajo la línea de firma: «Director/a».</summary>
    public required string FirmanteCargo { get; init; }

    /// <summary>Se muestra una sola vez, bajo el título.</summary>
    public required string Folio { get; init; }

    /// <summary>Código de verificación impreso junto al QR.</summary>
    public required string Codigo { get; init; }

    /// <summary>
    /// Dirección corta que codifica el QR (por ejemplo
    /// <c>https://portal.mpaz.cl/c/ABCD1234EFGH</c>). No se imprime como
    /// texto: el papel sólo muestra el QR y el código.
    /// </summary>
    public required string UrlValidacion { get; init; }

    /// <summary>QR en PNG que apunta a <see cref="UrlValidacion"/>.</summary>
    public required byte[] Qr { get; init; }

    public byte[]? Logo { get; init; }
}
