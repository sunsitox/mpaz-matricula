namespace MarcelaPaz.Web.Servicios;

/// <summary>
/// Catálogo único de tipos de documento del expediente (MAT-06/07/09):
/// etiqueta visible y sensibilidad por área. Lo usan la ficha del alumno y
/// los reportes para no duplicar las reglas.
/// </summary>
public static class CatalogoDocumentos
{
    public static string Sensibilidad(string tipo) => tipo switch
    {
        "ORDEN_MEDICA" or "RESPALDO_SALUD" => "SALUD",
        "CERTIFICADO_PIE" => "PIE",
        "DOCUMENTO_JUDICIAL" => "JUDICIAL",
        _ => "GENERAL",
    };

    public static string Etiqueta(string tipo) => tipo switch
    {
        "CEDULA_IDENTIDAD" => "Cédula de identidad",
        "CERTIFICADO_NACIMIENTO" => "Certificado de nacimiento",
        "PODER_TERCERO" => "Poder de tercero",
        "ORDEN_MEDICA" => "Orden médica",
        "RESPALDO_SALUD" => "Respaldo de salud",
        "CERTIFICADO_PIE" => "Certificado PIE",
        "DOCUMENTO_JUDICIAL" => "Documento judicial",
        "FICHA_FIRMADA" => "Ficha firmada",
        "FICHA_PDF" => "Ficha en PDF",
        _ => "Otro documento",
    };

    /// <summary>
    /// ¿Puede la sesión ver un documento de esta sensibilidad? GENERAL lo ve
    /// cualquiera con DOCUMENTOS:LEER; el resto exige su área (S-02).
    /// </summary>
    public static bool VisiblePara(string sensibilidad, Func<string, bool> puedeLeerArea) => sensibilidad switch
    {
        "SALUD" => puedeLeerArea("SALUD"),
        "PIE" => puedeLeerArea("PIE"),
        "JUDICIAL" => puedeLeerArea("JUDICIAL"),
        _ => true,
    };
}
