using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Models.Validaciones;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MarcelaPaz.Web.Servicios;

/// <summary>
/// Lleva el borrador del asistente a las tablas del DDL institucional.
///
/// Se aplica paso a paso, cada vez que se guarda: el formulario no exige campos,
/// así que sólo se escriben los bloques que traen datos. Cuando falta el RUN del
/// estudiante o de una persona no hay forma de identificarla en la base y el
/// servicio devuelve un aviso para que el funcionario sepa qué quedó sin registrar.
/// </summary>
public class RegistroFicha
{
    private readonly MatriculaDbContext _db;

    public RegistroFicha(MatriculaDbContext db) => _db = db;

    /// <summary>
    /// Escribe el paso indicado. Devuelve un aviso para mostrar en pantalla, o
    /// <c>null</c> cuando todo se guardó sin observaciones.
    /// </summary>
    public async Task<string?> AplicarAsync(
        int paso,
        IReadOnlyDictionary<string, string> datos,
        int anio,
        int? funcionarioId = null,
        CancellationToken ct = default)
    {
        var avisos = new List<string>();

        try
        {
            switch (paso)
            {
                case 1:
                    await Paso1Async(datos, anio, funcionarioId, avisos, ct);
                    break;
                case 2:
                    await Paso2Async(datos, anio, avisos, ct);
                    break;
                case 3:
                    await Paso3Async(datos, avisos, ct);
                    break;
                case 4:
                    await Paso4Async(datos, avisos, ct);
                    break;
                case 5:
                    await Paso5Async(datos, avisos, ct);
                    break;
                case 6:
                    await Paso6Async(datos, avisos, ct);
                    break;
                case 7:
                    await Paso7Async(datos, avisos, ct);
                    break;
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            return $"No se pudo guardar en la base de datos: {ex.InnerException?.Message ?? ex.Message}";
        }

        return avisos.Count == 0 ? null : string.Join(" ", avisos);
    }

    // ── Paso 1 · Estudiante, curso y matrícula del año ───────────────────────

    private async Task Paso1Async(
        IReadOnlyDictionary<string, string> datos,
        int anio,
        int? funcionarioId,
        List<string> avisos,
        CancellationToken ct)
    {
        var tipoDocumento = TipoDocumento(Texto(datos, "tipo identificador"));
        var documento = NormalizarDocumento(tipoDocumento, Texto(datos, "cédula de identidad"));
        var nombres = Texto(datos, "nombres");
        var paterno = Texto(datos, "apellido paterno");
        var nacimiento = Fecha(Texto(datos, "fecha nacimiento"));

        var estudiante = documento is null
            ? null
            : await _db.Estudiantes.FirstOrDefaultAsync(e => e.Identificador == documento, ct);

        if (estudiante is null)
        {
            if (documento is null || !DocumentoValido(tipoDocumento, documento) || nombres is null || paterno is null || nacimiento is null)
            {
                avisos.Add("El estudiante aún no se registra en la base: se necesitan documento válido (RUN o IPE/IPA), nombres, apellido paterno y fecha de nacimiento.");
                return;
            }

            estudiante = new Estudiante
            {
                TipoIdentificador = tipoDocumento,
                Identificador = documento,
                Nombres = nombres,
                ApellidoPaterno = paterno,
                ApellidoMaterno = Texto(datos, "apellido materno"),
                FechaNacimiento = nacimiento.Value,
                Nacionalidad = Texto(datos, "nacionalidad") ?? "Chilena",
                Domicilio = Texto(datos, "domicilio") ?? string.Empty,
            };

            _db.Estudiantes.Add(estudiante);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // Sólo se reemplaza lo que vino con valor: nunca se borra lo ya guardado.
            estudiante.TipoIdentificador = tipoDocumento;
            estudiante.Nombres = nombres ?? estudiante.Nombres;
            estudiante.ApellidoPaterno = paterno ?? estudiante.ApellidoPaterno;
            estudiante.ApellidoMaterno = Texto(datos, "apellido materno") ?? estudiante.ApellidoMaterno;
            estudiante.FechaNacimiento = nacimiento ?? estudiante.FechaNacimiento;
            estudiante.Nacionalidad = Texto(datos, "nacionalidad") ?? estudiante.Nacionalidad;
            estudiante.Domicilio = Texto(datos, "domicilio") ?? estudiante.Domicilio;
        }

        var nombreComuna = Texto(datos, "comuna");
        if (nombreComuna is not null)
        {
            var comuna = await ComunaPorNombreAsync(nombreComuna, ct);

            if (comuna is null)
            {
                avisos.Add($"La comuna «{nombreComuna}» no está en el registro; el estudiante quedó sin comuna.");
            }
            else
            {
                estudiante.ComunaId = comuna.Id;
            }
        }

        var tipo = (datos.GetValueOrDefault("modo") ?? string.Empty)
            .StartsWith("Renov", StringComparison.OrdinalIgnoreCase)
                ? "RENOVACION"
                : "NUEVA";

        var matricula = await _db.Matriculas.FirstOrDefaultAsync(
            m => m.EstudianteId == estudiante.Id && m.Anio == anio && m.Estado != "ANULADA", ct);

        if (matricula is null)
        {
            matricula = new MatriculaAnual
            {
                EstudianteId = estudiante.Id,
                Anio = (short)anio,
                Tipo = tipo,
                Estado = "BORRADOR",
                CreadaPor = funcionarioId,
            };

            _db.Matriculas.Add(matricula);
        }
        else
        {
            matricula.Tipo = tipo;
        }

        var curso = await CursoAsync(Texto(datos, "curso: nivel"), Texto(datos, "curso: letra"), ct);
        if (curso is not null)
        {
            matricula.CursoId = curso.Id;
        }
    }

    // ── Paso 2 · Madre y padre ───────────────────────────────────────────────

    private async Task Paso2Async(
        IReadOnlyDictionary<string, string> datos,
        int anio,
        List<string> avisos,
        CancellationToken ct)
    {
        var estudiante = await EstudianteActualAsync(datos, avisos, ct);
        if (estudiante is null)
        {
            return;
        }

        var madre = await AsegurarPersonaAsync(datos, "madre", "la madre", avisos, ct);
        var vinculoMadre = madre is null
            ? null
            : await AsegurarRelacionAsync(estudiante.Id, madre, "MADRE", null, null, avisos, ct);

        var padre = await AsegurarPersonaAsync(datos, "padre", "el padre", avisos, ct);
        if (padre is not null)
        {
            await AsegurarRelacionAsync(estudiante.Id, padre, "PADRE", null, null, avisos, ct);
        }

        if (vinculoMadre is null)
        {
            return;
        }

        var restriccion = SiNo(Texto(datos, "restricción madre (SI/NO)"));
        var vigentes = await _db.RestriccionesFamiliares
            .Where(r => r.EstudiantePersonaId == vinculoMadre.Id && r.VigenteHasta == null)
            .ToListAsync(ct);

        if (restriccion == true && vigentes.Count == 0)
        {
            // El tipo y el detalle los declara el apoderado en el paso 2; si el
            // borrador es anterior a esos campos, se conserva el texto genérico.
            var tipo = Texto(datos, "restricción madre: tipo");
            var detalle = Texto(datos, "restricción madre: detalle");

            _db.RestriccionesFamiliares.Add(new RestriccionFamiliar
            {
                EstudiantePersona = vinculoMadre,
                Tipo = tipo is not null && tipo.StartsWith("J", StringComparison.OrdinalIgnoreCase)
                    ? "JUDICIAL"
                    : "FAMILIAR",
                Descripcion = detalle
                    ?? $"Restricción declarada en la ficha {anio}: la madre no puede vincularse con el o la estudiante.",
                VigenteDesde = DateOnly.FromDateTime(DateTime.Now),
            });
        }
        else if (restriccion == false)
        {
            foreach (var restriccionVigente in vigentes)
            {
                restriccionVigente.VigenteHasta = DateOnly.FromDateTime(DateTime.Now);
            }
        }

        if (restriccion is not null)
        {
            vinculoMadre.TieneRestriccion = restriccion;
        }
    }

    // ── Paso 3 · Apoderado principal y secundario ────────────────────────────

    private async Task Paso3Async(
        IReadOnlyDictionary<string, string> datos,
        List<string> avisos,
        CancellationToken ct)
    {
        var estudiante = await EstudianteActualAsync(datos, avisos, ct);
        if (estudiante is null)
        {
            return;
        }

        await AsegurarApoderadoAsync(datos, "apoderado principal", "PRINCIPAL", estudiante.Id, avisos, ct);
        await AsegurarApoderadoAsync(datos, "apoderado secundario", "SECUNDARIO", estudiante.Id, avisos, ct);
    }

    private async Task AsegurarApoderadoAsync(
        IReadOnlyDictionary<string, string> datos,
        string prefijo,
        string tipoApoderado,
        int estudianteId,
        List<string> avisos,
        CancellationToken ct)
    {
        var persona = await AsegurarPersonaAsync(datos, prefijo, $"el apoderado {tipoApoderado.ToLowerInvariant()}", avisos, ct);
        var vinculo = Texto(datos, $"{prefijo}: vínculo");

        if (persona is null)
        {
            return;
        }

        await AsegurarRelacionAsync(
            estudianteId,
            persona,
            "OTRO",
            vinculo ?? "No declarado",
            tipoApoderado,
            avisos,
            ct);
    }

    // ── Paso 4 · Contactos de emergencia ─────────────────────────────────────

    private async Task Paso4Async(
        IReadOnlyDictionary<string, string> datos,
        List<string> avisos,
        CancellationToken ct)
    {
        var estudiante = await EstudianteActualAsync(datos, avisos, ct);
        if (estudiante is null)
        {
            return;
        }

        foreach (var orden in new short[] { 1, 2 })
        {
            var numero = orden.ToString("00", CultureInfo.InvariantCulture);
            var nombre = Texto(datos, $"contacto emergencia {numero}: identificación");
            var telefono = Texto(datos, $"contacto emergencia {numero}: teléfono");
            var vinculo = Texto(datos, $"contacto emergencia {numero}: vínculo");

            if (nombre is null && telefono is null && vinculo is null)
            {
                continue;
            }

            if (nombre is null || telefono is null)
            {
                avisos.Add($"El contacto de emergencia {orden} no se guarda hasta tener nombre y teléfono.");
                continue;
            }

            var contacto = await _db.ContactosEmergencia.FirstOrDefaultAsync(
                c => c.EstudianteId == estudiante.Id && c.Orden == orden && c.VigenteHasta == null, ct);

            if (contacto is null)
            {
                contacto = new ContactoEmergencia
                {
                    EstudianteId = estudiante.Id,
                    Orden = orden,
                    VigenteDesde = DateOnly.FromDateTime(DateTime.Now),
                };

                _db.ContactosEmergencia.Add(contacto);
            }

            contacto.Nombre = nombre;
            contacto.Telefono = telefono;
            contacto.Vinculo = vinculo ?? "No declarado";
        }
    }

    // ── Paso 5 · Retiro ──────────────────────────────────────────────────────

    private async Task Paso5Async(
        IReadOnlyDictionary<string, string> datos,
        List<string> avisos,
        CancellationToken ct)
    {
        var estudiante = await EstudianteActualAsync(datos, avisos, ct);
        if (estudiante is null)
        {
            return;
        }

        var saleSolo = SiNo(Texto(datos, "salida solo al finalizar jornada (SI/NO)"));
        if (saleSolo is not null)
        {
            estudiante.SaleSoloFinalJornada = saleSolo;
        }

        foreach (var numero in new[] { "01", "02", "03" })
        {
            var rut = NormalizarRut(Texto(datos, $"retiro autorizado {numero}: rut"));
            var vinculo = Texto(datos, $"retiro autorizado {numero}: vínculo");

            if (rut is null)
            {
                continue;
            }

            if (!RutValido(rut))
            {
                avisos.Add($"El RUN «{rut}» de la persona autorizada a retirar no es válido y no se guardó.");
                continue;
            }

            var autorizado = await _db.RetirosAutorizados.FirstOrDefaultAsync(
                r => r.EstudianteId == estudiante.Id && r.Identificador == rut && r.VigenteHasta == null, ct);

            if (autorizado is null)
            {
                var persona = await _db.Personas.FirstOrDefaultAsync(p => p.Identificador == rut, ct);

                autorizado = new RetiroAutorizado
                {
                    EstudianteId = estudiante.Id,
                    TipoIdentificador = "RUN",
                    Identificador = rut,
                    Nombre = persona?.Nombres ?? rut,
                    VigenteDesde = DateOnly.FromDateTime(DateTime.Now),
                };

                _db.RetirosAutorizados.Add(autorizado);
            }

            autorizado.Vinculo = vinculo ?? "No declarado";
        }
    }

    // ── Paso 6 · Datos médicos ───────────────────────────────────────────────

    private async Task Paso6Async(
        IReadOnlyDictionary<string, string> datos,
        List<string> avisos,
        CancellationToken ct)
    {
        var estudiante = await EstudianteActualAsync(datos, avisos, ct);
        if (estudiante is null)
        {
            return;
        }

        var tieneSituacion = SiNo(Texto(datos, "situación médica (SI/NO)"));
        var saleAlBano = SiNo(Texto(datos, "salida al baño (SI/NO)"));

        var antecedente = await _db.AntecedentesSalud
            .FirstOrDefaultAsync(a => a.EstudianteId == estudiante.Id, ct);

        if (antecedente is null)
        {
            antecedente = new AntecedenteSalud { EstudianteId = estudiante.Id };
            _db.AntecedentesSalud.Add(antecedente);
        }

        antecedente.TieneSituacionMedica = tieneSituacion ?? antecedente.TieneSituacionMedica;
        antecedente.NecesidadesAtencion = saleAlBano is null
            ? antecedente.NecesidadesAtencion
            : $"Sale al baño durante la jornada: {(saleAlBano.Value ? "SI" : "NO")}.";
        antecedente.ContactoSanitarioNombre = Texto(datos, "contacto sanitario: nombre") ?? antecedente.ContactoSanitarioNombre;
        antecedente.ContactoSanitarioTelefono = Texto(datos, "contacto sanitario: teléfono") ?? antecedente.ContactoSanitarioTelefono;

        var declaradas = new List<SituacionMedica>();

        foreach (var numero in new[] { "01", "02", "03" })
        {
            var identificacion = Texto(datos, $"situación médica {numero}: identificación");
            var detalle = Texto(datos, $"situación médica {numero}: detalle");
            var tratamiento = SiNo(Texto(datos, $"situación médica {numero}: en tratamiento"));

            var descripcion = (identificacion, detalle) switch
            {
                (null, null) => null,
                (not null, null) => identificacion,
                (null, not null) => detalle,
                _ => $"{identificacion}: {detalle}",
            };

            if (descripcion is null)
            {
                continue;
            }

            declaradas.Add(new SituacionMedica
            {
                EstudianteId = estudiante.Id,
                Tipo = "CONDICION",
                Descripcion = descripcion,
                // La columna es NOT NULL: sin respuesta se guarda FALSE.
                EnTratamiento = tratamiento ?? false,
                VigenteDesde = DateOnly.FromDateTime(DateTime.Now),
            });
        }

        if (declaradas.Count > 0)
        {
            var anteriores = await _db.SituacionesMedicas
                .Where(s => s.EstudianteId == estudiante.Id && s.VigenteHasta == null && s.Tipo != "ALERGIA")
                .ToListAsync(ct);

            _db.SituacionesMedicas.RemoveRange(anteriores);
            _db.SituacionesMedicas.AddRange(declaradas);
        }

        // Alergias: se guardan como situación médica de tipo ALERGIA.
        var alergias = Texto(datos, "alergias");
        if (alergias is not null)
        {
            var alergiasAnteriores = await _db.SituacionesMedicas
                .Where(s => s.EstudianteId == estudiante.Id && s.VigenteHasta == null && s.Tipo == "ALERGIA")
                .ToListAsync(ct);

            _db.SituacionesMedicas.RemoveRange(alergiasAnteriores);

            if (!SinAlergias(alergias))
            {
                _db.SituacionesMedicas.Add(new SituacionMedica
                {
                    EstudianteId = estudiante.Id,
                    Tipo = "ALERGIA",
                    Descripcion = alergias,
                    EnTratamiento = false,
                    VigenteDesde = DateOnly.FromDateTime(DateTime.Now),
                });
            }
        }

        // PIE y apoyos: un único registro por estudiante.
        var participaPie = Texto(datos, "PIE: participa (SI/NO/PENDIENTE)");
        var diagnosticoPie = SiNo(Texto(datos, "PIE: diagnosticado (SI/NO)"));
        var apoyosPie = Texto(datos, "PIE: apoyos o diagnóstico");

        if (participaPie is not null || diagnosticoPie is not null || apoyosPie is not null)
        {
            var apoyo = await _db.AntecedentesApoyo
                .FirstOrDefaultAsync(a => a.EstudianteId == estudiante.Id, ct);

            if (apoyo is null)
            {
                apoyo = new AntecedenteApoyo { EstudianteId = estudiante.Id };
                _db.AntecedentesApoyo.Add(apoyo);
            }

            if (participaPie is not null)
            {
                apoyo.ParticipaPie = SiNo(participaPie) switch
                {
                    true => "SI",
                    false => "NO",
                    null => "PENDIENTE",
                };
            }

            if (diagnosticoPie is not null)
            {
                apoyo.TieneDiagnostico = diagnosticoPie;
            }

            if (apoyosPie is not null)
            {
                apoyo.ApoyosInformados = apoyosPie;
            }
        }

    }

    // ── Paso 7 · Autorizaciones del apoderado ────────────────────────────────

    private async Task Paso7Async(
        IReadOnlyDictionary<string, string> datos,
        List<string> avisos,
        CancellationToken ct)
    {
        var estudiante = await EstudianteActualAsync(datos, avisos, ct);
        if (estudiante is null)
        {
            return;
        }

        await GuardarAutorizacionesAsync(datos, estudiante, avisos, ct);
    }

    // ── Autorizaciones de imagen y convivencia (MAT-03 y MAT-04) ─────────────

    private static readonly (string Clave, string Tipo, string Texto, string[] Alcances)[] AutorizacionesFicha =
    [
        ("autorización de uso de imagen (SI/NO)", "IMAGEN", FichaImpresion.TextoImagen,
            ["CAPTACION_INTERNA", "SITIO_INSTITUCIONAL", "REDES_SOCIALES"]),
        ("autorización de convivencia escolar (SI/NO)", "CONVIVENCIA", FichaImpresion.TextoConvivencia, []),
    ];

    /// <summary>
    /// Guarda la respuesta a cada autorización con su estado (OTORGADA/RECHAZADA)
    /// y sus alcances. Si el colegio todavía no aprueba el texto, la plantilla queda
    /// registrada como pendiente de aprobación para no bloquear la captura.
    /// </summary>
    private async Task GuardarAutorizacionesAsync(
        IReadOnlyDictionary<string, string> datos,
        Estudiante estudiante,
        List<string> avisos,
        CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var firmante = await PersonaFirmanteAsync(datos, ct);

        foreach (var (clave, tipo, texto, alcances) in AutorizacionesFicha)
        {
            var respuesta = SiNo(Texto(datos, clave));
            if (respuesta is null)
            {
                continue;
            }

            var plantilla = await _db.PlantillasAutorizacion
                .FirstOrDefaultAsync(p => p.Tipo == tipo && p.VigenteHasta == null, ct);

            if (plantilla is null)
            {
                var ultima = await _db.PlantillasAutorizacion
                    .Where(p => p.Tipo == tipo)
                    .MaxAsync(p => (short?)p.Version, ct) ?? 0;

                plantilla = new PlantillaAutorizacion
                {
                    Tipo = tipo,
                    Version = (short)(ultima + 1),
                    Texto = texto,
                    AprobadoPor = "Pendiente de aprobación (Dirección)",
                    VigenteDesde = hoy,
                };

                _db.PlantillasAutorizacion.Add(plantilla);
                await _db.SaveChangesAsync(ct);
            }

            // uk_autorizacion_otorgada admite una sola OTORGADA por estudiante y
            // tipo, sin mirar la vigencia. Al otorgar se reutiliza esa fila aunque
            // figure cerrada; si no existe, se toma la vigente o se crea una nueva.
            Autorizacion? autorizacion = null;

            if (respuesta.Value)
            {
                autorizacion = await _db.Autorizaciones
                    .Include(a => a.Alcances)
                    .FirstOrDefaultAsync(
                        a => a.EstudianteId == estudiante.Id && a.Tipo == tipo && a.Estado == "OTORGADA",
                        ct);
            }

            autorizacion ??= await _db.Autorizaciones
                .Include(a => a.Alcances)
                .FirstOrDefaultAsync(
                    a => a.EstudianteId == estudiante.Id && a.Tipo == tipo && a.VigenteHasta == null,
                    ct);

            if (autorizacion is null)
            {
                autorizacion = new Autorizacion
                {
                    EstudianteId = estudiante.Id,
                    Tipo = tipo,
                    PlantillaId = plantilla.Id,
                };

                _db.Autorizaciones.Add(autorizacion);
            }

            // ck_aut_otorgada exige firmante y fecha cuando la respuesta es OTORGADA.
            if (respuesta.Value && firmante is null)
            {
                avisos.Add($"La autorización de {tipo.ToLowerInvariant()} quedó pendiente: no se encontró la persona firmante.");
                autorizacion.Estado = "PENDIENTE";
                autorizacion.FirmantePersonaId = null;
                autorizacion.FechaFirma = null;
            }
            else
            {
                autorizacion.Estado = respuesta.Value ? "OTORGADA" : "RECHAZADA";
                autorizacion.FirmantePersonaId = firmante;
                autorizacion.FechaFirma = hoy;
            }

            autorizacion.VigenteDesde = hoy;
            autorizacion.VigenteHasta = null;

            if (alcances.Length > 0)
            {
                _db.AutorizacionesAlcance.RemoveRange(autorizacion.Alcances);
                autorizacion.Alcances.Clear();

                foreach (var alcance in alcances)
                {
                    autorizacion.Alcances.Add(new AutorizacionAlcance
                    {
                        Alcance = alcance,
                        Otorgado = respuesta.Value,
                    });
                }
            }

            // Sólo una autorización actual por tipo: las demás filas abiertas del
            // mismo tipo quedan cerradas con la fecha de hoy.
            var otrasVigentes = await _db.Autorizaciones
                .Where(a => a.EstudianteId == estudiante.Id && a.Tipo == tipo
                    && a.Id != autorizacion.Id && a.VigenteHasta == null)
                .ToListAsync(ct);

            foreach (var otra in otrasVigentes)
            {
                otra.VigenteHasta = hoy;
            }
        }
    }

    // ── Apoyo ────────────────────────────────────────────────────────────────

    /// <summary>Persona que firma: apoderado principal, madre o padre, en ese orden.</summary>
    private async Task<int?> PersonaFirmanteAsync(
        IReadOnlyDictionary<string, string> datos,
        CancellationToken ct)
    {
        foreach (var prefijo in new[] { "apoderado principal", "madre", "padre" })
        {
            var rut = NormalizarRut(Texto(datos, $"{prefijo}: cédula identidad"));

            if (rut is null || !RutValido(rut))
            {
                continue;
            }

            var id = await _db.Personas
                .Where(p => p.Identificador == rut)
                .Select(p => p.Id)
                .FirstOrDefaultAsync(ct);

            if (id != 0)
            {
                return id;
            }
        }

        return null;
    }

    private async Task<Estudiante?> EstudianteActualAsync(
        IReadOnlyDictionary<string, string> datos,
        List<string> avisos,
        CancellationToken ct)
    {
        var tipo = TipoDocumento(Texto(datos, "tipo identificador"));
        var documento = NormalizarDocumento(tipo, Texto(datos, "cédula de identidad"));
        var estudiante = documento is null
            ? null
            : await _db.Estudiantes.FirstOrDefaultAsync(e => e.Identificador == documento, ct);

        if (estudiante is null)
        {
            avisos.Add("El estudiante todavía no está registrado en la base: complete el paso 1 con un documento válido.");
        }

        return estudiante;
    }

    /// <summary>Busca o crea la persona de un bloque (madre, padre, apoderado).</summary>
    private async Task<Persona?> AsegurarPersonaAsync(
        IReadOnlyDictionary<string, string> datos,
        string prefijo,
        string etiqueta,
        List<string> avisos,
        CancellationToken ct)
    {
        var nombre = Texto(datos, $"{prefijo}: nombre apellido");
        var rut = NormalizarRut(Texto(datos, $"{prefijo}: cédula identidad"));
        var telefono = Texto(datos, $"{prefijo}: teléfono");
        var escolaridad = Texto(datos, $"{prefijo}: escolaridad");
        var domicilio = Texto(datos, $"{prefijo}: domicilio");
        var correo = Texto(datos, $"{prefijo}: correo");

        if (nombre is null && telefono is null && escolaridad is null && domicilio is null && correo is null)
        {
            return null;
        }

        if (rut is null)
        {
            avisos.Add($"Los datos de {etiqueta} quedaron sólo en el borrador: falta su RUN para registrarlos.");
            return null;
        }

        if (!RutValido(rut))
        {
            avisos.Add($"El RUN «{rut}» de {etiqueta} no es válido y sus datos no se guardaron.");
            return null;
        }

        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.Identificador == rut, ct);

        if (persona is null)
        {
            persona = new Persona
            {
                TipoIdentificador = "RUN",
                Identificador = rut,
                Nombres = nombre ?? rut,
                Apellidos = string.Empty,
            };

            _db.Personas.Add(persona);
        }
        else if (nombre is not null)
        {
            persona.Nombres = nombre;
        }

        persona.Telefono = telefono ?? persona.Telefono;
        persona.Escolaridad = escolaridad ?? persona.Escolaridad;
        persona.Domicilio = domicilio ?? persona.Domicilio;
        persona.Correo = correo ?? persona.Correo;

        return persona;
    }

