using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Estructura del wizard de la ficha de matrícula (mockup 02 · ficha-pasos-apoderado).
/// Cuatro pasos con borrador temporal en TempData: aún no hay persistencia en
/// PostgreSQL ni validación de campos. Los bloques definitivos y sus campos
/// obligatorios se definen con Dirección y Secretaría (Flujo B).
/// </summary>
[Authorize]
public class FichaController : Controller
{
    private const string ClaveBorrador = "Ficha";

    /// <summary>Año escolar que se está matriculando (paso 1 · campo de sólo lectura).</summary>
    private const int AnioMatricula = 2027;

    private readonly MatriculaDbContext _db;

    public FichaController(MatriculaDbContext db) => _db = db;

    // El parámetro de ruta por defecto se llama 'id' (Program.cs), así que se ocupa ese nombre:
    // GET /Ficha/Paso/{pasito mostrado}
    [HttpGet]
    [Authorize(Policy = "Permiso:GENERAL:LEER")]
    public IActionResult Paso(int? id)
    {
        var paso = (id ?? 1) switch { < 1 => 1, > 7 => 7, var n => n };

        var datos = LeerBorrador();

        ViewData["Paso"] = paso;
        ViewData["Borrador"] = datos;
        ViewData["Modo"] = datos.GetValueOrDefault("modo");
        ViewData["ModoDetalle"] = datos.GetValueOrDefault("modo: detalle");

        return View($"Paso{paso}", FormularioDesdeBorrador(datos));
    }

    /// <summary>
    /// Estructural por ahora: guarda los campos del paso en un borrador temporal
    /// y avanza. Los datos no se persisten en la base todavía.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Permiso:GENERAL:CREAR")]
    [ValidateAntiForgeryToken]
    public IActionResult Guardar(int? id, Models.FichaForm form)
    {
        var paso = (id ?? 1) switch { < 1 => 1, > 7 => 7, var n => n };

        var datos = LeerBorrador();

        switch (paso)
        {
            case 1:
                GuardarCampo(datos, "curso: nivel", form.Curso_Nivel);
                GuardarCampo(datos, "curso: letra", form.Curso_Letra);
                GuardarCampo(datos, "año escolar", form.Curso_Anio);
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

            case 4:
                GuardarCampo(datos, "contacto emergencia 01: identificación", form.Contacto1_Identificacion);
                GuardarCampo(datos, "contacto emergencia 01: teléfono", form.Contacto1_Telefono);
                GuardarCampo(datos, "contacto emergencia 01: vínculo", form.Contacto1_Vinculo);
                GuardarCampo(datos, "contacto emergencia 02: identificación", form.Contacto2_Identificacion);
                GuardarCampo(datos, "contacto emergencia 02: teléfono", form.Contacto2_Telefono);
                GuardarCampo(datos, "contacto emergencia 02: vínculo", form.Contacto2_Vinculo);
                break;

            case 5:
                GuardarCampo(datos, "salida solo al finalizar jornada (SI/NO)", form.SalidaSolo);
                GuardarCampo(datos, "retiro autorizado 01: rut", form.Retiro1_Rut);
                GuardarCampo(datos, "retiro autorizado 01: vínculo", form.Retiro1_Vinculo);
                GuardarCampo(datos, "retiro autorizado 02: rut", form.Retiro2_Rut);
                GuardarCampo(datos, "retiro autorizado 02: vínculo", form.Retiro2_Vinculo);
                GuardarCampo(datos, "retiro autorizado 03: rut", form.Retiro3_Rut);
                GuardarCampo(datos, "retiro autorizado 03: vínculo", form.Retiro3_Vinculo);
                break;

            case 6:
                GuardarCampo(datos, "situación médica (SI/NO)", form.TieneSituacionMedica);
                GuardarCampo(datos, "situación médica 01: identificación", form.Medica1_Identificacion);
                GuardarCampo(datos, "situación médica 01: detalle", form.Medica1_Situacion);
                GuardarCampo(datos, "situación médica 01: en tratamiento", form.Medica1_Tratamiento);
                GuardarCampo(datos, "situación médica 02: identificación", form.Medica2_Identificacion);
                GuardarCampo(datos, "situación médica 02: detalle", form.Medica2_Situacion);
                GuardarCampo(datos, "situación médica 02: en tratamiento", form.Medica2_Tratamiento);
                GuardarCampo(datos, "situación médica 03: identificación", form.Medica3_Identificacion);
                GuardarCampo(datos, "situación médica 03: detalle", form.Medica3_Situacion);
                GuardarCampo(datos, "situación médica 03: en tratamiento", form.Medica3_Tratamiento);
                GuardarCampo(datos, "salida al baño (SI/NO)", form.SalidaBano);
                GuardarCampo(datos, "autorización de paracetamol (SI/NO)", form.AutorizaParacetamol);
                break;

            case 7:
                // Confirmación estructural: sin declaración de veracidad ni envío real todavía.
                TempData["Aviso"] = "Estructura completa. El envío real se implementará junto con la persistencia en PostgreSQL.";
                return RedirectToAction(nameof(Paso), new { id = 7 });
        }

        TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
        return RedirectToAction(nameof(Paso), new { id = paso + 1 });
    }

