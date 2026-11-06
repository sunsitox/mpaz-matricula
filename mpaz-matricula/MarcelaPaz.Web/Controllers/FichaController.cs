using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Infrastructure.Documentos;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Asistente de la ficha de matrícula (mockup 02 · ficha-pasos-apoderado).
/// Cada paso se guarda en PostgreSQL a través de <see cref="RegistroFicha"/> y el
/// borrador se conserva en TempData para poder navegar entre pasos. Sin validación
/// de campos: los bloques definitivos y sus campos obligatorios se definen con
/// Dirección y Secretaría (Flujo B).
/// </summary>
[Authorize]
public class FichaController : Controller
{
    private const string ClaveBorrador = "Ficha";

    /// <summary>Año escolar que se está matriculando (paso 1 · campo de sólo lectura).</summary>
    private const int AnioMatricula = 2027;

    /// <summary>Escudo institucional con lema que se imprime en la ficha.</summary>
    private const string RutaLogo = "images/logo-mpaz-con-lema.png";

    private readonly MatriculaDbContext _db;
    private readonly FichaMatriculaPdf _pdf;
    private readonly IWebHostEnvironment _entorno;
    private readonly RegistroFicha _registro;

    public FichaController(
        MatriculaDbContext db,
        FichaMatriculaPdf pdf,
        IWebHostEnvironment entorno,
        RegistroFicha registro)
    {
        _db = db;
        _pdf = pdf;
        _entorno = entorno;
        _registro = registro;
    }

    // El parámetro de ruta por defecto se llama 'id' (Program.cs), así que se ocupa ese nombre:
    // GET /Ficha/Paso/{pasito mostrado}
    [HttpGet]
    [Authorize(Policy = "Permiso:GENERAL:LEER")]
    public async Task<IActionResult> Paso(int? id)
    {
        var paso = (id ?? 1) switch { < 1 => 1, > 7 => 7, var n => n };

        var datos = LeerBorrador();

        // La ficha de un estudiante ya registrado se abre con sus apoderados,
        // contactos, retiros y datos médicos tomados de la base.
        if (await CompletarDesdeBaseAsync(datos))
        {
            TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
        }

        MostrarPaso(paso, datos);

        return View($"Paso{paso}", FormularioDesdeBorrador(datos));
    }

    /// <summary>
    /// Guarda los campos del paso: primero en el borrador temporal y luego en las
    /// tablas del DDL institucional, y avanza al paso siguiente.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Permiso:GENERAL:CREAR")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(int? id, Models.FichaForm form)
    {
        var paso = (id ?? 1) switch { < 1 => 1, > 7 => 7, var n => n };

        var datos = LeerBorrador();

        // Se revisa el formato (RUN con o sin puntos, correos) antes de escribir.
        if (!ModelState.IsValid)
        {
            MostrarPaso(paso, datos);
            TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
            return View($"Paso{paso}", form);
        }

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
                GuardarCampo(datos, "PIE: participa (SI/NO/PENDIENTE)", form.ParticipaPie);
                GuardarCampo(datos, "PIE: diagnosticado (SI/NO)", form.PieDiagnostico);
                GuardarCampo(datos, "PIE: apoyos o diagnóstico", form.PieApoyos);
                GuardarCampo(datos, "autorización de uso de imagen (SI/NO)", form.AutorizaImagen);
                GuardarCampo(datos, "autorización de convivencia escolar (SI/NO)", form.AutorizaConvivencia);
                break;

            case 7:
                // La revisión exige la confirmación expresa de que se leyó la ficha.
                if (!form.Confirmo_Revision)
                {
                    TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
                    TempData["Aviso"] = "Para enviar la ficha a revisión, marque la confirmación de que revisó todos los datos.";
                    return RedirectToAction(nameof(Paso), new { id = 7 });
                }

                // La ficha se registra con folio recién cuando exista la sesión del
                // funcionario que la genera (ficha_version.generada_por).
                TempData["NotificacionTitulo"] = "Ficha guardada";
                TempData["Notificacion"] = "Los datos quedaron registrados en la base y la ficha pasa a revisión. La Secretaría emitirá el folio al cerrar la revisión.";
                TempData["NotificacionTipo"] = "exito";

                // Al confirmar, la ficha vuelve a la ficha del estudiante: ahí se ve el
                // expediente completo en lugar de dejar al usuario en el paso 7.
                var rutBorrador = datos.GetValueOrDefault("cédula de identidad");
                var rutEstudiante = string.IsNullOrWhiteSpace(rutBorrador)
                    ? string.Empty
                    : NormalizarRut(rutBorrador!);

                var alumnoId = string.IsNullOrWhiteSpace(rutEstudiante)
                    ? (int?)null
                    : await _db.Estudiantes
                        .Where(e => e.Identificador == rutEstudiante)
                        .Select(e => (int?)e.Id)
                        .FirstOrDefaultAsync();

                if (alumnoId is int idAlumno)
                {
                    return RedirectToAction("Detalle", "Alumnos", new { id = idAlumno });
                }

                TempData["Notificacion"] = "La ficha quedó guardada, pero no se pudo identificar al estudiante en la base. Búsquelo por RUN para ver su ficha.";
                return RedirectToAction(nameof(Buscar));
        }

