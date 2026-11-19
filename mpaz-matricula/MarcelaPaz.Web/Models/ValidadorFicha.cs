using MarcelaPaz.Domain.Identificacion;

namespace MarcelaPaz.Web.Models;

/// <summary>
/// Reglas del asistente que dependen de varios campos y no se pueden expresar
/// con un atributo: al menos un bloque de madre/padre, contacto de emergencia,
/// retiro autorizado, restricción judicial, respuestas de salud, PIE y
/// autorizaciones. Devuelve los errores para que el controlador los agregue al
/// <c>ModelState</c>; <c>Campo</c> nulo indica un error general del paso.
///
/// Además de la obligatoriedad, se revisa que los valores recibidos sean los
/// que ofrece el formulario (sí/no, tipo de restricción, escolaridad) y que los
/// bloques no se contradigan entre sí: dos RUN iguales, contactos repetidos o
/// una persona autorizada que es el propio estudiante. Los pasos 2 a 5 forman
/// los bloques B (familia y apoderados) y C (emergencia y retiro).
/// </summary>
public static class ValidadorFicha
{
    /// <summary>Respuestas que admiten las preguntas de sí/no del formulario.</summary>
    private static readonly string[] RespuestasSiNo = { "Si", "No" };

    /// <summary>Tipos de restricción que ofrece el paso 2.</summary>
    private static readonly string[] TiposRestriccion = { "Judicial", "Familiar" };

    /// <summary>Escolaridades que ofrece el paso 2, en el mismo orden de la vista.</summary>
    private static readonly string[] Escolaridades =
    {
        "Sin estudios",
        "Educación básica",
        "Educación media",
        "Educación media técnico-profesional",
        "Técnico de nivel superior",
        "Educación universitaria",
        "Postgrado",
    };

    /// <summary>Mínimo de dígitos para aceptar un teléfono de contacto.</summary>
    private const int DigitosTelefonoMinimos = 6;

    public sealed record ErrorValidacion(string? Campo, string Mensaje);