    /// <summary>
    /// Deja vigente la relación estudiante–persona del bloque que se está guardando,
    /// ya sea por parentesco (madre/padre) o por tipo de apoderado.
    /// </summary>
    private async Task<EstudiantePersona> AsegurarRelacionAsync(
        int estudianteId,
        Persona persona,
        string parentesco,
        string? vinculo,
        string? tipoApoderado,
        List<string> avisos,
        CancellationToken ct)
    {
        var relaciones = await _db.EstudiantesPersonas
            .Where(r => r.EstudianteId == estudianteId && r.VigenteHasta == null)
            .ToListAsync(ct);

        // Si la persona ya está vinculada (por ejemplo, la madre es además la
        // apoderada principal), se reutiliza ese vínculo en vez de duplicarlo.
        var relacion = persona.Id == 0
            ? null
            : relaciones.FirstOrDefault(r => r.PersonaId == persona.Id);

        relacion ??= tipoApoderado is null
            ? relaciones.FirstOrDefault(r => r.Parentesco == parentesco)
            : relaciones.FirstOrDefault(r => r.TipoApoderado == tipoApoderado && r.Parentesco == "OTRO");

        if (relacion is null)
        {
            // Sólo puede haber un apoderado principal y uno secundario: se libera
            // el tipo de apoderado de cualquier otro vínculo antes de asignarlo.
            if (tipoApoderado is not null)
            {
                foreach (var otro in relaciones.Where(r => r.TipoApoderado == tipoApoderado))
                {
                    otro.TipoApoderado = null;
                }
            }

            relacion = new EstudiantePersona
            {
                EstudianteId = estudianteId,
                Persona = persona,
                Parentesco = parentesco,
                Vinculo = vinculo,
                TipoApoderado = tipoApoderado,
                VigenteDesde = DateOnly.FromDateTime(DateTime.Now),
            };

            _db.EstudiantesPersonas.Add(relacion);
            return relacion;
        }

        relacion.Persona = persona;
        relacion.Vinculo = vinculo ?? relacion.Vinculo;

        if (tipoApoderado is not null)
        {
            foreach (var otro in relaciones.Where(r => r != relacion && r.TipoApoderado == tipoApoderado))
            {
                otro.TipoApoderado = null;
            }

            relacion.TipoApoderado = tipoApoderado;
        }
        else if (relacion.TipoApoderado is null)
        {
            // Un vínculo de apoderado conserva su parentesco OTRO.
            relacion.Parentesco = parentesco;
        }

        return relacion;
    }

