using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Domain.Identificacion;

namespace MarcelaPaz.Web.Models;

/// <summary>
/// Reglas de la ficha de salud del funcionario (paso 25): documento (RUN o
/// pasaporte), teléfonos marcables, contactos de emergencia y transfusión.
/// Devuelve errores por campo para mostrarlos en el formulario.
/// </summary>
public static class ValidadorFichaSalud
{
    private static readonly string[] SistemasSalud = { "Fonasa", "Isapre", "Otro" };

    private static readonly string[] RespuestasSiNo = { "Si", "No" };

    private const int DigitosTelefonoMinimos = 6;

    public sealed record ErrorValidacion(string? Campo, string Mensaje);

    public static IReadOnlyList<ErrorValidacion> Validar(FichaSaludFuncionario ficha)
    {
        var errores = new List<ErrorValidacion>();

        if (string.IsNullOrWhiteSpace(ficha.NombreCompleto))
        {
            errores.Add(new ErrorValidacion(nameof(ficha.NombreCompleto), "Indique el nombre completo del funcionario."));
        }

        var tieneRut = !string.IsNullOrWhiteSpace(ficha.Rut);
        var tienePasaporte = !string.IsNullOrWhiteSpace(ficha.Pasaporte);

        if (!tieneRut && !tienePasaporte)
        {
            errores.Add(new ErrorValidacion(nameof(ficha.Rut), "Indique el RUN o el pasaporte del funcionario."));
        }

        if (tieneRut && !RutChileno.EsValido(ficha.Rut))
        {
            errores.Add(new ErrorValidacion(nameof(ficha.Rut), "El RUN del funcionario no es válido."));
        }

        ExigirTelefono(errores, "personal", ficha.TelefonoPersonal, nameof(ficha.TelefonoPersonal), obligatorio: false);

        if (string.IsNullOrWhiteSpace(ficha.Contacto1Nombre))
        {
            errores.Add(new ErrorValidacion(nameof(ficha.Contacto1Nombre), "Indique el nombre del contacto de emergencia 1."));
        }

        ExigirTelefono(errores, "del contacto de emergencia 1", ficha.Contacto1Telefono, nameof(ficha.Contacto1Telefono), obligatorio: true);

        var contacto2 = !string.IsNullOrWhiteSpace(ficha.Contacto2Nombre)
            || !string.IsNullOrWhiteSpace(ficha.Contacto2Telefono);

        if (contacto2)
        {
            if (string.IsNullOrWhiteSpace(ficha.Contacto2Nombre))
            {
                errores.Add(new ErrorValidacion(nameof(ficha.Contacto2Nombre), "Indique el nombre del contacto de emergencia 2."));
            }

            ExigirTelefono(errores, "del contacto de emergencia 2", ficha.Contacto2Telefono, nameof(ficha.Contacto2Telefono), obligatorio: true);

            if (MismoTelefono(ficha.Contacto1Telefono, ficha.Contacto2Telefono))
            {
                errores.Add(new ErrorValidacion(nameof(ficha.Contacto2Telefono),
                    "El segundo contacto de emergencia tiene el mismo teléfono que el primero: registre a otra persona."));
            }
        }

        if (!string.IsNullOrWhiteSpace(ficha.SistemaSalud)
            && !SistemasSalud.Contains(ficha.SistemaSalud.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errores.Add(new ErrorValidacion(nameof(ficha.SistemaSalud), "El sistema de salud debe ser Fonasa, Isapre u Otro."));
        }

        ExigirTelefono(errores, "del centro médico", ficha.CentroTelefono, nameof(ficha.CentroTelefono), obligatorio: false);
        ExigirTelefono(errores, "de la ambulancia", ficha.AmbulanciaTelefono, nameof(ficha.AmbulanciaTelefono), obligatorio: false);

        if (string.IsNullOrWhiteSpace(ficha.AceptaTransfusion))
        {
            errores.Add(new ErrorValidacion(nameof(ficha.AceptaTransfusion), "Indique si acepta transfusión de sangre."));
        }
        else if (!RespuestasSiNo.Contains(ficha.AceptaTransfusion.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errores.Add(new ErrorValidacion(nameof(ficha.AceptaTransfusion), "Responda «Si» o «No» a la transfusión de sangre."));
        }
        else if (EsNo(ficha.AceptaTransfusion) && string.IsNullOrWhiteSpace(ficha.MotivoTransfusion))
        {
            errores.Add(new ErrorValidacion(nameof(ficha.MotivoTransfusion),
                "Si no acepta transfusión, indique el motivo religioso o de otra índole."));
        }

        return errores;
    }

    private static void ExigirTelefono(
        List<ErrorValidacion> errores,
        string etiqueta,
        string? telefono,
        string campo,
        bool obligatorio)
    {
        if (string.IsNullOrWhiteSpace(telefono))
        {
            if (obligatorio)
            {
                errores.Add(new ErrorValidacion(campo, $"Indique el teléfono {etiqueta}."));
            }

            return;
        }

        if (SoloDigitos(telefono).Length < DigitosTelefonoMinimos)
        {
            errores.Add(new ErrorValidacion(campo,
                $"El teléfono {etiqueta} debe tener al menos {DigitosTelefonoMinimos} dígitos."));
        }
    }

    private static bool MismoTelefono(string? uno, string? otro)
    {
        var a = SoloDigitos(uno);
        var b = SoloDigitos(otro);

        return a.Length > 0 && string.Equals(a, b, StringComparison.Ordinal);
    }

    private static string SoloDigitos(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());

    private static bool EsNo(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && valor.Trim().StartsWith("N", StringComparison.OrdinalIgnoreCase);
}
