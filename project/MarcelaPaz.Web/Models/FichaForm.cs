namespace MarcelaPaz.Web.Models;

/// <summary>
/// Campos provisorios del wizard. Sin validación: los bloques definitivos y sus
/// campos obligatorios se acuerdan con Dirección y Secretaría antes de cerrar.
/// </summary>
public class FichaForm
{
    // Paso 1 · Datos del apoderado (antecedentes de la ficha, sección 1)
    public string? Nombres { get; set; }
    public string? Apellido_Paterno { get; set; }
    public string? Apellido_Materno { get; set; }
    public string? Cedula_Identidad { get; set; }
    public string? Fecha_Nacimiento { get; set; }
    public string? Nacionalidad { get; set; }
    public string? Domicilio { get; set; }
    public string? Comuna { get; set; }

    // Paso 2 · Contacto: datos de la madre y del padre (completar al menos un bloque)
    public string? Madre_Nombre_Apellido { get; set; }
    public string? Madre_Cedula_Identidad { get; set; }
    public string? Madre_Telefono { get; set; }
    public string? Madre_Escolaridad { get; set; }
    public string? Madre_Domicilio { get; set; }
    public string? Madre_Correo { get; set; }

    public string? Padre_Nombre_Apellido { get; set; }
    public string? Padre_Cedula_Identidad { get; set; }
    public string? Padre_Telefono { get; set; }
    public string? Padre_Escolaridad { get; set; }
    public string? Padre_Domicilio { get; set; }
    public string? Padre_Correo { get; set; }

    // Restricción (SI/NO): ¿Existe alguna restricción judicial o familiar que
    // prohíba a la madre vincularse con el o la estudiante?
    public string? Restriccion_Madre { get; set; }

    // Paso 3 · Apoderado principal y secundario
    public string? Principal_Nombre_Apellido { get; set; }
    public string? Principal_Cedula_Identidad { get; set; }
    public string? Principal_Telefono { get; set; }
    public string? Principal_Vinculo { get; set; }
    public string? Principal_Correo { get; set; }

    public string? Secundario_Nombre_Apellido { get; set; }
    public string? Secundario_Cedula_Identidad { get; set; }
    public string? Secundario_Telefono { get; set; }
    public string? Secundario_Vinculo { get; set; }
    public string? Secundario_Correo { get; set; }

    // Pasos 4 a 6 (contactos de emergencia, retiro y datos médicos):
    // aún sin campos; se armarán con Dirección y Secretaría antes de cerrar.
}
