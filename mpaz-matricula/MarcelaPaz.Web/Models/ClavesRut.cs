namespace MarcelaPaz.Web.Models;

/// <summary>
/// Claves del borrador y del documento que contienen un RUN. Se reconocen por su
/// nombre («cédula de identidad», «cédula identidad», «… : rut») para limpiarlas
/// al guardar y formatearlas al mostrar.
/// </summary>
public static class ClavesRut
{
    public static bool EsClave(string clave) =>
        clave.EndsWith("cédula identidad", StringComparison.OrdinalIgnoreCase)
        || clave.EndsWith("cédula de identidad", StringComparison.OrdinalIgnoreCase)
        || clave.EndsWith(": rut", StringComparison.OrdinalIgnoreCase);
}