    /// <summary>
    /// Punto de entrada del flujo: busca al estudiante por RUN antes de abrir la
    /// ficha, para decidir entre matrícula nueva y renovación (MAT-01 y MAT-02).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Permiso:GENERAL:LEER")]
    public async Task<IActionResult> Buscar(string? rut)
    {
        var modelo = new BusquedaFicha { Rut = rut };

        if (string.IsNullOrWhiteSpace(rut))
        {
            return View(modelo);
        }

        modelo.Buscado = true;
        var identificador = NormalizarRut(rut);

        var estudiante = await _db.Estudiantes
            .Include(e => e.Comuna)
            .FirstOrDefaultAsync(e => e.Identificador == identificador);

        if (estudiante is null)
        {
            return View(modelo);
        }

        modelo.EstudianteId = estudiante.Id;
        modelo.NombreCompleto = estudiante.NombreCompleto;

        var matriculas = await _db.Matriculas
            .Include(m => m.Curso)
            .Where(m => m.EstudianteId == estudiante.Id)
            .OrderByDescending(m => m.Anio)
            .ToListAsync();

        var vigente = matriculas.FirstOrDefault(m => m.Anio == AnioMatricula);
        var anterior = matriculas
            .Where(m => m.Anio < AnioMatricula)
            .OrderByDescending(m => m.Anio)
            .FirstOrDefault();

        modelo.AnioAnterior = anterior?.Anio;
        modelo.CursoAnterior = anterior?.Curso?.Nombre;

        // MAT-02: si la matrícula del año ya está cerrada, no se crea otro expediente.
        if (vigente is not null && MatriculaCerrada(vigente.Estado))
        {
            modelo.EstadoVigente = vigente.Estado;
            return View(modelo);
        }

        modelo.PuedeRenovar = anterior is not null;
        modelo.PuedeInscribir = anterior is null;

        return View(modelo);
    }

    /// <summary>
    /// Renovación: abre un expediente nuevo del año copiando los datos del
    /// estudiante y el curso anterior. La matrícula previa nunca se sobrescribe.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Permiso:GENERAL:CREAR")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renovar(int id)
    {
        var estudiante = await _db.Estudiantes
            .Include(e => e.Comuna)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (estudiante is null)
        {
            TempData["Aviso"] = "No se encontró el estudiante seleccionado.";
            return RedirectToAction(nameof(Buscar));
        }

        var anterior = await _db.Matriculas
            .Include(m => m.Curso)
            .Where(m => m.EstudianteId == id && m.Anio < AnioMatricula)
            .OrderByDescending(m => m.Anio)
            .FirstOrDefaultAsync();

        var datos = new Dictionary<string, string>();
        GuardarCampo(datos, "nombres", estudiante.Nombres);
        GuardarCampo(datos, "apellido paterno", estudiante.ApellidoPaterno);
        GuardarCampo(datos, "apellido materno", estudiante.ApellidoMaterno);
        GuardarCampo(datos, "cédula de identidad", estudiante.Identificador);
        GuardarCampo(datos, "fecha nacimiento", estudiante.FechaNacimiento.ToString("yyyy-MM-dd"));
        GuardarCampo(datos, "nacionalidad", estudiante.Nacionalidad);
        GuardarCampo(datos, "domicilio", estudiante.Domicilio);
        GuardarCampo(datos, "comuna", estudiante.Comuna?.Nombre);

        var (nivel, letra) = SepararCurso(anterior?.Curso?.Nombre);
        GuardarCampo(datos, "curso: nivel", nivel);
        GuardarCampo(datos, "curso: letra", letra);
        GuardarCampo(datos, "año escolar", AnioMatricula.ToString());

        datos["modo"] = "Renovación";
        datos["modo: detalle"] = anterior is null
            ? $"Expediente nuevo {AnioMatricula}; el estudiante no tenía matrícula anterior."
            : $"Expediente nuevo {AnioMatricula} a partir de la matrícula {anterior.Anio}"
              + (string.IsNullOrWhiteSpace(anterior.Curso?.Nombre) ? "." : $" ({anterior.Curso!.Nombre}).");

        TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
        return RedirectToAction(nameof(Paso), new { id = 1 });
    }