    private async Task<Comuna?> ComunaPorNombreAsync(string nombre, CancellationToken ct)
    {
        var buscada = SinTildes(nombre);

        var comunas = await _db.Comunas.Select(c => new { c.Id, c.Nombre }).ToListAsync(ct);

        var encontrada = comunas.FirstOrDefault(c => SinTildes(c.Nombre) == buscada);

        return encontrada is null ? null : await _db.Comunas.FirstOrDefaultAsync(c => c.Id == encontrada.Id, ct);
    }

    private async Task<Curso?> CursoAsync(string? nivel, string? letra, CancellationToken ct)
    {
        if (nivel is null && letra is null)
        {
            return null;
        }

        var grado = new string((nivel ?? string.Empty).Where(char.IsDigit).ToArray());
        var cursos = await _db.Cursos.ToListAsync(ct);

        if (grado.Length == 0)
        {
            return letra is null
                ? null
                : cursos.FirstOrDefault(c => SinTildes(c.Nombre).EndsWith(SinTildes(letra), StringComparison.Ordinal));
        }

        var objetivo = SinTildes($"{grado} {letra}");

        return cursos.FirstOrDefault(c => SinTildes(c.Nombre) == objetivo)
            ?? cursos.FirstOrDefault(c => SinTildes(c.Nombre).StartsWith(SinTildes($"{grado} "), StringComparison.Ordinal));
    }