        var aviso = await _registro.AplicarAsync(paso, datos, AnioMatricula);

        TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);

        if (aviso is not null)
        {
            TempData["Aviso"] = aviso;
        }

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
        var modelo = new BusquedaFicha { Rut = RutChileno.Formatear(rut) };

        if (string.IsNullOrWhiteSpace(rut))
        {
            return View(modelo);
        }

        if (!RutChileno.EsValido(rut))
        {
            modelo.Error = "El RUN no es válido: revise el número y el dígito verificador.";
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

    /// <summary>
    /// Ficha imprimible para firma (MAT-08): folio, año, versión y bloques de
    /// firma. Se arma con el borrador actual, sin registrar todavía la matrícula.
    /// Sin <paramref name="descargar"/> se muestra en el navegador; con él se
    /// descarga como archivo.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Permiso:GENERAL:LEER")]
    public async Task<IActionResult> Comprobante(bool descargar = false)
    {
        var datos = LeerBorrador();

        // La ficha para firma muestra también lo que ya está en la base.
        await CompletarDesdeBaseAsync(datos);

        if (datos.Count == 0)
        {
            TempData["Aviso"] = "Aún no hay datos en el borrador: complete el formulario antes de generar la ficha.";
            return RedirectToAction(nameof(Paso), new { id = 1 });
        }

        var ficha = FichaImpresion.DesdeBorrador(datos, AnioMatricula, DateTime.Now, LogoInstitucional());
        var archivo = _pdf.Generar(ficha);

        return descargar
            ? File(archivo, "application/pdf", $"ficha-matricula-{ficha.Folio}.pdf")
            : File(archivo, "application/pdf");
    }

    /// <summary>
    /// Formulario de matrícula de un alumno ya registrado, para descargar desde su
    /// ficha. Se arma con los datos de la base y se imprime con el mismo PDF
    /// institucional de la ficha (MAT-08).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Permiso:GENERAL:LEER")]
    public async Task<IActionResult> FormularioAlumno(int id, bool descargar = true)
    {
        var estudiante = await _db.Estudiantes
            .AsNoTracking()
            .Include(e => e.Comuna)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (estudiante is null)
        {
            return NotFound();
        }

        var matriculas = await _db.Matriculas
            .AsNoTracking()
            .Include(m => m.Curso)
            .Where(m => m.EstudianteId == id)
            .OrderByDescending(m => m.Anio)
            .ToListAsync();

        // Se imprime la matrícula del año en curso; si aún no existe, la última registrada.
        var matricula = matriculas.FirstOrDefault(m => m.Anio == AnioMatricula)
            ?? matriculas.FirstOrDefault();

        var anio = matricula?.Anio ?? AnioMatricula;

        var datos = new Dictionary<string, string>
        {
            ["cédula de identidad"] = estudiante.Identificador,
            ["nombres"] = estudiante.Nombres,
            ["apellido paterno"] = estudiante.ApellidoPaterno,
            ["apellido materno"] = estudiante.ApellidoMaterno,
            ["año escolar"] = anio.ToString(),
            ["modo"] = $"Matrícula {anio}",
        };

        if (estudiante.FechaNacimiento != default)
        {
            GuardarCampo(datos, "fecha nacimiento", estudiante.FechaNacimiento.ToString("dd-MM-yyyy"));
        }

        GuardarCampo(datos, "nacionalidad", estudiante.Nacionalidad);
        GuardarCampo(datos, "domicilio", estudiante.Domicilio);
        GuardarCampo(datos, "comuna", estudiante.Comuna?.Nombre);

        var (nivel, letra) = SepararCurso(matricula?.Curso?.Nombre);
        GuardarCampo(datos, "curso: nivel", nivel);
        GuardarCampo(datos, "curso: letra", letra);

        datos["modo: detalle"] = matricula is null
            ? $"El alumno no tiene matrícula {AnioMatricula} registrada; la ficha se genera con sus datos personales."
            : $"Matrícula {matricula.Anio}"
              + (string.IsNullOrWhiteSpace(matricula.Curso?.Nombre) ? string.Empty : $" · {matricula.Curso!.Nombre}")
              + $" · {matricula.Estado.ToLowerInvariant()}.";

        // Apoderados, contactos, retiros y salud salen de la base, igual que en el asistente.
        await CompletarDesdeBaseAsync(datos);

        var version = matricula is null
            ? $"sin matrícula {AnioMatricula} registrada"
            : $"matrícula {matricula.Anio} · {matricula.Estado.ToLowerInvariant()}";

        var ficha = FichaImpresion.DesdeBorrador(datos, anio, DateTime.Now, LogoInstitucional(), version);
        var archivo = _pdf.Generar(ficha);
        var nombre = $"formulario-matricula-{estudiante.Identificador}-{anio}.pdf";

        return descargar
            ? File(archivo, "application/pdf", nombre)
            : File(archivo, "application/pdf");
    }

    /// <summary>Lee el escudo desde wwwroot; si falta, la ficha se genera sin logo.</summary>
    private byte[]? LogoInstitucional()
    {
        var ruta = Path.Combine(_entorno.WebRootPath, RutaLogo);

        return System.IO.File.Exists(ruta) ? System.IO.File.ReadAllBytes(ruta) : null;
    }

    /// <summary>Datos que la vista del paso necesita para dibujar el encabezado y el avance.</summary>
    private void MostrarPaso(int paso, Dictionary<string, string> datos)
    {
        ViewData["Paso"] = paso;
        ViewData["Borrador"] = datos;
        ViewData["Modo"] = datos.GetValueOrDefault("modo");
        ViewData["ModoDetalle"] = datos.GetValueOrDefault("modo: detalle");
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
        if (string.IsNullOrWhiteSpace(valor))
        {
            return;
        }

        // La base guarda el RUN sin puntos; la ficha impresa lo muestra con puntos.
        datos[clave] = ClavesRut.EsClave(clave) ? RutChileno.Limpiar(valor) : valor.Trim();
    }

    /// <summary>Estados en que la matrícula del año ya no admite un expediente nuevo.</summary>
    private static bool MatriculaCerrada(string estado) =>
        estado is "FIRMADA" or "CONFIRMADA" or "FORMALIZADA";

    /// <summary>Deja el RUN como lo guarda la base: sin puntos, sin espacios y con guion.</summary>
    private static string NormalizarRut(string rut) => RutChileno.Limpiar(rut);

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

    /// <summary>
    /// Completa los pasos 2 a 6 del borrador con lo que ya está guardado del
    /// estudiante (madre, padre, apoderados, contactos de emergencia, retiros y
    /// datos médicos). Sólo rellena lo que falta: nunca pisa lo escrito en el
    /// asistente. Devuelve true si agregó algún dato.
    /// </summary>
    private async Task<bool> CompletarDesdeBaseAsync(Dictionary<string, string> datos)
    {
        var rut = datos.GetValueOrDefault("cédula de identidad");

        if (string.IsNullOrWhiteSpace(rut))
        {
            return false;
        }

        var identificador = NormalizarRut(rut);
        var estudiante = await _db.Estudiantes
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Identificador == identificador);

        if (estudiante is null)
        {
            return false;
        }

        var agregados = 0;

        // Paso 5 · ¿puede salir solo al finalizar la jornada?
        if (estudiante.SaleSoloFinalJornada is bool saleSolo)
        {
            agregados += PonerSiFalta(datos, "salida solo al finalizar jornada (SI/NO)", saleSolo ? "Si" : "No");
        }

        // Pasos 2 y 3 · madre, padre y apoderados vigentes.
        var vinculos = await _db.EstudiantesPersonas
            .AsNoTracking()
            .Include(v => v.Persona)
            .Where(v => v.EstudianteId == estudiante.Id && v.VigenteHasta == null)
            .OrderBy(v => v.Id)
            .ToListAsync();

        var madre = vinculos.FirstOrDefault(v => v.Parentesco == "MADRE");
        var padre = vinculos.FirstOrDefault(v => v.Parentesco == "PADRE");

        if (madre?.Persona is not null)
        {
            agregados += PonerSiFalta(datos, "madre: nombre apellido", NombreCompleto(madre.Persona));
            agregados += PonerSiFalta(datos, "madre: cédula identidad", madre.Persona.Identificador);
            agregados += PonerSiFalta(datos, "madre: teléfono", madre.Persona.Telefono);
            agregados += PonerSiFalta(datos, "madre: escolaridad", madre.Persona.Escolaridad);
            agregados += PonerSiFalta(datos, "madre: domicilio", madre.Persona.Domicilio);
            agregados += PonerSiFalta(datos, "madre: correo", madre.Persona.Correo);
        }

        if (padre?.Persona is not null)
        {
            agregados += PonerSiFalta(datos, "padre: nombre apellido", NombreCompleto(padre.Persona));
            agregados += PonerSiFalta(datos, "padre: cédula identidad", padre.Persona.Identificador);
            agregados += PonerSiFalta(datos, "padre: teléfono", padre.Persona.Telefono);
            agregados += PonerSiFalta(datos, "padre: escolaridad", padre.Persona.Escolaridad);
            agregados += PonerSiFalta(datos, "padre: domicilio", padre.Persona.Domicilio);
            agregados += PonerSiFalta(datos, "padre: correo", padre.Persona.Correo);
        }

        agregados += AgregarApoderado(datos, "apoderado principal",
            vinculos.FirstOrDefault(v => v.TipoApoderado == "PRINCIPAL"));
        agregados += AgregarApoderado(datos, "apoderado secundario",
            vinculos.FirstOrDefault(v => v.TipoApoderado == "SECUNDARIO"));

        if (vinculos.Any(v => v.TieneRestriccion == true))
        {
            agregados += PonerSiFalta(datos, "restricción madre (SI/NO)", "Si");
        }
        else if (vinculos.Any(v => v.TieneRestriccion == false))
        {
            agregados += PonerSiFalta(datos, "restricción madre (SI/NO)", "No");
        }

        // Paso 4 · contactos de emergencia.
        var contactos = await _db.ContactosEmergencia
            .AsNoTracking()
            .Where(c => c.EstudianteId == estudiante.Id && c.VigenteHasta == null)
            .OrderBy(c => c.Orden)
            .Take(2)
            .ToListAsync();

        var numeroContacto = 1;
        foreach (var contacto in contactos)
        {
            var prefijo = $"contacto emergencia {numeroContacto:00}";
            agregados += PonerSiFalta(datos, $"{prefijo}: identificación", contacto.Nombre);
            agregados += PonerSiFalta(datos, $"{prefijo}: teléfono", contacto.Telefono);
            agregados += PonerSiFalta(datos, $"{prefijo}: vínculo", contacto.Vinculo);
            numeroContacto++;
        }

        // Paso 5 · personas autorizadas a retirar.
        var retiros = await _db.RetirosAutorizados
            .AsNoTracking()
            .Where(r => r.EstudianteId == estudiante.Id && r.VigenteHasta == null)
            .OrderBy(r => r.Id)
            .Take(3)
            .ToListAsync();

        var numeroRetiro = 1;
        foreach (var retiro in retiros)
        {
            agregados += PonerSiFalta(datos, $"retiro autorizado {numeroRetiro:00}: rut", retiro.Identificador);
            agregados += PonerSiFalta(datos, $"retiro autorizado {numeroRetiro:00}: vínculo", retiro.Vinculo);
            numeroRetiro++;
        }

        // Paso 6 · salud declarada.
        var salud = await _db.AntecedentesSalud
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EstudianteId == estudiante.Id);

        if (salud?.TieneSituacionMedica is bool tieneSituacion)
        {
            agregados += PonerSiFalta(datos, "situación médica (SI/NO)", tieneSituacion ? "Si" : "No");
        }

        if (!string.IsNullOrWhiteSpace(salud?.NecesidadesAtencion))
        {
            if (salud.NecesidadesAtencion.Contains(": SI", StringComparison.Ordinal))
            {
                agregados += PonerSiFalta(datos, "salida al baño (SI/NO)", "Si");
            }
            else if (salud.NecesidadesAtencion.Contains(": NO", StringComparison.Ordinal))
            {
                agregados += PonerSiFalta(datos, "salida al baño (SI/NO)", "No");
            }
        }

        var situaciones = await _db.SituacionesMedicas
            .AsNoTracking()
            .Where(s => s.EstudianteId == estudiante.Id && s.VigenteHasta == null)
            .OrderBy(s => s.Id)
            .Take(3)
            .ToListAsync();

        var numeroSituacion = 1;
        foreach (var situacion in situaciones)
        {
            var (identificacion, detalle) = SepararDescripcion(situacion.Descripcion);
            agregados += PonerSiFalta(datos, $"situación médica {numeroSituacion:00}: identificación", identificacion);
            agregados += PonerSiFalta(datos, $"situación médica {numeroSituacion:00}: detalle", detalle);

            if (situacion.EnTratamiento is bool enTratamiento)
            {
                agregados += PonerSiFalta(datos, $"situación médica {numeroSituacion:00}: en tratamiento", enTratamiento ? "Si" : "No");
            }

            numeroSituacion++;
        }

        // Paso 6 · PIE y apoyos.
        var apoyo = await _db.AntecedentesApoyo
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EstudianteId == estudiante.Id);

        if (apoyo is not null)
        {
            agregados += PonerSiFalta(datos, "PIE: participa (SI/NO/PENDIENTE)", apoyo.ParticipaPie switch
            {
                "SI" => "Si",
                "NO" => "No",
                _ => "Pendiente",
            });

            if (apoyo.TieneDiagnostico is bool tieneDiagnostico)
            {
                agregados += PonerSiFalta(datos, "PIE: diagnosticado (SI/NO)", tieneDiagnostico ? "Si" : "No");
            }

            agregados += PonerSiFalta(datos, "PIE: apoyos o diagnóstico", apoyo.ApoyosInformados);
        }

        return agregados > 0;
    }

    /// <summary>Agrega el bloque de un apoderado desde su vínculo con el estudiante.</summary>
    private static int AgregarApoderado(
        Dictionary<string, string> datos,
        string prefijo,
        EstudiantePersona? vinculo)
    {
        if (vinculo?.Persona is null)
        {
            return 0;
        }

        return PonerSiFalta(datos, $"{prefijo}: nombre apellido", NombreCompleto(vinculo.Persona))
             + PonerSiFalta(datos, $"{prefijo}: cédula identidad", vinculo.Persona.Identificador)
             + PonerSiFalta(datos, $"{prefijo}: teléfono", vinculo.Persona.Telefono)
             + PonerSiFalta(datos, $"{prefijo}: vínculo", EtiquetaVinculo(vinculo))
             + PonerSiFalta(datos, $"{prefijo}: correo", vinculo.Persona.Correo);
    }

    /// <summary>Agrega una clave al borrador sólo si venía vacía.</summary>
    private static int PonerSiFalta(Dictionary<string, string> datos, string clave, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || datos.ContainsKey(clave))
        {
            return 0;
        }

        datos[clave] = valor.Trim();
        return 1;
    }

    private static string NombreCompleto(Persona persona) =>
        $"{persona.Nombres} {persona.Apellidos}".Trim();

    /// <summary>«MADRE» → «Madre»; para «OTRO» se usa el vínculo declarado (por ejemplo, abuela).</summary>
    private static string? EtiquetaVinculo(EstudiantePersona vinculo) => vinculo.Parentesco switch
    {
        "MADRE" => "Madre",
        "PADRE" => "Padre",
        _ => vinculo.Vinculo,
    };

    /// <summary>«Asma: usa inhalador» → («Asma», «usa inhalador»).</summary>
    private static (string? identificacion, string? detalle) SepararDescripcion(string descripcion)
    {
        var corte = descripcion.IndexOf(": ", StringComparison.Ordinal);

        return corte < 0
            ? (descripcion, null)
            : (descripcion[..corte], descripcion[(corte + 2)..]);
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
        Cedula_Identidad = RutChileno.Formatear(datos.GetValueOrDefault("cédula de identidad")),
        Fecha_Nacimiento = datos.GetValueOrDefault("fecha nacimiento"),
        Nacionalidad = datos.GetValueOrDefault("nacionalidad"),
        Domicilio = datos.GetValueOrDefault("domicilio"),
        Comuna = datos.GetValueOrDefault("comuna"),

        Madre_Nombre_Apellido = datos.GetValueOrDefault("madre: nombre apellido"),
        Madre_Cedula_Identidad = RutChileno.Formatear(datos.GetValueOrDefault("madre: cédula identidad")),
        Madre_Telefono = datos.GetValueOrDefault("madre: teléfono"),
        Madre_Escolaridad = datos.GetValueOrDefault("madre: escolaridad"),
        Madre_Domicilio = datos.GetValueOrDefault("madre: domicilio"),
        Madre_Correo = datos.GetValueOrDefault("madre: correo"),

        Padre_Nombre_Apellido = datos.GetValueOrDefault("padre: nombre apellido"),
        Padre_Cedula_Identidad = RutChileno.Formatear(datos.GetValueOrDefault("padre: cédula identidad")),
        Padre_Telefono = datos.GetValueOrDefault("padre: teléfono"),
        Padre_Escolaridad = datos.GetValueOrDefault("padre: escolaridad"),
        Padre_Domicilio = datos.GetValueOrDefault("padre: domicilio"),
        Padre_Correo = datos.GetValueOrDefault("padre: correo"),

        Restriccion_Madre = datos.GetValueOrDefault("restricción madre (SI/NO)"),

        Principal_Nombre_Apellido = datos.GetValueOrDefault("apoderado principal: nombre apellido"),
        Principal_Cedula_Identidad = RutChileno.Formatear(datos.GetValueOrDefault("apoderado principal: cédula identidad")),
        Principal_Telefono = datos.GetValueOrDefault("apoderado principal: teléfono"),
        Principal_Vinculo = datos.GetValueOrDefault("apoderado principal: vínculo"),
        Principal_Correo = datos.GetValueOrDefault("apoderado principal: correo"),

        Secundario_Nombre_Apellido = datos.GetValueOrDefault("apoderado secundario: nombre apellido"),
        Secundario_Cedula_Identidad = RutChileno.Formatear(datos.GetValueOrDefault("apoderado secundario: cédula identidad")),
        Secundario_Telefono = datos.GetValueOrDefault("apoderado secundario: teléfono"),
        Secundario_Vinculo = datos.GetValueOrDefault("apoderado secundario: vínculo"),
        Secundario_Correo = datos.GetValueOrDefault("apoderado secundario: correo"),

        Contacto1_Identificacion = datos.GetValueOrDefault("contacto emergencia 01: identificación"),
        Contacto1_Telefono = datos.GetValueOrDefault("contacto emergencia 01: teléfono"),
        Contacto1_Vinculo = datos.GetValueOrDefault("contacto emergencia 01: vínculo"),

        Contacto2_Identificacion = datos.GetValueOrDefault("contacto emergencia 02: identificación"),
        Contacto2_Telefono = datos.GetValueOrDefault("contacto emergencia 02: teléfono"),
        Contacto2_Vinculo = datos.GetValueOrDefault("contacto emergencia 02: vínculo"),

        SalidaSolo = datos.GetValueOrDefault("salida solo al finalizar jornada (SI/NO)"),

        Retiro1_Rut = RutChileno.Formatear(datos.GetValueOrDefault("retiro autorizado 01: rut")),
        Retiro1_Vinculo = datos.GetValueOrDefault("retiro autorizado 01: vínculo"),

        Retiro2_Rut = RutChileno.Formatear(datos.GetValueOrDefault("retiro autorizado 02: rut")),
        Retiro2_Vinculo = datos.GetValueOrDefault("retiro autorizado 02: vínculo"),

        Retiro3_Rut = RutChileno.Formatear(datos.GetValueOrDefault("retiro autorizado 03: rut")),
        Retiro3_Vinculo = datos.GetValueOrDefault("retiro autorizado 03: vínculo"),

        TieneSituacionMedica = datos.GetValueOrDefault("situación médica (SI/NO)"),

        Medica1_Identificacion = datos.GetValueOrDefault("situación médica 01: identificación"),
        Medica1_Situacion = datos.GetValueOrDefault("situación médica 01: detalle"),
        Medica1_Tratamiento = datos.GetValueOrDefault("situación médica 01: en tratamiento"),

        Medica2_Identificacion = datos.GetValueOrDefault("situación médica 02: identificación"),
        Medica2_Situacion = datos.GetValueOrDefault("situación médica 02: detalle"),
        Medica2_Tratamiento = datos.GetValueOrDefault("situación médica 02: en tratamiento"),

        Medica3_Identificacion = datos.GetValueOrDefault("situación médica 03: identificación"),
        Medica3_Situacion = datos.GetValueOrDefault("situación médica 03: detalle"),
        Medica3_Tratamiento = datos.GetValueOrDefault("situación médica 03: en tratamiento"),

        SalidaBano = datos.GetValueOrDefault("salida al baño (SI/NO)"),
        AutorizaParacetamol = datos.GetValueOrDefault("autorización de paracetamol (SI/NO)"),
    };

    /// <summary>Un bloque (madre o padre) queda vacío cuando no se escribió nada en él.</summary>
    private static bool BloqueVacio(params string?[] campos)
    {
        return campos.All(string.IsNullOrWhiteSpace);
    }
}
