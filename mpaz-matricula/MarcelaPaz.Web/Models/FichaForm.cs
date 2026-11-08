using System.ComponentModel.DataAnnotations;
using MarcelaPaz.Web.Models.Validaciones;

namespace MarcelaPaz.Web.Models;

/// <summary>
/// Campos del asistente de matrícula. Las validaciones de formato y de
/// obligatoriedad se declaran aquí; las reglas que dependen de varios campos
/// (por ejemplo, «al menos un bloque de madre o padre») se revisan en el
/// controlador antes de guardar. Los textos de error son los que ve el
/// funcionario en pantalla.
/// </summary>
public class FichaForm
{
    // Paso 1 · Curso al que postula (encabezado de la ficha: nivel, letra y año)
    [Required(ErrorMessage = "Seleccione el curso al que postula el estudiante.")]
    public string? Curso_Nivel { get; set; }

    [Required(ErrorMessage = "Seleccione la letra del curso.")]
    public string? Curso_Letra { get; set; }

    [Required(ErrorMessage = "Falta el año escolar.")]
    public string? Curso_Anio { get; set; }

    // Paso 1 · Identificación del estudiante (Bloque A: RUN o IPE/IPA)
    [Required(ErrorMessage = "Seleccione el tipo de documento del estudiante.")]
    public string? Tipo_Identificador { get; set; } = "RUN";

    [Required(ErrorMessage = "Ingrese el RUN o número IPE/IPA del estudiante.")]
    [Identificador]
    [StringLength(20, ErrorMessage = "El documento no puede superar los 20 caracteres.")]
    public string? Cedula_Identidad { get; set; }

    [Required(ErrorMessage = "Ingrese los nombres del estudiante.")]
    [StringLength(120, ErrorMessage = "Los nombres no pueden superar los 120 caracteres.")]
    public string? Nombres { get; set; }

    [Required(ErrorMessage = "Ingrese el apellido paterno del estudiante.")]
    [StringLength(80, ErrorMessage = "El apellido no puede superar los 80 caracteres.")]
    public string? Apellido_Paterno { get; set; }

    [StringLength(80, ErrorMessage = "El apellido no puede superar los 80 caracteres.")]
    public string? Apellido_Materno { get; set; }

    [Required(ErrorMessage = "Ingrese la fecha de nacimiento del estudiante.")]
    [FechaNacimiento]
    public string? Fecha_Nacimiento { get; set; }

    [Required(ErrorMessage = "Ingrese la nacionalidad del estudiante.")]
    [StringLength(60, ErrorMessage = "La nacionalidad no puede superar los 60 caracteres.")]
    public string? Nacionalidad { get; set; } = "Chilena";

    [Required(ErrorMessage = "Ingrese el domicilio del estudiante.")]
    [StringLength(255, ErrorMessage = "El domicilio no puede superar los 255 caracteres.")]
    public string? Domicilio { get; set; }

    [Required(ErrorMessage = "Ingrese la comuna del domicilio.")]
    public string? Comuna { get; set; }

    // Paso 2 · Contacto: datos de la madre y del padre (se exige al menos un bloque)
    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string? Madre_Nombre_Apellido { get; set; }

    [Rut]
    public string? Madre_Cedula_Identidad { get; set; }

    [StringLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
    public string? Madre_Telefono { get; set; }

    public string? Madre_Escolaridad { get; set; }

    [StringLength(255, ErrorMessage = "El domicilio no puede superar los 255 caracteres.")]
    public string? Madre_Domicilio { get; set; }

    [EmailAddress(ErrorMessage = "Revise el formato del correo de la madre.")]
    public string? Madre_Correo { get; set; }

    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string? Padre_Nombre_Apellido { get; set; }

    [Rut]
    public string? Padre_Cedula_Identidad { get; set; }

    [StringLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
    public string? Padre_Telefono { get; set; }

    public string? Padre_Escolaridad { get; set; }

    [StringLength(255, ErrorMessage = "El domicilio no puede superar los 255 caracteres.")]
    public string? Padre_Domicilio { get; set; }

    [EmailAddress(ErrorMessage = "Revise el formato del correo del padre.")]
    public string? Padre_Correo { get; set; }

    // Restricción (SI/NO): ¿Existe alguna restricción judicial o familiar que
    // prohíba a la madre vincularse con el o la estudiante?
    public string? Restriccion_Madre { get; set; }

    // Paso 3 · Apoderado principal y secundario
    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string? Principal_Nombre_Apellido { get; set; }

    [Rut]
    public string? Principal_Cedula_Identidad { get; set; }

    [StringLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
    public string? Principal_Telefono { get; set; }

    public string? Principal_Vinculo { get; set; }

    [EmailAddress(ErrorMessage = "Revise el formato del correo del apoderado principal.")]
    public string? Principal_Correo { get; set; }

    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string? Secundario_Nombre_Apellido { get; set; }

    [Rut]
    public string? Secundario_Cedula_Identidad { get; set; }

    [StringLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
    public string? Secundario_Telefono { get; set; }

    public string? Secundario_Vinculo { get; set; }

    [EmailAddress(ErrorMessage = "Revise el formato del correo del apoderado secundario.")]
    public string? Secundario_Correo { get; set; }

    // Paso 4 · Contactos de emergencia (registros 01 y 02 de la ficha)
    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string? Contacto1_Identificacion { get; set; }

    [StringLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
    public string? Contacto1_Telefono { get; set; }

    public string? Contacto1_Vinculo { get; set; }

    [StringLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string? Contacto2_Identificacion { get; set; }

    [StringLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
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

    /// <summary>Alergias declaradas. Se escribe «Ninguna» cuando no las hay.</summary>
    [StringLength(255, ErrorMessage = "Las alergias no pueden superar los 255 caracteres.")]
    public string? Alergias { get; set; }

    /// <summary>Persona a quien avisar ante una urgencia sanitaria.</summary>
    [StringLength(150, ErrorMessage = "El contacto sanitario no puede superar los 150 caracteres.")]
    public string? Contacto_Sanitario_Nombre { get; set; }

    [StringLength(20, ErrorMessage = "El teléfono no puede superar los 20 caracteres.")]
    public string? Contacto_Sanitario_Telefono { get; set; }

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
    [StringLength(500, ErrorMessage = "Los apoyos no pueden superar los 500 caracteres.")]
    public string? PieApoyos { get; set; }

    // Autorizaciones del apoderado (Si / No). Se guardan en la tabla autorizacion.
    public string? AutorizaImagen { get; set; }

    public string? AutorizaConvivencia { get; set; }

    // Paso 7 · confirmación obligatoria para enviar la ficha a revisión.
    // Se valida en el controlador: un [Range] sobre bool invalidaba todos los pasos.
    public bool Confirmo_Revision { get; set; }
}