    // ── Cierre de la ficha (MAT-08) ──────────────────────────────────────────

    /// <summary>Resultado del cierre: expediente, versión, folio y número de versión.</summary>
    public sealed record CierreFicha(int EstudianteId, int MatriculaId, int FichaVersionId, string Folio, short Version);

    /// <summary>
    /// Cierra el borrador y genera la versión firmable: congela los datos en
    /// <c>ficha_version.datos_snapshot</c>, obtiene el folio institucional y deja
    /// la matrícula en FICHA_GENERADA. Devuelve null si falta el estudiante o su
    /// matrícula del año.
    /// </summary>
    public async Task<CierreFicha?> CerrarFichaAsync(
        IReadOnlyDictionary<string, string> datos,
        int anio,
        int funcionarioId,
        CancellationToken ct = default)
    {
        var documento = NormalizarDocumento(
            TipoDocumento(Texto(datos, "tipo identificador")),
            Texto(datos, "cédula de identidad"));

        if (documento is null)
        {
            return null;
        }

        var estudiante = await _db.Estudiantes.FirstOrDefaultAsync(e => e.Identificador == documento, ct);
        if (estudiante is null)
        {
            return null;
        }

        var matricula = await _db.Matriculas.FirstOrDefaultAsync(
            m => m.EstudianteId == estudiante.Id && m.Anio == anio && m.Estado != "ANULADA", ct);

        if (matricula is null)
        {
            return null;
        }

        var ultimaVersion = await _db.FichaVersiones
            .Where(v => v.MatriculaId == matricula.Id)
            .MaxAsync(v => (short?)v.NumeroVersion, ct) ?? 0;

        var version = (short)(ultimaVersion + 1);

        // MAT-18: desde la versión 2 la base exige el motivo de la corrección.
        var motivo = version > 1 ? Texto(datos, "corrección: motivo") : null;
        // siguiente_folio recibe SMALLINT: sin el cast, PostgreSQL no resuelve
        // la función al enviar el año como integer.
        var folio = await _db.Database
            .SqlQuery<string>($"SELECT siguiente_folio(CAST({anio} AS smallint)) AS \"Value\"")
            .SingleAsync(ct);

        var vigentes = await _db.FichaVersiones
            .Where(v => v.MatriculaId == matricula.Id && v.Estado == "VIGENTE")
            .ToListAsync(ct);

        foreach (var anterior in vigentes)
        {
            anterior.Estado = "REEMPLAZADA";
        }

        var ficha = new FichaVersion
        {
            MatriculaId = matricula.Id,
            NumeroVersion = version,
            Folio = folio,
            Estado = "VIGENTE",
            MotivoCorreccion = motivo,
            DatosSnapshot = JsonSerializer.Serialize(new
            {
                campos = new SortedDictionary<string, string>(
                    datos.ToDictionary(par => par.Key, par => par.Value)),
                generado_en = DateTimeOffset.UtcNow,
            }),
            GeneradaPor = funcionarioId,
            GeneradaEn = DateTimeOffset.UtcNow,
        };

        _db.FichaVersiones.Add(ficha);

        if (matricula.Estado == "BORRADOR")
        {
            matricula.Estado = "FICHA_GENERADA";
            matricula.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        // MAT-14: cada autorización queda ligada a la ficha en que se firmó. Las
        // que ya estaban ligadas a una versión anterior conservan ese vínculo.
        var autorizaciones = await _db.Autorizaciones
            .Where(a => a.EstudianteId == estudiante.Id && a.VigenteHasta == null && a.FichaVersionId == null)
            .ToListAsync(ct);

        foreach (var autorizacion in autorizaciones)
        {
            autorizacion.FichaVersionId = ficha.Id;
        }

        if (autorizaciones.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new CierreFicha(estudiante.Id, matricula.Id, ficha.Id, folio, version);
    }

    /// <summary>Versión vigente de la ficha para un estudiante y año, si existe.</summary>
    public Task<FichaVersion?> FichaVigenteAsync(int estudianteId, int anio, CancellationToken ct = default) =>
        _db.FichaVersiones
            .Join(
                _db.Matriculas.Where(m => m.EstudianteId == estudianteId && m.Anio == anio),
                version => version.MatriculaId,
                matricula => matricula.Id,
                (version, _) => version)
            .Where(version => version.Estado == "VIGENTE")
            .OrderByDescending(version => version.NumeroVersion)
            .FirstOrDefaultAsync(ct);

    /// <summary>Recupera los campos congelados de una versión cerrada.</summary>
    public static Dictionary<string, string> DatosDesdeSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            using var documento = JsonDocument.Parse(json);

            if (documento.RootElement.TryGetProperty("campos", out var campos)
                && campos.ValueKind == JsonValueKind.Object)
            {
                return campos.Deserialize<Dictionary<string, string>>() ?? new Dictionary<string, string>();
            }
        }
        catch (JsonException)
        {
            // Un snapshot ilegible no debe romper la consulta: se trata como vacío.
        }

        return new Dictionary<string, string>();
    }