    /// <summary>Matrícula nueva para un estudiante sin matrícula anterior.</summary>
    [HttpPost]
    [Authorize(Policy = "Permiso:GENERAL:CREAR")]
    [ValidateAntiForgeryToken]
    public IActionResult Nueva()
    {
        var datos = new Dictionary<string, string>
        {
            ["modo"] = "Matrícula nueva",
            ["modo: detalle"] = $"Expediente nuevo {AnioMatricula} sin matrícula anterior.",
            ["año escolar"] = AnioMatricula.ToString(),
        };

        TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
        return RedirectToAction(nameof(Paso), new { id = 1 });
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

    /// <summary>Estados en que la matrícula del año ya no admite un expediente nuevo.</summary>
    private static bool MatriculaCerrada(string estado) =>
        estado is "FIRMADA" or "CONFIRMADA" or "FORMALIZADA";

    /// <summary>Deja el RUN como lo guarda la base: sin puntos, sin espacios y con guion.</summary>
    private static string NormalizarRut(string rut)
    {
        var limpio = rut.Replace(".", string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();

        return limpio.Contains('-') || limpio.Length < 2
            ? limpio
            : $"{limpio[..^1]}-{limpio[^1]}";
    }

    /// <summary>«1° A» → («1º básico», «A»). El curso anterior se propone, no se fija.</summary>
    private static (string? nivel, string? letra) SepararCurso(string? curso)
    {
        if (string.IsNullOrWhiteSpace(curso))
        {
            return (null, null);
        }

        var partes = curso.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var nivel = partes.Length > 0 ? NivelDesdeGrado(partes[0]) : null;
        var letra = partes.Length > 1 ? partes[1] : null;

        return (nivel, letra);
    }

    private static string? NivelDesdeGrado(string grado)
    {
        var numero = new string(grado.Where(char.IsDigit).ToArray());

        return numero switch
        {
            "1" => "1º básico",
            "2" => "2º básico",
            "3" => "3º básico",
            "4" => "4º básico",
            "5" => "5º básico",
            "6" => "6º básico",
            "7" => "7º básico",
            "8" => "8º básico",
            _ => null,
        };
    }

    /// <summary>Vuelve a llenar el formulario con lo que ya estaba en el borrador.</summary>
    private static FichaForm FormularioDesdeBorrador(Dictionary<string, string> datos) => new()
    {
        Curso_Nivel = datos.GetValueOrDefault("curso: nivel"),
        Curso_Letra = datos.GetValueOrDefault("curso: letra"),
        Curso_Anio = datos.GetValueOrDefault("año escolar") ?? AnioMatricula.ToString(),
        Nombres = datos.GetValueOrDefault("nombres"),
        Apellido_Paterno = datos.GetValueOrDefault("apellido paterno"),
        Apellido_Materno = datos.GetValueOrDefault("apellido materno"),
        Cedula_Identidad = datos.GetValueOrDefault("cédula de identidad"),
        Fecha_Nacimiento = datos.GetValueOrDefault("fecha nacimiento"),
        Nacionalidad = datos.GetValueOrDefault("nacionalidad"),
        Domicilio = datos.GetValueOrDefault("domicilio"),
        Comuna = datos.GetValueOrDefault("comuna"),
    };

    /// <summary>Un bloque (madre o padre) queda vacío cuando no se escribió nada en él.</summary>
    private static bool BloqueVacio(params string?[] campos)
    {
        return campos.All(string.IsNullOrWhiteSpace);
    }
}
