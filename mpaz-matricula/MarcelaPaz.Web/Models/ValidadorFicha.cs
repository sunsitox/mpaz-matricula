using MarcelaPaz.Domain.Identificacion;

namespace MarcelaPaz.Web.Models;

/// <summary>
/// Reglas del asistente que dependen de varios campos y no se pueden expresar
/// con un atributo: al menos un bloque de madre/padre, contacto de emergencia,
/// retiro autorizado, restricción judicial, respuestas de salud, PIE y
/// autorizaciones. Devuelve los errores para que el controlador los agregue al
/// <c>ModelState</c>; <c>Campo</c> nulo indica un error general del paso.
/// </summary>
public static class ValidadorFicha
{
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
                }

                if (padre)
                {
                    ExigirAdulto(errores, "del padre", form.Padre_Nombre_Apellido, form.Padre_Cedula_Identidad,
                        form.Padre_Telefono, nameof(FichaForm.Padre_Nombre_Apellido),
                        nameof(FichaForm.Padre_Cedula_Identidad), nameof(FichaForm.Padre_Telefono));
                }

                if (string.IsNullOrWhiteSpace(form.Restriccion_Madre))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.Restriccion_Madre),
                        "Responda si existe alguna restricción que prohíba a la madre vincularse."));
                }
                else if (EsSi(form.Restriccion_Madre))
                {
                    if (string.IsNullOrWhiteSpace(form.Restriccion_Madre_Tipo))
                    {
                        errores.Add(new ErrorValidacion(nameof(FichaForm.Restriccion_Madre_Tipo),
                            "Indique si la restricción es judicial o familiar."));
                    }

                    if (string.IsNullOrWhiteSpace(form.Restriccion_Madre_Detalle))
                    {
                        errores.Add(new ErrorValidacion(nameof(FichaForm.Restriccion_Madre_Detalle),
                            "Describa la restricción declarada."));
                    }
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

                if (ConDatos(form.Secundario_Nombre_Apellido, form.Secundario_Cedula_Identidad,
                        form.Secundario_Telefono, form.Secundario_Vinculo, form.Secundario_Correo))
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

                break;

            case 5:
                if (string.IsNullOrWhiteSpace(form.SalidaSolo))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.SalidaSolo),
                        "Indique si el estudiante puede salir solo al finalizar la jornada."));
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
                break;

            case 6:
                if (string.IsNullOrWhiteSpace(form.TieneSituacionMedica))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.TieneSituacionMedica),
                        "Indique si el estudiante tiene alguna situación médica."));
                }

                if (string.IsNullOrWhiteSpace(form.Alergias))
                {
                    errores.Add(new ErrorValidacion(nameof(FichaForm.Alergias),
                        "Indique las alergias o escriba «Ninguna»."));
                }

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

        if (string.IsNullOrWhiteSpace(telefono))
        {
            errores.Add(new ErrorValidacion(campoTelefono, $"Indique el teléfono {etiqueta}."));
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

    private static bool ConDatos(params string?[] valores) =>
        valores.Any(valor => !string.IsNullOrWhiteSpace(valor));

    private static bool EsSi(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && valor.Trim().StartsWith("S", StringComparison.OrdinalIgnoreCase);

    private static bool EsNo(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && valor.Trim().StartsWith("N", StringComparison.OrdinalIgnoreCase);
}