    /// <summary>
    /// Registra la recepción de la ficha firmada (BORRADOR → FICHA_GENERADA →
    /// FIRMADA). El escaneo del documento firmado se agrega en la Etapa II.
    /// </summary>
    public async Task<string?> RegistrarFirmaAsync(
        int matriculaId,
        DateOnly fechaFirma,
        CancellationToken ct = default)
    {
        var matricula = await _db.Matriculas.FirstOrDefaultAsync(m => m.Id == matriculaId, ct);

        if (matricula is null)
        {
            return "No se encontró la matrícula seleccionada.";
        }

        if (matricula.Estado == "FIRMADA")
        {
            return null;
        }

        if (matricula.Estado != "FICHA_GENERADA")
        {
            return "Sólo se registra la firma de una matrícula con la ficha generada.";
        }

        var ficha = await _db.FichaVersiones
            .Where(v => v.MatriculaId == matriculaId && v.Estado == "VIGENTE")
            .OrderByDescending(v => v.NumeroVersion)
            .FirstOrDefaultAsync(ct);

        if (ficha is null)
        {
            return "La matrícula no tiene una ficha vigente: genere la ficha antes de registrar la firma.";
        }

        // La ficha cerrada es inmutable (trigger del DDL): la fecha de la firma
        // queda en el evento de auditoría y el estado de la matrícula.
        matricula.Estado = "FIRMADA";
        matricula.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return null;
    }

