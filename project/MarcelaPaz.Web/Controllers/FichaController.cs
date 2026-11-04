using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Estructura del wizard de la ficha de matrícula (mockup 02 · ficha-pasos-apoderado).
/// Cuatro pasos con borrador temporal en TempData: aún no hay persistencia en
/// PostgreSQL ni validación de campos. Los bloques definitivos y sus campos
/// obligatorios se definen con Dirección y Secretaría (Flujo B).
/// </summary>
public class FichaController : Controller
{
    private const string ClaveBorrador = "Ficha";

    // El parámetro de ruta por defecto se llama 'id' (Program.cs), así que se ocupa ese nombre:
    // GET /Ficha/Paso/{pasito mostrado}
    [HttpGet]
    public IActionResult Paso(int? id)
    {
        var paso = (id ?? 1) switch { < 1 => 1, > 7 => 7, var n => n };

        ViewData["Paso"] = paso;
        ViewData["Borrador"] = LeerBorrador();

        return View($"Paso{paso}");
    }

    /// <summary>
    /// Estructural por ahora: guarda los campos del paso en un borrador temporal
    /// y avanza. Los datos no se persisten en la base todavía.
    /// </summary>
    [HttpPost]
    public IActionResult Guardar(int? id, Models.FichaForm form)
    {
        var paso = (id ?? 1) switch { < 1 => 1, > 7 => 7, var n => n };

        var datos = LeerBorrador();

        switch (paso)
        {
            case 1:
                GuardarCampo(datos, "nombres", form.Nombres);
                GuardarCampo(datos, "apellido paterno", form.Apellido_Paterno);
                GuardarCampo(datos, "apellido materno", form.Apellido_Materno);
                GuardarCampo(datos, "cédula de identidad", form.Cedula_Identidad);
                GuardarCampo(datos, "fecha nacimiento", form.Fecha_Nacimiento);
                GuardarCampo(datos, "nacionalidad", form.Nacionalidad);
                GuardarCampo(datos, "domicilio", form.Domicilio);
                GuardarCampo(datos, "comuna", form.Comuna);
                break;

            case 2:
                GuardarCampo(datos, "madre: nombre apellido", form.Madre_Nombre_Apellido);
                GuardarCampo(datos, "madre: cédula identidad", form.Madre_Cedula_Identidad);
                GuardarCampo(datos, "madre: teléfono", form.Madre_Telefono);
                GuardarCampo(datos, "madre: escolaridad", form.Madre_Escolaridad);
                GuardarCampo(datos, "madre: domicilio", form.Madre_Domicilio);
                GuardarCampo(datos, "madre: correo", form.Madre_Correo);
                GuardarCampo(datos, "padre: nombre apellido", form.Padre_Nombre_Apellido);
                GuardarCampo(datos, "padre: cédula identidad", form.Padre_Cedula_Identidad);
                GuardarCampo(datos, "padre: teléfono", form.Padre_Telefono);
                GuardarCampo(datos, "padre: escolaridad", form.Padre_Escolaridad);
                GuardarCampo(datos, "padre: domicilio", form.Padre_Domicilio);
                GuardarCampo(datos, "padre: correo", form.Padre_Correo);
                GuardarCampo(datos, "restricción madre (SI/NO)", form.Restriccion_Madre);

                // Regla de la ficha: se requiere completar al menos uno de los dos
                // bloques (madre o padre). POR AHORA DESACTIVADA: el colegio está
                // revisando estilos; volver a activarla cuando Dirección la confirme.
                // if (
                //     BloqueVacio(form.Madre_Nombre_Apellido, form.Madre_Cedula_Identidad, form.Madre_Telefono,
                //                 form.Madre_Escolaridad, form.Madre_Domicilio, form.Madre_Correo)
                //     &&
                //     BloqueVacio(form.Padre_Nombre_Apellido, form.Padre_Cedula_Identidad, form.Padre_Telefono,
                //                 form.Padre_Escolaridad, form.Padre_Domicilio, form.Padre_Correo))
                // {
                //     TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
                //     TempData["Aviso"] = "Para continuar, complete al menos los datos de la madre o los del padre.";
                //     return RedirectToAction(nameof(Paso), new { id = 2 });
                // }
                break;

            case 3:
                GuardarCampo(datos, "apoderado principal: nombre apellido", form.Principal_Nombre_Apellido);
                GuardarCampo(datos, "apoderado principal: cédula identidad", form.Principal_Cedula_Identidad);
                GuardarCampo(datos, "apoderado principal: teléfono", form.Principal_Telefono);
                GuardarCampo(datos, "apoderado principal: vínculo", form.Principal_Vinculo);
                GuardarCampo(datos, "apoderado principal: correo", form.Principal_Correo);
                GuardarCampo(datos, "apoderado secundario: nombre apellido", form.Secundario_Nombre_Apellido);
                GuardarCampo(datos, "apoderado secundario: cédula identidad", form.Secundario_Cedula_Identidad);
                GuardarCampo(datos, "apoderado secundario: teléfono", form.Secundario_Telefono);
                GuardarCampo(datos, "apoderado secundario: vínculo", form.Secundario_Vinculo);
                GuardarCampo(datos, "apoderado secundario: correo", form.Secundario_Correo);
                break;

            // Pasos 4 a 6: aún sin campos; solo se conserva el borrador y se avanza.

            case 7:
                // Confirmación estructural: sin declaración de veracidad ni envío real todavía.
                TempData["Aviso"] = "Estructura completa. El envío real se implementará junto con la persistencia en PostgreSQL.";
                return RedirectToAction(nameof(Paso), new { id = 7 });
        }

        TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
        return RedirectToAction(nameof(Paso), new { id = paso + 1 });
    }

    private Dictionary<string, string> LeerBorrador()
    {
        var json = TempData.Peek(ClaveBorrador) as string;
        if (string.IsNullOrEmpty(json))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static void GuardarCampo(Dictionary<string, string> datos, string clave, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            datos[clave] = valor.Trim();
        }
    }

    /// <summary>Un bloque (madre o padre) queda vacío cuando no se escribió nada en él.</summary>
    private static bool BloqueVacio(params string?[] campos)
    {
        return campos.All(string.IsNullOrWhiteSpace);
    }
}
