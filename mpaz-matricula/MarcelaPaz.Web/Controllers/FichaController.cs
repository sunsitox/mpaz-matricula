using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Infrastructure.Documentos;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Models.Validaciones;
using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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

    /// <summary>Escudo institucional con lema que se imprime en la ficha.</summary>
    private const string RutaLogo = "images/logo-mpaz-con-lema.png";

    private readonly MatriculaDbContext _db;
    private readonly FichaMatriculaPdf _pdf;
    private readonly IWebHostEnvironment _entorno;
    private readonly RegistroFicha _registro;
    private readonly IAuditoriaAcceso _auditoria;
    private readonly AnioEscolar _anios;
    private readonly EscrituraAuditada _escritura;

    public FichaController(
        MatriculaDbContext db,
        FichaMatriculaPdf pdf,
        IWebHostEnvironment entorno,
        RegistroFicha registro,
        IAuditoriaAcceso auditoria,
        AnioEscolar anios,
        EscrituraAuditada escritura)
    {
        _db = db;
        _pdf = pdf;
        _entorno = entorno;
        _registro = registro;
        _auditoria = auditoria;
        _anios = anios;
        _escritura = escritura;
    }

    // El parámetro de ruta por defecto se llama 'id' (Program.cs), así que se ocupa ese nombre:
    // GET /Ficha/Paso/{pasito mostrado}
    [HttpGet]
    [Authorize(Policy = "Permiso:GENERAL:LEER")]
    public async Task<IActionResult> Paso(int? id)
    {
        var paso = (id ?? 1) switch { < 1 => 1, > 8 => 8, var n => n };

        var datos = LeerBorrador();

        var anioActivo = await _anios.ActivoAsync();

        if (anioActivo is null)
        {
            return SinAnioActivo();
        }

        // MAT-18: la corrección parte de la copia congelada, que se carga desde
        // la base para no llevar el snapshot completo en una cookie.
        await CompletarDesdeCorreccionAsync(datos);

        // La ficha de un estudiante ya registrado se abre con sus apoderados,
        // contactos, retiros y datos médicos tomados de la base.
        if (await CompletarDesdeBaseAsync(datos))
        {
            TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
        }

        MostrarPaso(paso, datos);

        return View($"Paso{paso}", FormularioDesdeBorrador(datos, anioActivo.Value));
    }

    /// <summary>
    /// Confirmación intermedia del envío: muestra la rueda y el mensaje antes de
    /// volver a la ficha del estudiante, que se abre sola unos segundos después.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Permiso:GENERAL:LEER")]
    public async Task<IActionResult> Enviada(int id)
    {
        var existe = await _db.Estudiantes.AnyAsync(e => e.Id == id);

        if (!existe)
        {
            return RedirectToAction(nameof(Buscar));
        }

        var anioActivo = await _anios.ActivoAsync();

        if (anioActivo is null)
        {
            return SinAnioActivo();
        }

        var ficha = await _registro.FichaVigenteAsync(id, anioActivo.Value);
        ViewData["Folio"] = ficha?.Folio;
        ViewData["Version"] = ficha?.NumeroVersion;

        return View(id);
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
        var paso = (id ?? 1) switch { < 1 => 1, > 8 => 8, var n => n };

        var datos = LeerBorrador();

        var anioActivo = await _anios.ActivoAsync();

        if (anioActivo is null)
        {
            return SinAnioActivo();
        }

        var anio = anioActivo.Value;

        // MAT-18: si el borrador viene de una corrección, se completa con la copia
        // congelada de la ficha vigente antes de validar y guardar el paso.
        await CompletarDesdeCorreccionAsync(datos);

        // Se revisan formato y reglas de bloque (documento, contactos, salud)
        // antes de escribir.
        ValidarPaso(paso, form);

        // El asistente envía sólo los campos del paso actual. Sin este filtro,
        // las anotaciones [Required] de los pasos anteriores invalidan el POST.
        LimpiarModelStateDeOtrosPasos(paso);

        if (!ModelState.IsValid)
        {
            MostrarPaso(paso, datos);
            TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
            return View($"Paso{paso}", form);
        }

        switch (paso)
        {
            case 1:
                GuardarCampo(datos, "tipo identificador", form.Tipo_Identificador ?? "RUN");
                GuardarDocumento(datos, form);
                GuardarCampo(datos, "curso: nivel", form.Curso_Nivel);
                GuardarCampo(datos, "curso: letra", form.Curso_Letra);
                GuardarCampo(datos, "año escolar", form.Curso_Anio);
                GuardarCampo(datos, "nombres", form.Nombres);
                GuardarCampo(datos, "apellido paterno", form.Apellido_Paterno);
                GuardarCampo(datos, "apellido materno", form.Apellido_Materno);

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
                GuardarCampo(datos, "restricción madre: tipo", form.Restriccion_Madre_Tipo);
                GuardarCampo(datos, "restricción madre: detalle", form.Restriccion_Madre_Detalle);

                // La regla «al menos un bloque de madre o padre» se exige en
                // ValidarPaso antes de guardar.
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
                GuardarCampo(datos, "alergias", form.Alergias);
                GuardarCampo(datos, "contacto sanitario: nombre", form.Contacto_Sanitario_Nombre);
                GuardarCampo(datos, "contacto sanitario: teléfono", form.Contacto_Sanitario_Telefono);
                GuardarCampo(datos, "salida al baño (SI/NO)", form.SalidaBano);
                GuardarCampo(datos, "PIE: participa (SI/NO/PENDIENTE)", form.ParticipaPie);
                GuardarCampo(datos, "PIE: diagnosticado (SI/NO)", form.PieDiagnostico);
                GuardarCampo(datos, "PIE: apoyos o diagnóstico", form.PieApoyos);
                break;

            case 7:
                // Las autorizaciones se responden y se guardan en su propio paso.
                GuardarCampo(datos, "autorización de uso de imagen (SI/NO)", form.AutorizaImagen);
                GuardarCampo(datos, "autorización de convivencia escolar (SI/NO)", form.AutorizaConvivencia);
                break;

            case 8:
                // El cierre de la ficha exige la confirmación expresa de que se leyó.
                if (!form.Confirmo_Revision)
                {
                    ModelState.AddModelError(nameof(FichaForm.Confirmo_Revision),
                        "Para guardar la ficha, marque la confirmación de que revisó todos los datos.");
                    MostrarPaso(8, datos);
                    TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
                    return View("Paso8", form);
                }

                // MAT-18: sobre una ficha ya generada sólo se avanza creando una
                // versión nueva y con motivo escrito. La anterior no se toca.
                var esCorreccion = datos.ContainsKey("corrección: versión base");

                if (esCorreccion && string.IsNullOrWhiteSpace(form.Motivo_Correccion))
                {
                    ModelState.AddModelError(nameof(FichaForm.Motivo_Correccion),
                        "Indique el motivo de la corrección: se guardará una versión nueva y la anterior se conserva.");
                    MostrarPaso(8, datos);
                    TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
                    return View("Paso8", form);
                }

                if (esCorreccion)
                {
                    datos["corrección: motivo"] = form.Motivo_Correccion!.Trim();
                }

                // Cierre (MAT-08): congela los datos en ficha_version, obtiene el
                // folio institucional y deja la matrícula en FICHA_GENERADA.
                var funcionarioId = FuncionarioId();

                if (funcionarioId is null)
                {
                    TempData["Aviso"] = "Inicie sesión como funcionario para generar la ficha con folio.";
                    return RedirectToAction(nameof(Paso), new { id = 8 });
                }

                RegistroFicha.CierreFicha? cierre = null;

                // El año del borrador identifica la matrícula que se cierra; en una
                // corrección puede ser un año anterior al activo (MAT-18).
                var anioCierre = int.TryParse(datos.GetValueOrDefault("año escolar"), out var anioBorrador)
                    ? anioBorrador
                    : anio;

                await _escritura.EjecutarAsync(funcionarioId.Value, async ct =>
                {
                    cierre = await _registro.CerrarFichaAsync(datos, anioCierre, funcionarioId.Value, ct);
                }, HttpContext.RequestAborted);

                var cerrada = cierre;

                if (cerrada is null)
                {
                    TempData["Aviso"] = "No se pudo cerrar la ficha: revise que el estudiante y su matrícula del año estén registrados.";
                    return RedirectToAction(nameof(Paso), new { id = 1 });
                }

                await _auditoria.RegistrarAsync(
                    "REEMPLAZAR",
                    "MATRICULA",
                    "ficha_version",
                    detalle: $"{{\"evento\":\"generar_ficha\",\"folio\":\"{cerrada.Folio}\",\"version\":{cerrada.Version}}}",
                    estudianteId: cerrada.EstudianteId,
                    recursoId: cerrada.FichaVersionId);

                TempData.Remove(ClaveBorrador);
                return RedirectToAction(nameof(Enviada), new { id = cerrada.EstudianteId });
        }

        string? aviso = null;

        await _escritura.EjecutarAsync(
            FuncionarioId() ?? 0,
            async ct => aviso = await _registro.AplicarAsync(paso, datos, anio, FuncionarioId(), ct),
            HttpContext.RequestAborted);

        TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);

        if (aviso is not null)
        {
            TempData["Aviso"] = aviso;
        }

        return RedirectToAction(nameof(Paso), new { id = paso + 1 });
    }

    /// <summary>
    /// MAT-18: abre la corrección de una ficha ya generada. El borrador se llena
    /// con la copia congelada de la versión vigente y al cerrar se crea una
    /// versión nueva con motivo; la anterior queda como REEMPLAZADA y no se edita.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Permiso:GENERAL:CREAR")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Corregir(int matriculaId)
    {
        var matricula = await _db.Matriculas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == matriculaId);

        if (matricula is null)
        {
            return NotFound();
        }

        var ficha = await _db.FichaVersiones
            .AsNoTracking()
            .Where(v => v.MatriculaId == matriculaId && v.Estado == "VIGENTE")
            .OrderByDescending(v => v.NumeroVersion)
            .FirstOrDefaultAsync();

        if (ficha is null)
        {
            TempData["Aviso"] = "La matrícula no tiene una ficha vigente que corregir.";
            return RedirectToAction("Detalle", "Alumnos", new { id = matricula.EstudianteId });
        }

        // El borrador lleva sólo la referencia: los campos se cargan desde el
        // snapshot en cada paso, sin una cookie de varios kilobytes.
        var datos = new Dictionary<string, string>
        {
            ["modo"] = "Corrección",
            ["modo: detalle"] =
                $"Corrección de la ficha {ficha.Folio} (versión {ficha.NumeroVersion}): la versión anterior se conserva.",
            ["año escolar"] = matricula.Anio.ToString(),
            ["corrección: versión base"] = ficha.NumeroVersion.ToString(),
            ["corrección: ficha"] = ficha.Id.ToString(),
        };

        TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);

        await _auditoria.RegistrarAsync(
            "REEMPLAZAR",
            "MATRICULA",
            "ficha_version",
            detalle: $"{{\"evento\":\"abrir_correccion\",\"folio\":\"{ficha.Folio}\",\"version\":{ficha.NumeroVersion}}}",
            estudianteId: matricula.EstudianteId,
            recursoId: ficha.Id);

        return RedirectToAction(nameof(Paso), new { id = 1 });
    }

    /// <summary>
    /// MAT-14: descarga una versión histórica de la ficha desde su copia
    /// congelada, con su folio y número de versión. No edita nada.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Permiso:GENERAL:LEER")]
    public async Task<IActionResult> Version(int id, bool descargar = true)
    {
        var ficha = await _db.FichaVersiones
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id);

        if (ficha is null)
        {
            return NotFound();
        }

        var anio = await AnioDeFichaAsync(ficha);

        if (anio is null)
        {
            return NotFound();
        }

        var documento = FichaImpresion.DesdeBorrador(
            DatosImprimibles(RegistroFicha.DatosDesdeSnapshot(ficha.DatosSnapshot)),
            anio.Value,
            ficha.GeneradaEn.LocalDateTime,
            LogoInstitucional(),
            $"versión {ficha.NumeroVersion} · {ficha.Estado.ToLowerInvariant()}",
            ficha.Folio);

        if (descargar)
        {
            await _auditoria.RegistrarAsync(
                "DESCARGAR",
                "MATRICULA",
                "ficha_version",
                detalle: $"{{\"evento\":\"descargar_version\",\"folio\":\"{ficha.Folio}\",\"version\":{ficha.NumeroVersion}}}",
                recursoId: ficha.Id);
        }

        var archivo = _pdf.Generar(documento);
        var nombre = $"ficha-{ficha.Folio}-v{ficha.NumeroVersion}.pdf";

        return descargar
            ? File(archivo, "application/pdf", nombre)
            : File(archivo, "application/pdf");
    }

    /// <summary>
    /// Punto de entrada del flujo: busca al estudiante por RUN antes de abrir la
    /// ficha, para decidir entre matrícula nueva y renovación (MAT-01 y MAT-02).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Permiso:GENERAL:LEER")]
    public async Task<IActionResult> Buscar(string? rut, string? tipo)
    {
        var anioActivo = await _anios.ActivoAsync();

        if (anioActivo is null)
        {
            return SinAnioActivo();
        }

        var anio = anioActivo.Value;

        var tipoDocumento = (tipo ?? "RUN").Trim().ToUpperInvariant();
        tipoDocumento = tipoDocumento is "IPE" or "IPA" ? tipoDocumento : "RUN";

        var modelo = new BusquedaFicha
        {
            Anio = anio,
            Tipo = tipoDocumento,
            Rut = tipoDocumento == "RUN" ? RutChileno.Formatear(rut) : rut?.Trim().ToUpperInvariant(),
        };

        if (string.IsNullOrWhiteSpace(rut))
        {
            return View(modelo);
        }

        if (!IdentificadorAttribute.EsValido(rut, tipoDocumento))
        {
            modelo.Error = tipoDocumento == "RUN"
                ? "El RUN no es válido: revise el número y el dígito verificador."
                : "El número IPE/IPA no es válido: use entre 3 y 20 caracteres, sin espacios.";
            return View(modelo);
        }

        modelo.Buscado = true;
        var identificador = tipoDocumento == "RUN" ? NormalizarRut(rut) : rut.Trim().ToUpperInvariant();

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

        var vigente = matriculas.FirstOrDefault(m => m.Anio == anio);
        var anterior = matriculas
            .Where(m => m.Anio < anio)
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
        var anioActivo = await _anios.ActivoAsync();

        if (anioActivo is null)
        {
            return SinAnioActivo();
        }

        var anio = anioActivo.Value;

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
            .Where(m => m.EstudianteId == id && m.Anio < anio)
            .OrderByDescending(m => m.Anio)
            .FirstOrDefaultAsync();

        var datos = new Dictionary<string, string>();
        GuardarCampo(datos, "tipo identificador", estudiante.TipoIdentificador);
        GuardarCampo(datos, "nombres", estudiante.Nombres);
        GuardarCampo(datos, "apellido paterno", estudiante.ApellidoPaterno);
        GuardarCampo(datos, "apellido materno", estudiante.ApellidoMaterno);
        // El documento se copia tal como está en la base: sólo el RUN se normaliza.
        datos["cédula de identidad"] = estudiante.Identificador;
        GuardarCampo(datos, "fecha nacimiento", estudiante.FechaNacimiento.ToString("yyyy-MM-dd"));
        GuardarCampo(datos, "nacionalidad", estudiante.Nacionalidad);
        GuardarCampo(datos, "domicilio", estudiante.Domicilio);
        GuardarCampo(datos, "comuna", estudiante.Comuna?.Nombre);

        var (nivel, letra) = SepararCurso(anterior?.Curso?.Nombre);
        GuardarCampo(datos, "curso: nivel", nivel);
        GuardarCampo(datos, "curso: letra", letra);
        GuardarCampo(datos, "año escolar", anio.ToString());

        datos["modo"] = "Renovación";
        datos["modo: detalle"] = anterior is null
            ? $"Expediente nuevo {anio}; el estudiante no tenía matrícula anterior."
            : $"Expediente nuevo {anio} a partir de la matrícula {anterior.Anio}"
              + (string.IsNullOrWhiteSpace(anterior.Curso?.Nombre) ? "." : $" ({anterior.Curso!.Nombre}).");

        TempData[ClaveBorrador] = JsonSerializer.Serialize(datos);
        return RedirectToAction(nameof(Paso), new { id = 1 });
    }

    /// <summary>Matrícula nueva para un estudiante sin matrícula anterior.</summary>
    [HttpPost]
    [Authorize(Policy = "Permiso:GENERAL:CREAR")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nueva()
    {
        var anioActivo = await _anios.ActivoAsync();

        if (anioActivo is null)
        {
            return SinAnioActivo();
        }

        var datos = new Dictionary<string, string>
        {
            ["modo"] = "Matrícula nueva",
            ["modo: detalle"] = $"Expediente nuevo {anioActivo.Value} sin matrícula anterior.",
            ["año escolar"] = anioActivo.Value.ToString(),
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
        await CompletarDesdeCorreccionAsync(datos);
        var anioActivo = await _anios.ActivoAsync();
        var fichaCerrada = anioActivo is null
            ? null
            : await FichaVigenteDesdeBorradorAsync(datos, anioActivo.Value);
        DocumentoFicha ficha;

        if (fichaCerrada is not null)
        {
            // La ficha cerrada se imprime siempre desde su copia congelada y con
            // el año con que se generó, aunque el sistema ya esté en otro año.
            var anioFicha = await AnioDeFichaAsync(fichaCerrada) ?? anioActivo!.Value;

            ficha = FichaImpresion.DesdeBorrador(
                DatosImprimibles(RegistroFicha.DatosDesdeSnapshot(fichaCerrada.DatosSnapshot)),
                anioFicha,
                fichaCerrada.GeneradaEn.LocalDateTime,
                LogoInstitucional(),
                $"versión {fichaCerrada.NumeroVersion} · vigente",
                fichaCerrada.Folio);
        }
        else
        {
            if (anioActivo is null)
            {
                return SinAnioActivo();
            }

            // Vista previa del borrador: sin folio definitivo todavía.
            await CompletarDesdeBaseAsync(datos);

            if (datos.Count == 0)
            {
                TempData["Aviso"] = "Aún no hay datos en el borrador: complete el formulario antes de generar la ficha.";
                return RedirectToAction(nameof(Paso), new { id = 1 });
            }

            ficha = FichaImpresion.DesdeBorrador(DatosImprimibles(datos), anioActivo.Value, DateTime.Now, LogoInstitucional());
        }

        var folioVisible = string.IsNullOrWhiteSpace(ficha.Folio) ? "sin folio" : ficha.Folio;

        if (descargar)
        {
            await _auditoria.RegistrarAsync(
                "DESCARGAR",
                "MATRICULA",
                "ficha_version",
                detalle: $"{{\"evento\":\"descargar_ficha\",\"folio\":\"{folioVisible}\"}}",
                recursoId: fichaCerrada?.Id);
        }

        var archivo = _pdf.Generar(ficha);

        var nombreArchivo = string.IsNullOrWhiteSpace(ficha.Folio)
            ? $"ficha-matricula-{ficha.Anio}-sin-folio.pdf"
            : $"ficha-matricula-{ficha.Folio}.pdf";

        return descargar
            ? File(archivo, "application/pdf", nombreArchivo)
            : File(archivo, "application/pdf");
    }

    /// <summary>
    /// Formulario de matrícula de un alumno ya registrado, para descargar desde su
    /// ficha. Se arma con los datos de la base y se imprime con el mismo PDF
    /// institucional de la ficha (MAT-08).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Permiso:GENERAL:LEER")]
    public async Task<IActionResult> FormularioAlumno(int id, bool descargar = true, int? anioPedido = null)
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

        // Se imprime la matrícula del año consultado; si aún no existe, la última registrada.
        var anioConsultado = await _anios.ResolverAsync(anioPedido) ?? 0;

        var matricula = matriculas.FirstOrDefault(m => m.Anio == anioConsultado)
            ?? matriculas.FirstOrDefault();

        var anio = matricula?.Anio ?? anioConsultado;

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
            ? $"El alumno no tiene matrícula {anio} registrada; la ficha se genera con sus datos personales."
            : $"Matrícula {matricula.Anio}"
              + (string.IsNullOrWhiteSpace(matricula.Curso?.Nombre) ? string.Empty : $" · {matricula.Curso!.Nombre}")
              + $" · {matricula.Estado.ToLowerInvariant()}.";

        // Apoderados, contactos, retiros y salud salen de la base, igual que en el asistente.
        await CompletarDesdeBaseAsync(datos);

        var fichaCerrada = await _registro.FichaVigenteAsync(id, anio);
        DocumentoFicha ficha;

        if (fichaCerrada is not null)
        {
            ficha = FichaImpresion.DesdeBorrador(
                DatosImprimibles(RegistroFicha.DatosDesdeSnapshot(fichaCerrada.DatosSnapshot)),
                anio,
                fichaCerrada.GeneradaEn.LocalDateTime,
                LogoInstitucional(),
                $"versión {fichaCerrada.NumeroVersion} · vigente",
                fichaCerrada.Folio);
        }
        else
        {
            var version = matricula is null
                ? $"sin matrícula {anio} registrada"
                : $"matrícula {matricula.Anio} · {matricula.Estado.ToLowerInvariant()}";

            ficha = FichaImpresion.DesdeBorrador(DatosImprimibles(datos), anio, DateTime.Now, LogoInstitucional(), version);
        }

        if (descargar)
        {
            await _auditoria.RegistrarAsync(
                "DESCARGAR",
                "MATRICULA",
                "ficha_version",
                detalle: $"{{\"evento\":\"descargar_formulario\",\"folio\":\"{(string.IsNullOrWhiteSpace(ficha.Folio) ? "sin folio" : ficha.Folio)}\"}}",
                estudianteId: id,
                recursoId: fichaCerrada?.Id);
        }

        var archivo = _pdf.Generar(ficha);
        var nombre = $"formulario-matricula-{estudiante.Identificador}-{anio}.pdf";

        return descargar
            ? File(archivo, "application/pdf", nombre)
            : File(archivo, "application/pdf");
    }

    /// <summary>
    /// Copia el contenido que se va a imprimir sin los bloques de áreas que la
    /// sesión no puede leer (S-02). Se aplica al PDF: la pantalla del asistente
    /// ya se limita por su propio permiso, pero una descarga no debe entregar más
    /// que lo que el servidor concedería en la ficha del alumno.
    /// </summary>
    private Dictionary<string, string> DatosImprimibles(IReadOnlyDictionary<string, string> datos)
    {
        var copia = new Dictionary<string, string>(datos);

        if (!User.HasClaim("permiso", "SALUD:LEER"))
        {
            copia.Remove("situación médica (SI/NO)");
            copia.Remove("alergias");
            copia.Remove("contacto sanitario: nombre");
            copia.Remove("contacto sanitario: teléfono");
            copia.Remove("salida al baño (SI/NO)");
            QuitarPrefijo(copia, "situación médica ");
        }

        if (!User.HasClaim("permiso", "PIE:LEER"))
        {
            QuitarPrefijo(copia, "PIE: ");
        }

        if (!User.HasClaim("permiso", "RETIRO:LEER"))
        {
            QuitarPrefijo(copia, "retiro autorizado ");
            copia.Remove("salida solo al finalizar jornada (SI/NO)");
        }

        if (!User.HasClaim("permiso", "FAMILIA:LEER"))
        {
            QuitarPrefijo(copia, "madre: ");
            QuitarPrefijo(copia, "padre: ");
            QuitarPrefijo(copia, "apoderado principal: ");
            QuitarPrefijo(copia, "apoderado secundario: ");
            QuitarPrefijo(copia, "contacto emergencia ");
            copia.Remove("restricción madre (SI/NO)");
            copia.Remove("restricción madre: tipo");
            copia.Remove("restricción madre: detalle");
        }

        if (!User.HasClaim("permiso", "AUTORIZACIONES:LEER"))
        {
            copia.Remove("autorización de uso de imagen (SI/NO)");
            copia.Remove("autorización de convivencia escolar (SI/NO)");
        }

        return copia;
    }

    private static void QuitarPrefijo(Dictionary<string, string> datos, string prefijo)
    {
        foreach (var clave in datos.Keys.Where(clave => clave.StartsWith(prefijo, StringComparison.Ordinal)).ToList())
        {
            datos.Remove(clave);
        }
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

    // ── Validaciones de bloque (pasos 2 a 7) ─────────────────────────────────

    /// <summary>
    /// Reglas que dependen de varios campos (ver <see cref="Models.ValidadorFicha"/>
    /// para las reglas y sus pruebas).
    /// </summary>
    private void ValidarPaso(int paso, FichaForm form)
    {
        foreach (var error in ValidadorFicha.Validar(paso, form))
        {
            ModelState.AddModelError(error.Campo ?? string.Empty, error.Mensaje);
        }
    }

    /// <summary>Campos que pertenecen a cada paso del asistente.</summary>
    private static readonly Dictionary<int, string[]> CamposDelPaso = new()
    {
        [1] = new[]
        {
            nameof(FichaForm.Curso_Nivel), nameof(FichaForm.Curso_Letra), nameof(FichaForm.Curso_Anio),
            nameof(FichaForm.Tipo_Identificador), nameof(FichaForm.Cedula_Identidad),
            nameof(FichaForm.Nombres), nameof(FichaForm.Apellido_Paterno), nameof(FichaForm.Apellido_Materno),
            nameof(FichaForm.Fecha_Nacimiento), nameof(FichaForm.Nacionalidad),
            nameof(FichaForm.Domicilio), nameof(FichaForm.Comuna),
        },
        [2] = new[]
        {
            nameof(FichaForm.Madre_Nombre_Apellido), nameof(FichaForm.Madre_Cedula_Identidad),
            nameof(FichaForm.Madre_Telefono), nameof(FichaForm.Madre_Escolaridad),
            nameof(FichaForm.Madre_Domicilio), nameof(FichaForm.Madre_Correo),
            nameof(FichaForm.Padre_Nombre_Apellido), nameof(FichaForm.Padre_Cedula_Identidad),
            nameof(FichaForm.Padre_Telefono), nameof(FichaForm.Padre_Escolaridad),
            nameof(FichaForm.Padre_Domicilio), nameof(FichaForm.Padre_Correo),
            nameof(FichaForm.Restriccion_Madre), nameof(FichaForm.Restriccion_Madre_Tipo),
            nameof(FichaForm.Restriccion_Madre_Detalle),
        },
        [3] = new[]
        {
            nameof(FichaForm.Principal_Nombre_Apellido), nameof(FichaForm.Principal_Cedula_Identidad),
            nameof(FichaForm.Principal_Telefono), nameof(FichaForm.Principal_Vinculo),
            nameof(FichaForm.Principal_Correo),
            nameof(FichaForm.Secundario_Nombre_Apellido), nameof(FichaForm.Secundario_Cedula_Identidad),
            nameof(FichaForm.Secundario_Telefono), nameof(FichaForm.Secundario_Vinculo),
            nameof(FichaForm.Secundario_Correo),
        },
        [4] = new[]
        {
            nameof(FichaForm.Contacto1_Identificacion), nameof(FichaForm.Contacto1_Telefono),
            nameof(FichaForm.Contacto1_Vinculo), nameof(FichaForm.Contacto2_Identificacion),
            nameof(FichaForm.Contacto2_Telefono), nameof(FichaForm.Contacto2_Vinculo),
        },
        [5] = new[]
        {
            nameof(FichaForm.SalidaSolo), nameof(FichaForm.Retiro1_Rut), nameof(FichaForm.Retiro1_Vinculo),
            nameof(FichaForm.Retiro2_Rut), nameof(FichaForm.Retiro2_Vinculo),
            nameof(FichaForm.Retiro3_Rut), nameof(FichaForm.Retiro3_Vinculo),
        },
        [6] = new[]
        {
            nameof(FichaForm.TieneSituacionMedica),
            nameof(FichaForm.Medica1_Identificacion), nameof(FichaForm.Medica1_Situacion), nameof(FichaForm.Medica1_Tratamiento),
            nameof(FichaForm.Medica2_Identificacion), nameof(FichaForm.Medica2_Situacion), nameof(FichaForm.Medica2_Tratamiento),
            nameof(FichaForm.Medica3_Identificacion), nameof(FichaForm.Medica3_Situacion), nameof(FichaForm.Medica3_Tratamiento),
            nameof(FichaForm.Alergias), nameof(FichaForm.Contacto_Sanitario_Nombre),
            nameof(FichaForm.Contacto_Sanitario_Telefono), nameof(FichaForm.SalidaBano),
            nameof(FichaForm.ParticipaPie), nameof(FichaForm.PieDiagnostico), nameof(FichaForm.PieApoyos),
        },
        [7] = new[]
        {
            nameof(FichaForm.AutorizaImagen), nameof(FichaForm.AutorizaConvivencia),
        },
        [8] = new[] { nameof(FichaForm.Motivo_Correccion), nameof(FichaForm.Confirmo_Revision) },
    };

    /// <summary>
    /// Elimina del estado del modelo los campos que no corresponden al paso
    /// actual. Los errores de nivel de modelo (clave vacía) se conservan.
    /// </summary>
    private void LimpiarModelStateDeOtrosPasos(int paso)
    {
        var permitidos = CamposDelPaso[paso];

        foreach (var clave in ModelState.Keys.ToList())
        {
            if (string.IsNullOrEmpty(clave))
            {
                continue;
            }

            var nombre = clave.Contains('.') ? clave[(clave.LastIndexOf('.') + 1)..] : clave;

            if (!permitidos.Contains(nombre, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.Remove(clave);
            }
        }
    }

    // ── Apoyo del cierre y de la búsqueda por documento ─────────────────────

    private int? FuncionarioId() =>
        int.TryParse(User.FindFirstValue("funcionario_id"), out var id) ? id : null;

    /// <summary>Ficha vigente del estudiante identificado en el borrador.</summary>
    private async Task<FichaVersion?> FichaVigenteDesdeBorradorAsync(
        IReadOnlyDictionary<string, string> datos,
        int anio)
    {
        var estudiante = await EstudianteDelBorradorAsync(datos);

        return estudiante is null ? null : await _registro.FichaVigenteAsync(estudiante.Id, anio);
    }

    /// <summary>Año con que se generó una ficha, tomado de su matrícula.</summary>
    private async Task<int?> AnioDeFichaAsync(FichaVersion ficha) =>
        await _db.Matriculas
            .AsNoTracking()
            .Where(m => m.Id == ficha.MatriculaId)
            .Select(m => (int?)m.Anio)
            .FirstOrDefaultAsync();

    /// <summary>Salida común cuando todavía no hay un año escolar configurado.</summary>
    private IActionResult SinAnioActivo()
    {
        TempData["Aviso"] =
            "No hay un año escolar con matrícula activa. Secretaría o Dirección debe definirlo en Configuración → Año escolar.";

        return RedirectToAction("Index", "Home");
    }

    /// <summary>Busca al estudiante por RUN o por IPE/IPA, según el tipo del borrador.</summary>
    private async Task<Estudiante?> EstudianteDelBorradorAsync(IReadOnlyDictionary<string, string> datos)
    {
        var documento = datos.GetValueOrDefault("cédula de identidad");

        if (string.IsNullOrWhiteSpace(documento))
        {
            return null;
        }

        var tipo = datos.GetValueOrDefault("tipo identificador") ?? "RUN";
        var normalizado = tipo == "RUN"
            ? RutChileno.Limpiar(documento)
            : documento.Trim().ToUpperInvariant();

        return await _db.Estudiantes
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Identificador == normalizado);
    }

    /// <summary>Guarda el documento respetando IPE/IPA: sólo el RUN se normaliza.</summary>
    private static void GuardarDocumento(Dictionary<string, string> datos, FichaForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Cedula_Identidad))
        {
            return;
        }

        var tipo = (form.Tipo_Identificador ?? "RUN").Trim().ToUpperInvariant();

        datos["cédula de identidad"] = tipo == "RUN"
            ? RutChileno.Limpiar(form.Cedula_Identidad)
            : form.Cedula_Identidad.Trim().ToUpperInvariant();
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
        estado is "FICHA_GENERADA" or "FIRMADA" or "CONFIRMADA" or "FORMALIZADA";

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
    /// MAT-18: completa el borrador con la copia congelada de la ficha que se está
    /// corrigiendo. Sólo rellena lo que falta y no escribe en la ficha original.
    /// </summary>
    private async Task CompletarDesdeCorreccionAsync(Dictionary<string, string> datos)
    {
        if (!datos.TryGetValue("corrección: ficha", out var valor) || !int.TryParse(valor, out var fichaId))
        {
            return;
        }

        var ficha = await _db.FichaVersiones
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == fichaId);

        if (ficha is null)
        {
            return;
        }

        foreach (var (clave, contenido) in RegistroFicha.DatosDesdeSnapshot(ficha.DatosSnapshot))
        {
            if (!datos.ContainsKey(clave))
            {
                datos[clave] = contenido;
            }
        }
    }

    /// <summary>
    /// Completa los pasos 2 a 6 del borrador con lo que ya está guardado del
    /// estudiante (madre, padre, apoderados, contactos de emergencia, retiros y
    /// datos médicos). Sólo rellena lo que falta: nunca pisa lo escrito en el
    /// asistente. Devuelve true si agregó algún dato.
    /// </summary>
    private async Task<bool> CompletarDesdeBaseAsync(Dictionary<string, string> datos)
    {
        var estudiante = await EstudianteDelBorradorAsync(datos);

        if (estudiante is null)
        {
            return false;
        }

        var agregados = PonerSiFalta(datos, "tipo identificador", estudiante.TipoIdentificador);

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

            // Se precarga el detalle de la última restricción vigente de la madre.
            var vinculoMadre = vinculos.FirstOrDefault(v => v.Parentesco == "MADRE");

            if (vinculoMadre is not null)
            {
                var restriccion = await _db.RestriccionesFamiliares
                    .AsNoTracking()
                    .Where(r => r.EstudiantePersonaId == vinculoMadre.Id)
                    .OrderByDescending(r => r.VigenteHasta == null)
                    .ThenByDescending(r => r.Id)
                    .FirstOrDefaultAsync();

                if (restriccion is not null)
                {
                    agregados += PonerSiFalta(datos, "restricción madre: tipo",
                        restriccion.Tipo == "JUDICIAL" ? "Judicial" : "Familiar");
                    agregados += PonerSiFalta(datos, "restricción madre: detalle", restriccion.Descripcion);
                }
            }
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

        agregados += PonerSiFalta(datos, "contacto sanitario: nombre", salud?.ContactoSanitarioNombre);
        agregados += PonerSiFalta(datos, "contacto sanitario: teléfono", salud?.ContactoSanitarioTelefono);

        var alergias = await _db.SituacionesMedicas
            .AsNoTracking()
            .Where(s => s.EstudianteId == estudiante.Id && s.VigenteHasta == null && s.Tipo == "ALERGIA")
            .Select(s => s.Descripcion)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(alergias))
        {
            agregados += PonerSiFalta(datos, "alergias", alergias);
        }

        var autorizaciones = await _db.Autorizaciones
            .AsNoTracking()
            .Where(a => a.EstudianteId == estudiante.Id && a.VigenteHasta == null)
            .ToListAsync();

        foreach (var autorizacion in autorizaciones)
        {
            var clave = autorizacion.Tipo switch
            {
                "IMAGEN" => "autorización de uso de imagen (SI/NO)",
                "CONVIVENCIA" => "autorización de convivencia escolar (SI/NO)",
                _ => null,
            };

            if (clave is not null)
            {
                agregados += PonerSiFalta(datos, clave, autorizacion.Estado == "OTORGADA" ? "Si" : "No");
            }
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
    private static FichaForm FormularioDesdeBorrador(Dictionary<string, string> datos, int anio) => new()
    {
        Curso_Nivel = datos.GetValueOrDefault("curso: nivel"),
        Curso_Letra = datos.GetValueOrDefault("curso: letra"),
        Curso_Anio = datos.GetValueOrDefault("año escolar") ?? anio.ToString(),
        Nombres = datos.GetValueOrDefault("nombres"),
        Apellido_Paterno = datos.GetValueOrDefault("apellido paterno"),
        Apellido_Materno = datos.GetValueOrDefault("apellido materno"),
        Cedula_Identidad = RutChileno.Formatear(datos.GetValueOrDefault("cédula de identidad")),
        Tipo_Identificador = datos.GetValueOrDefault("tipo identificador") ?? "RUN",
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
        Restriccion_Madre_Tipo = datos.GetValueOrDefault("restricción madre: tipo"),
        Restriccion_Madre_Detalle = datos.GetValueOrDefault("restricción madre: detalle"),

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

        Alergias = datos.GetValueOrDefault("alergias"),
        Contacto_Sanitario_Nombre = datos.GetValueOrDefault("contacto sanitario: nombre"),
        Contacto_Sanitario_Telefono = datos.GetValueOrDefault("contacto sanitario: teléfono"),

        SalidaBano = datos.GetValueOrDefault("salida al baño (SI/NO)"),

        ParticipaPie = datos.GetValueOrDefault("PIE: participa (SI/NO/PENDIENTE)"),
        PieDiagnostico = datos.GetValueOrDefault("PIE: diagnosticado (SI/NO)"),
        PieApoyos = datos.GetValueOrDefault("PIE: apoyos o diagnóstico"),

        AutorizaImagen = datos.GetValueOrDefault("autorización de uso de imagen (SI/NO)"),
        AutorizaConvivencia = datos.GetValueOrDefault("autorización de convivencia escolar (SI/NO)"),

        Motivo_Correccion = datos.GetValueOrDefault("corrección: motivo"),
    };

}