    /// <summary>Confirma la matrícula según el procedimiento oficial (MAT-04).</summary>
    public async Task<string?> ConfirmarAsync(int matriculaId, int funcionarioId, CancellationToken ct = default)
    {
        var matricula = await _db.Matriculas.FirstOrDefaultAsync(m => m.Id == matriculaId, ct);

        if (matricula is null)
        {
            return "No se encontró la matrícula seleccionada.";
        }

        if (matricula.Estado == "CONFIRMADA")
        {
            return null;
        }

        if (matricula.Estado is not ("FICHA_GENERADA" or "FIRMADA"))
        {
            return "Sólo se confirma una matrícula con la ficha generada o firmada.";
        }

        var tieneFicha = await _db.FichaVersiones
            .AnyAsync(v => v.MatriculaId == matriculaId && v.Estado == "VIGENTE", ct);

        if (!tieneFicha)
        {
            return "La matrícula no tiene una ficha vigente: genere la ficha antes de confirmar.";
        }

        matricula.Estado = "CONFIRMADA";
        matricula.ConfirmadaEn = DateTimeOffset.UtcNow;
        matricula.ConfirmadaPor = funcionarioId;
        matricula.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return null;
    }

    /// <summary>
    /// Anula la matrícula del año (BORRADOR, FICHA_GENERADA o FIRMADA →
    /// ANULADA). La anulación exige motivo, que queda en las observaciones, y
    /// permite iniciar una matrícula nueva porque el índice único de la base
    /// ignora las anuladas. La matrícula confirmada no se anula por esta vía.
    /// </summary>
    public async Task<string?> AnularAsync(int matriculaId, string? motivo, CancellationToken ct = default)
    {
        var matricula = await _db.Matriculas.FirstOrDefaultAsync(m => m.Id == matriculaId, ct);

        if (matricula is null)
        {
            return "No se encontró la matrícula seleccionada.";
        }

        if (matricula.Estado == "ANULADA")
        {
            return null;
        }

        if (matricula.Estado == "CONFIRMADA")
        {
            return "La matrícula confirmada no se puede anular: solicite el procedimiento de Dirección.";
        }

        if (matricula.Estado is not ("BORRADOR" or "FICHA_GENERADA" or "FIRMADA"))
        {
            return "Sólo se anula una matrícula en borrador, con ficha generada o firmada.";
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return "Indique el motivo de la anulación.";
        }

        matricula.Estado = "ANULADA";
        matricula.Observaciones = motivo.Trim();
        matricula.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return null;
    }

