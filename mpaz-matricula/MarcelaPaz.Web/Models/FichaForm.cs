using System.ComponentModel.DataAnnotations;
using MarcelaPaz.Web.Models.Validaciones;

namespace MarcelaPaz.Web.Models;

/// <summary>
/// Campos provisorios del wizard. Todavía no hay campos obligatorios, pero se
/// revisa el formato del RUN (con o sin puntos) y de los correos al guardar.
/// </summary>
public class FichaForm
{
    // Paso 1 · Curso al que postula (encabezado de la ficha: nivel, letra y año)
    public string? Curso_Nivel { get; set; }
    public string? Curso_Letra { get; set; }
    public string? Curso_Anio { get; set; }

    // Paso 1 · Datos del apoderado (antecedentes de la ficha, sección 1)
    public string? Nombres { get; set; }
    public string? Apellido_Paterno { get; set; }
    public string? Apellido_Materno { get; set; }
    [Rut]
    public string? Cedula_Identidad { get; set; }
    public string? Fecha_Nacimiento { get; set; }
    public string? Nacionalidad { get; set; }
    public string? Domicilio { get; set; }
    public string? Comuna { get; set; }

    // Paso 2 · Contacto: datos de la madre y del padre (completar al menos un bloque)
    public string? Madre_Nombre_Apellido { get; set; }
    [Rut]
    public string? Madre_Cedula_Identidad { get; set; }
    public string? Madre_Telefono { get; set; }
    public string? Madre_Escolaridad { get; set; }
    public string? Madre_Domicilio { get; set; }
    [EmailAddress(ErrorMessage = "Revise el formato del correo.")]
    public string? Madre_Correo { get; set; }

    public string? Padre_Nombre_Apellido { get; set; }
    [Rut]
    public string? Padre_Cedula_Identidad { get; set; }
    public string? Padre_Telefono { get; set; }
    public string? Padre_Escolaridad { get; set; }
    public string? Padre_Domicilio { get; set; }
    [EmailAddress(ErrorMessage = "Revise el formato del correo.")]
    public string? Padre_Correo { get; set; }

    // Restricción (SI/NO): ¿Existe alguna restricción judicial o familiar que
    // prohíba a la madre vincularse con el o la estudiante?
    public string? Restriccion_Madre { get; set; }

    // Paso 3 · Apoderado principal y secundario
    public string? Principal_Nombre_Apellido { get; set; }
    [Rut]
    public string? Principal_Cedula_Identidad { get; set; }
    public string? Principal_Telefono { get; set; }
    public string? Principal_Vinculo { get; set; }
    [EmailAddress(ErrorMessage = "Revise el formato del correo.")]
    public string? Principal_Correo { get; set; }

    public string? Secundario_Nombre_Apellido { get; set; }
    [Rut]
    public string? Secundario_Cedula_Identidad { get; set; }
    public string? Secundario_Telefono { get; set; }
    public string? Secundario_Vinculo { get; set; }
    [EmailAddress(ErrorMessage = "Revise el formato del correo.")]
    public string? Secundario_Correo { get; set; }

    // Paso 4 · Contactos de emergencia (registros 01 y 02 de la ficha)
    public string? Contacto1_Identificacion { get; set; }
    public string? Contacto1_Telefono { get; set; }
    public string? Contacto1_Vinculo { get; set; }

    public string? Contacto2_Identificacion { get; set; }
    public string? Contacto2_Telefono { get; set; }
    public string? Contacto2_Vinculo { get; set; }

    // Paso 5 · Retiro del estudiante
    // ¿Autorizado para salir solo al finalizar la jornada? (Si / No)
    public string? SalidaSolo { get; set; }

    [Rut]
    public string? Retiro1_Rut { get; set; }
    public string? Retiro1_Vinculo { get; set; }

    [Rut]
    public string? Retiro2_Rut { get; set; }
    public string? Retiro2_Vinculo { get; set; }

    [Rut]
    public string? Retiro3_Rut { get; set; }
    public string? Retiro3_Vinculo { get; set; }

    // Paso 6 · Datos médicos
    // ¿Cuenta con alguna situación médica? (Si / No)
    public string? TieneSituacionMedica { get; set; }

    public string? Medica1_Identificacion { get; set; }
    public string? Medica1_Situacion { get; set; }
    public string? Medica1_Tratamiento { get; set; }

    public string? Medica2_Identificacion { get; set; }
    public string? Medica2_Situacion { get; set; }
    public string? Medica2_Tratamiento { get; set; }

    public string? Medica3_Identificacion { get; set; }
    public string? Medica3_Situacion { get; set; }
    public string? Medica3_Tratamiento { get; set; }

    // ¿Condición médica que amerite salir constantemente al baño? (Si / No)
    public string? SalidaBano { get; set; }

    // Autorización de paracetamol de la ficha vigente (Si / No)
    public string? AutorizaParacetamol { get; set; }

    // Paso 6 · Programa de Integración Escolar (PIE)
    // ¿Participa del PIE? (Si / No / Pendiente)
    public string? ParticipaPie { get; set; }

    // ¿Cuenta con diagnóstico o documentación PIE? (Si / No)
    public string? PieDiagnostico { get; set; }

    // Apoyos informados o diagnóstico declarado
    public string? PieApoyos { get; set; }

    // Autorizaciones del apoderado (Si / No)
    public string? AutorizaImagen { get; set; }

    public string? AutorizaConvivencia { get; set; }

    // Paso 7 · confirmación obligatoria para enviar la ficha a revisión
    public bool Confirmo_Revision { get; set; }
}