    public static IReadOnlyList<ErrorValidacion> Validar(int paso, FichaForm form)
    {
        var errores = new List<ErrorValidacion>();

        switch (paso)
        {
            case 2:
                var madre = ConDatos(form.Madre_Nombre_Apellido, form.Madre_Cedula_Identidad,
                    form.Madre_Telefono, form.Madre_Escolaridad, form.Madre_Domicilio, form.Madre_Correo);
                var padre = ConDatos(form.Padre_Nombre_Apellido, form.Padre_Cedula_Identidad,
                    form.Padre_Telefono, form.Padre_Escolaridad, form.Padre_Domicilio, form.Padre_Correo);

                if (!madre && !padre)
                {
                    errores.Add(new ErrorValidacion(null,
                        "Complete al menos los datos de la madre o los del padre."));
                }

                if (madre)
                {
                    ExigirAdulto(errores, "de la madre", form.Madre_Nombre_Apellido, form.Madre_Cedula_Identidad,
                        form.Madre_Telefono, nameof(FichaForm.Madre_Nombre_Apellido),
                        nameof(FichaForm.Madre_Cedula_Identidad), nameof(FichaForm.Madre_Telefono));

                    ExigirEscolaridad(errores, "de la madre", form.Madre_Escolaridad,
                        nameof(FichaForm.Madre_Escolaridad));
                }

                if (padre)
                {
                    ExigirAdulto(errores, "del padre", form.Padre_Nombre_Apellido, form.Padre_Cedula_Identidad,
                        form.Padre_Telefono, nameof(FichaForm.Padre_Nombre_Apellido),
                        nameof(FichaForm.Padre_Cedula_Identidad), nameof(FichaForm.Padre_Telefono));

                    ExigirEscolaridad(errores, "del padre", form.Padre_Escolaridad,
                        nameof(FichaForm.Padre_Escolaridad));
                }

                // El mismo RUN en los dos bloques significaría la misma persona
                // registrada dos veces: la ficha impresa quedaría contradictoria.
                if (madre && padre && MismoRut(form.Madre_Cedula_Identidad, form.Padre_Cedula_Identidad))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.Padre_Cedula_Identidad),
                        "El RUN del padre no puede ser el mismo que el de la madre: revise los datos."));
                }

                ExigirRestriccion(errores, "de la madre", form.Restriccion_Madre,
                    form.Restriccion_Madre_Tipo, form.Restriccion_Madre_Detalle,
                    nameof(FichaForm.Restriccion_Madre), nameof(FichaForm.Restriccion_Madre_Tipo),
                    nameof(FichaForm.Restriccion_Madre_Detalle));

                if (padre)
                {
                    // La restricción del padre sólo aplica cuando hay datos del
                    // padre; sin vínculo no hay a quién restringir.
                    ExigirRestriccion(errores, "del padre", form.Restriccion_Padre,
                        form.Restriccion_Padre_Tipo, form.Restriccion_Padre_Detalle,
                        nameof(FichaForm.Restriccion_Padre), nameof(FichaForm.Restriccion_Padre_Tipo),
                        nameof(FichaForm.Restriccion_Padre_Detalle));
                }
                else if (EsSi(form.Restriccion_Padre))
                {
                    // Declarar una restricción sin datos del padre deja una
                    // prohibición sin persona a la que aplicarla.
                    errores.Add(new ErrorValidacion(nameof(FichaForm.Restriccion_Padre),
                        "Declaró una restricción del padre, pero no registró sus datos: complete el bloque del padre o responda «No»."));
                }

                break;

            case 3:
                ExigirAdulto(errores, "del apoderado principal", form.Principal_Nombre_Apellido,
                    form.Principal_Cedula_Identidad, form.Principal_Telefono,
                    nameof(FichaForm.Principal_Nombre_Apellido),
                    nameof(FichaForm.Principal_Cedula_Identidad), nameof(FichaForm.Principal_Telefono));

                if (string.IsNullOrWhiteSpace(form.Principal_Vinculo))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.Principal_Vinculo),
                        "Indique el vínculo del apoderado principal."));
                }

                var secundario = ConDatos(form.Secundario_Nombre_Apellido, form.Secundario_Cedula_Identidad,
                    form.Secundario_Telefono, form.Secundario_Vinculo, form.Secundario_Correo);

                if (secundario)
                {
                    ExigirAdulto(errores, "del apoderado secundario", form.Secundario_Nombre_Apellido,
                        form.Secundario_Cedula_Identidad, form.Secundario_Telefono,
                        nameof(FichaForm.Secundario_Nombre_Apellido),
                        nameof(FichaForm.Secundario_Cedula_Identidad), nameof(FichaForm.Secundario_Telefono));

                    if (string.IsNullOrWhiteSpace(form.Secundario_Vinculo))
                    {
                        errores.Add(new ErrorValidacion(nameof(FichaForm.Secundario_Vinculo),
                            "Indique el vínculo del apoderado secundario."));
                    }

                    // Un apoderado no puede ocupar los dos lugares de la ficha.
                    if (MismoRut(form.Principal_Cedula_Identidad, form.Secundario_Cedula_Identidad))
                    {
                        errores.Add(new ErrorValidacion(nameof(FichaForm.Secundario_Cedula_Identidad),
                            "El apoderado secundario no puede ser la misma persona que el principal: revise el RUN."));
                    }
                }

                break;

            case 4:
                var primero = ConDatos(form.Contacto1_Identificacion, form.Contacto1_Telefono, form.Contacto1_Vinculo);
                var segundo = ConDatos(form.Contacto2_Identificacion, form.Contacto2_Telefono, form.Contacto2_Vinculo);

                if (!primero && !segundo)
                {
                    errores.Add(new ErrorValidacion(null, "Registre al menos un contacto de emergencia."));
                }

                if (primero)
                {
                    ExigirContacto(errores, "1", form.Contacto1_Identificacion, form.Contacto1_Telefono,
                        form.Contacto1_Vinculo, nameof(FichaForm.Contacto1_Identificacion),
                        nameof(FichaForm.Contacto1_Telefono), nameof(FichaForm.Contacto1_Vinculo));
                }

                if (segundo)
                {
                    ExigirContacto(errores, "2", form.Contacto2_Identificacion, form.Contacto2_Telefono,
                        form.Contacto2_Vinculo, nameof(FichaForm.Contacto2_Identificacion),
                        nameof(FichaForm.Contacto2_Telefono), nameof(FichaForm.Contacto2_Vinculo));
                }

                // Dos contactos con el mismo teléfono son una sola persona: en
                // una urgencia se llamaría dos veces al mismo número.
                if (primero && segundo && MismoTelefono(form.Contacto1_Telefono, form.Contacto2_Telefono))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.Contacto2_Telefono),
                        "El segundo contacto de emergencia tiene el mismo teléfono que el primero: registre a otra persona."));
                }

                break;

            case 5:
                if (string.IsNullOrWhiteSpace(form.SalidaSolo))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.SalidaSolo),
                        "Indique si el estudiante puede salir solo al finalizar la jornada."));
                }
                else if (!ValorPermitido(form.SalidaSolo, RespuestasSiNo))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.SalidaSolo),
                        "Responda «Si» o «No» sobre la salida solo del estudiante."));
                }
                else if (EsNo(form.SalidaSolo)
                         && !ConDatos(form.Retiro1_Rut, form.Retiro2_Rut, form.Retiro3_Rut))
                {
                    errores.Add(new ErrorValidacion(null,
                        "Si el estudiante no sale solo, registre al menos una persona autorizada para retirarlo."));
                }

                ExigirRetiro(errores, "1", form.Retiro1_Rut, form.Retiro1_Vinculo,
                    nameof(FichaForm.Retiro1_Rut), nameof(FichaForm.Retiro1_Vinculo));
                ExigirRetiro(errores, "2", form.Retiro2_Rut, form.Retiro2_Vinculo,
                    nameof(FichaForm.Retiro2_Rut), nameof(FichaForm.Retiro2_Vinculo));
                ExigirRetiro(errores, "3", form.Retiro3_Rut, form.Retiro3_Vinculo,
                    nameof(FichaForm.Retiro3_Rut), nameof(FichaForm.Retiro3_Vinculo));

                // Cada persona autorizada es distinta: repetir el RUN anula el
                // sentido de tener tres cupos.
                if (MismoRut(form.Retiro1_Rut, form.Retiro2_Rut))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.Retiro2_Rut),
                        "La persona autorizada 2 tiene el mismo RUN que la 1: registre personas distintas."));
                }

                if (MismoRut(form.Retiro1_Rut, form.Retiro3_Rut) || MismoRut(form.Retiro2_Rut, form.Retiro3_Rut))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.Retiro3_Rut),
                        "La persona autorizada 3 repite el RUN de otra autorizada: registre personas distintas."));
                }

                // El propio estudiante no puede autorizarse para retirarse solo.
                var documentoEstudiante = RutDelEstudiante(form);

                if (!string.IsNullOrEmpty(documentoEstudiante))
                {
                    if (MismoRut(documentoEstudiante, form.Retiro1_Rut))
                    {
                        errores.Add(new ErrorValidacion(nameof(FichaForm.Retiro1_Rut),
                            "La persona autorizada 1 no puede ser el propio estudiante."));
                    }

                    if (MismoRut(documentoEstudiante, form.Retiro2_Rut))
                    {
                        errores.Add(new ErrorValidacion(nameof(FichaForm.Retiro2_Rut),
                            "La persona autorizada 2 no puede ser el propio estudiante."));
                    }

                    if (MismoRut(documentoEstudiante, form.Retiro3_Rut))
                    {
                        errores.Add(new ErrorValidacion(nameof(FichaForm.Retiro3_Rut),
                            "La persona autorizada 3 no puede ser el propio estudiante."));
                    }
                }

                break;

            case 6:
                if (string.IsNullOrWhiteSpace(form.TieneSituacionMedica))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.TieneSituacionMedica),
                        "Indique si el estudiante tiene alguna situación médica."));
                }

                // Las alergias son opcionales: si no hay, se deja en blanco y
                // no aparecen en la ficha impresa.
                if (string.IsNullOrWhiteSpace(form.Contacto_Sanitario_Nombre))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.Contacto_Sanitario_Nombre),
                        "Indique la persona a quien avisar ante una urgencia sanitaria."));
                }

                if (string.IsNullOrWhiteSpace(form.Contacto_Sanitario_Telefono))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.Contacto_Sanitario_Telefono),
                        "Indique el teléfono del contacto sanitario."));
                }

                if (string.IsNullOrWhiteSpace(form.ParticipaPie))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.ParticipaPie),
                        "Indique si el estudiante participa del PIE."));
                }

                if (EsSi(form.TieneSituacionMedica)
                    && !ConDatos(form.Medica1_Identificacion, form.Medica1_Situacion,
                        form.Medica2_Identificacion, form.Medica2_Situacion,
                        form.Medica3_Identificacion, form.Medica3_Situacion))
                {
                    errores.Add(new ErrorValidacion(null,
                        "Si declara una situación médica, registre al menos una."));
                }

                ExigirSituacion(errores, "1", form.Medica1_Identificacion, form.Medica1_Situacion,
                    form.Medica1_Tratamiento, nameof(FichaForm.Medica1_Identificacion),
                    nameof(FichaForm.Medica1_Situacion), nameof(FichaForm.Medica1_Tratamiento));
                ExigirSituacion(errores, "2", form.Medica2_Identificacion, form.Medica2_Situacion,
                    form.Medica2_Tratamiento, nameof(FichaForm.Medica2_Identificacion),
                    nameof(FichaForm.Medica2_Situacion), nameof(FichaForm.Medica2_Tratamiento));
                ExigirSituacion(errores, "3", form.Medica3_Identificacion, form.Medica3_Situacion,
                    form.Medica3_Tratamiento, nameof(FichaForm.Medica3_Identificacion),
                    nameof(FichaForm.Medica3_Situacion), nameof(FichaForm.Medica3_Tratamiento));

                if (EsSi(form.ParticipaPie) && string.IsNullOrWhiteSpace(form.PieDiagnostico))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.PieDiagnostico),
                        "Indique si cuenta con diagnóstico o documentación PIE."));
                }

                if (EsSi(form.PieDiagnostico) && string.IsNullOrWhiteSpace(form.PieApoyos))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.PieApoyos),
                        "Describa los apoyos o el diagnóstico informado."));
                }

                break;

            case 7:
                if (string.IsNullOrWhiteSpace(form.AutorizaImagen))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.AutorizaImagen),
                        "Responda la autorización de uso de imagen."));
                }

                if (string.IsNullOrWhiteSpace(form.AutorizaConvivencia))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.AutorizaConvivencia),
                        "Responda la autorización de Convivencia Escolar."));
                }

                break;
        }

        return errores;
    }

    private static void ExigirAdulto(
        List<ErrorValidacion> errores,
        string etiqueta,
        string? nombre,
        string? documento,
        string? telefono,
        string campoNombre,
        string campoDocumento,
        string campoTelefono)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            errores.Add(new ErrorValidacion(campoNombre, $"Indique el nombre {etiqueta}."));
        }

        if (string.IsNullOrWhiteSpace(documento))
        {
            errores.Add(new ErrorValidacion(campoDocumento, $"Indique el RUN {etiqueta}."));
        }
        else if (!RutChileno.EsValido(documento))
        {
            errores.Add(new ErrorValidacion(campoDocumento, $"El RUN {etiqueta} no es válido."));
        }

        ExigirTelefono(errores, etiqueta, telefono, campoTelefono);
    }

    /// <summary>Teléfono obligatorio y con dígitos suficientes para ser marcable.</summary>
    private static void ExigirTelefono(
        List<ErrorValidacion> errores,
        string etiqueta,
        string? telefono,
        string campoTelefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
        {
            errores.Add(new ErrorValidacion(campoTelefono, $"Indique el teléfono {etiqueta}."));
            return;
        }

        if (!TelefonoValido(telefono))
        {
            errores.Add(new ErrorValidacion(campoTelefono,
                $"El teléfono {etiqueta} debe tener al menos {DigitosTelefonoMinimos} dígitos."));
        }
    }

    /// <summary>Escolaridad opcional, pero si se responde debe ser una de las ofrecidas.</summary>
    private static void ExigirEscolaridad(
        List<ErrorValidacion> errores,
        string etiqueta,
        string? escolaridad,
        string campo)
    {
        if (string.IsNullOrWhiteSpace(escolaridad) || ValorPermitido(escolaridad, Escolaridades))
        {
            return;
        }

        errores.Add(new ErrorValidacion(campo,
            $"La escolaridad {etiqueta} no es una de las opciones del formulario: selecciónela de la lista."));
    }

    /// <summary>
    /// Pregunta de restricción: obligatoria, con respuesta sí/no y, cuando es
    /// afirmativa, con tipo y detalle.
    /// </summary>
    private static void ExigirRestriccion(
        List<ErrorValidacion> errores,
        string etiqueta,
        string? respuesta,
        string? tipo,
        string? detalle,
        string campoRespuesta,
        string campoTipo,
        string campoDetalle)
    {
        if (string.IsNullOrWhiteSpace(respuesta))
        {
            errores.Add(new ErrorValidacion(campoRespuesta,
                $"Responda si existe alguna restricción que prohíba al {etiqueta} vincularse."));
            return;
        }

        if (!ValorPermitido(respuesta, RespuestasSiNo))
        {
            errores.Add(new ErrorValidacion(campoRespuesta,
                $"Responda «Si» o «No» a la restricción {etiqueta}."));
            return;
        }

        if (!EsSi(respuesta))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(tipo))
        {
            errores.Add(new ErrorValidacion(campoTipo,
                $"Indique si la restricción {etiqueta} es judicial o familiar."));
        }
        else if (!ValorPermitido(tipo, TiposRestriccion))
        {
            errores.Add(new ErrorValidacion(campoTipo,
                $"El tipo de restricción {etiqueta} debe ser Judicial o Familiar."));
        }

        if (string.IsNullOrWhiteSpace(detalle))
        {
            errores.Add(new ErrorValidacion(campoDetalle,
                $"Describa la restricción declarada {etiqueta}."));
        }
    }

    private static void ExigirContacto(
        List<ErrorValidacion> errores,
        string numero,
        string? nombre,
        string? telefono,
        string? vinculo,
        string campoNombre,
        string campoTelefono,
        string campoVinculo)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            errores.Add(new ErrorValidacion(campoNombre, $"Indique el nombre del contacto de emergencia {numero}."));
        }

        if (string.IsNullOrWhiteSpace(telefono))
        {
            errores.Add(new ErrorValidacion(campoTelefono, $"Indique el teléfono del contacto de emergencia {numero}."));
        }
        else if (!TelefonoValido(telefono))
        {
            errores.Add(new ErrorValidacion(campoTelefono,
                $"El teléfono del contacto de emergencia {numero} debe tener al menos {DigitosTelefonoMinimos} dígitos."));
        }

        if (string.IsNullOrWhiteSpace(vinculo))
        {
            errores.Add(new ErrorValidacion(campoVinculo, $"Indique el vínculo del contacto de emergencia {numero}."));
        }
    }

    private static void ExigirRetiro(
        List<ErrorValidacion> errores,
        string numero,
        string? rut,
        string? vinculo,
        string campoRut,
        string campoVinculo)
    {
        if (string.IsNullOrWhiteSpace(rut) && string.IsNullOrWhiteSpace(vinculo))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(rut))
        {
            errores.Add(new ErrorValidacion(campoRut, $"Indique el RUN de la persona autorizada {numero}."));
        }
        else if (!RutChileno.EsValido(rut))
        {
            errores.Add(new ErrorValidacion(campoRut, $"El RUN de la persona autorizada {numero} no es válido."));
        }

        if (string.IsNullOrWhiteSpace(vinculo))
        {
            errores.Add(new ErrorValidacion(campoVinculo, $"Indique el vínculo de la persona autorizada {numero}."));
        }
    }

    private static void ExigirSituacion(
        List<ErrorValidacion> errores,
        string numero,
        string? identificacion,
        string? detalle,
        string? tratamiento,
        string campoIdentificacion,
        string campoDetalle,
        string campoTratamiento)
    {
        if (!ConDatos(identificacion, detalle, tratamiento))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(identificacion))
        {
            errores.Add(new ErrorValidacion(campoIdentificacion, $"Identifique la situación médica {numero}."));
        }

        if (string.IsNullOrWhiteSpace(detalle))
        {
            errores.Add(new ErrorValidacion(campoDetalle, $"Describa la situación médica {numero}."));
        }

        if (string.IsNullOrWhiteSpace(tratamiento))
        {
            errores.Add(new ErrorValidacion(campoTratamiento, $"Indique si la situación médica {numero} está en tratamiento."));
        }
    }

    /// <summary>RUN del estudiante del paso 1, sólo cuando el documento es un RUN.</summary>
    private static string? RutDelEstudiante(FichaForm form)
    {
        var tipo = (form.Tipo_Identificador ?? "RUN").Trim().ToUpperInvariant();

        if (tipo != "RUN" || string.IsNullOrWhiteSpace(form.Cedula_Identidad))
        {
            return null;
        }

        return RutChileno.Limpiar(form.Cedula_Identidad);
    }

    /// <summary>Compara dos RUN por su forma normalizada; vacíos nunca coinciden.</summary>
    private static bool MismoRut(string? uno, string? otro)
    {
        if (string.IsNullOrWhiteSpace(uno) || string.IsNullOrWhiteSpace(otro))
        {
            return false;
        }

        return string.Equals(RutChileno.Limpiar(uno), RutChileno.Limpiar(otro), StringComparison.Ordinal);
    }

    /// <summary>Compara dos teléfonos por sus dígitos; vacíos nunca coinciden.</summary>
    private static bool MismoTelefono(string? uno, string? otro)
    {
        var digitosUno = SoloDigitos(uno);
        var digitosOtro = SoloDigitos(otro);

        return digitosUno.Length > 0
            && digitosOtro.Length > 0
            && string.Equals(digitosUno, digitosOtro, StringComparison.Ordinal);
    }

    /// <summary>Un teléfono es válido si tiene los dígitos mínimos para marcarse.</summary>
    private static bool TelefonoValido(string? telefono) =>
        SoloDigitos(telefono).Length >= DigitosTelefonoMinimos;

    private static string SoloDigitos(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());

    /// <summary>Compara sin distinguir mayúsculas ni espacios sobrantes.</summary>
    private static bool ValorPermitido(string? valor, IReadOnlyCollection<string> permitidos) =>
        !string.IsNullOrWhiteSpace(valor)
        && permitidos.Contains(valor.Trim(), StringComparer.OrdinalIgnoreCase);

    private static bool ConDatos(params string?[] valores) =>
        valores.Any(valor => !string.IsNullOrWhiteSpace(valor));

    private static bool EsSi(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && valor.Trim().StartsWith("S", StringComparison.OrdinalIgnoreCase);

    private static bool EsNo(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && valor.Trim().StartsWith("N", StringComparison.OrdinalIgnoreCase);
}