    /// <summary>
    /// Validación institucional de Dirección (MAT-12): acto distinto de
    /// confirmar, con fecha y responsable. Sólo sobre matrícula formalizada
    /// (FIRMADA o CONFIRMADA); es idempotente y queda auditada por trigger.
    /// </summary>
    public async Task<string?> ValidarAsync(int matriculaId, int funcionarioId, CancellationToken ct = default)
    {
        var matricula = await _db.Matriculas.FirstOrDefaultAsync(m => m.Id == matriculaId, ct);

        if (matricula is null)
        {
            return "No se encontró la matrícula seleccionada.";
        }

        if (matricula.ValidadaEn is not null)
        {
            return null;
        }

        if (matricula.Estado is not ("FIRMADA" or "CONFIRMADA"))
        {
            return "Sólo se valida una matrícula firmada o confirmada.";
        }

        matricula.ValidadaEn = DateTimeOffset.UtcNow;
        matricula.ValidadaPor = funcionarioId;
        matricula.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return null;
    }

    /// <summary>
    /// MAT-04: pasa a VENCIDA las autorizaciones otorgadas cuyo plazo terminó.
    /// Devuelve cuántas cambió, para registrar el evento sólo cuando hubo algo.
    /// </summary>
    public async Task<int> MarcarVencidasAsync(int estudianteId, DateOnly hoy, CancellationToken ct = default)
    {
        var vencidas = await _db.Autorizaciones
            .Where(a => a.EstudianteId == estudianteId
                        && a.Estado == "OTORGADA"
                        && a.VigenteHasta != null
                        && a.VigenteHasta < hoy)
            .ToListAsync(ct);

        foreach (var autorizacion in vencidas)
        {
            autorizacion.Estado = "VENCIDA";
        }

        if (vencidas.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return vencidas.Count;
    }

    /// <summary>
    /// MAT-04: revoca una autorización otorgada. Se conserva la firma y la fecha
    /// originales y se registra el motivo, quién la revocó y desde cuándo rige.
    /// </summary>
    public async Task<string?> RevocarAutorizacionAsync(
        int autorizacionId,
        int estudianteId,
        string? motivo,
        int funcionarioId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return "Indique el motivo de la revocación.";
        }

        var autorizacion = await _db.Autorizaciones
            .FirstOrDefaultAsync(a => a.Id == autorizacionId && a.EstudianteId == estudianteId, ct);

        if (autorizacion is null)
        {
            return "No se encontró la autorización seleccionada.";
        }

        if (autorizacion.Estado == "REVOCADA")
        {
            return null;
        }

        if (autorizacion.Estado != "OTORGADA")
        {
            return "Sólo se revoca una autorización otorgada y vigente.";
        }

        var hoy = DateOnly.FromDateTime(DateTime.Now);

        autorizacion.Estado = "REVOCADA";
        autorizacion.RevocadaEn = DateTimeOffset.UtcNow;
        autorizacion.RevocadaPor = funcionarioId;
        autorizacion.MotivoRevocacion = motivo.Trim();
        autorizacion.VigenteHasta = autorizacion.VigenteDesde is DateOnly desde && desde > hoy ? desde : hoy;

        await _db.SaveChangesAsync(ct);

        return null;
    }

    private static string TipoDocumento(string? tipo)
    {
        var limpio = (tipo ?? "RUN").Trim().ToUpperInvariant();

        return limpio is "IPE" or "IPA" ? limpio : "RUN";
    }

    private static string? NormalizarDocumento(string tipo, string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            return null;
        }

        return tipo == "RUN"
            ? RutChileno.Limpiar(documento)
            : documento.Trim().ToUpperInvariant();
    }

    private static bool DocumentoValido(string tipo, string documento) =>
        tipo == "RUN"
            ? RutChileno.EsValido(documento)
            : IdentificadorAttribute.EsValido(documento, tipo);

    private static bool SinAlergias(string texto)
    {
        var limpio = SinTildes(texto);

        return limpio.StartsWith("ningun", StringComparison.Ordinal)
            || limpio.StartsWith("sin alerg", StringComparison.Ordinal)
            || limpio is "no" or "ninguna";
    }

    private static string? Texto(IReadOnlyDictionary<string, string> datos, string clave)
    {
        var valor = datos.GetValueOrDefault(clave);

        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    private static bool? SiNo(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var limpio = SinTildes(valor).ToUpperInvariant();

        return limpio.StartsWith('S') ? true : limpio.StartsWith('N') ? false : null;
    }

    private static DateOnly? Fecha(string? valor) =>
        DateOnly.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : null;

    /// <summary>Deja el RUN sin puntos, sin espacios y con guion, como lo guarda la base.</summary>
    private static string? NormalizarRut(string? rut) =>
        string.IsNullOrWhiteSpace(rut) ? null : RutChileno.Limpiar(rut);

    /// <summary>Valida el dígito verificador del RUN (módulo 11).</summary>
    private static bool RutValido(string rut) => RutChileno.EsValido(rut);

    private static string SinTildes(string valor)
    {
        var descompuesto = valor.Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(caracter);

            if (categoria != UnicodeCategory.NonSpacingMark)
            {
                limpio.Append(char.IsLetterOrDigit(caracter) ? char.ToLowerInvariant(caracter) : ' ');
            }
        }

        return string.Join(' ', limpio.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
