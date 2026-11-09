using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Seguridad;
using MarcelaPaz.Web.Servicios;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Registro de alumnos: listado con búsqueda y filtros, y la ficha de consulta
/// de cada estudiante. La extracción de documentos queda pendiente: los botones
/// se muestran deshabilitados hasta acordar con el colegio qué documentos se
/// pueden obtener.
/// </summary>
[Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:LEER")]
public class AlumnosController : Controller
{
    private readonly MatriculaDbContext _db;
    private readonly RegistroFicha _registro;
    private readonly IAuditoriaAcceso _auditoria;
    private readonly AnioEscolar _anios;
    private readonly EscrituraAuditada _escritura;
    private readonly ExpedienteDocumentos _documentos;

    public AlumnosController(
        MatriculaDbContext db,
        RegistroFicha registro,
        IAuditoriaAcceso auditoria,
        AnioEscolar anios,
        EscrituraAuditada escritura,
        ExpedienteDocumentos documentos)
    {
        _db = db;
        _registro = registro;
        _auditoria = auditoria;
        _anios = anios;
        _escritura = escritura;
        _documentos = documentos;
    }

    /// <summary>¿La sesión tiene el permiso de área indicado?</summary>
    private bool Puede(string area) => User.HasClaim("permiso", $"{area}:LEER");

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? curso, string? estado, string? orden, string? dir, int? anio, bool pendientes = false)
    {
        var anios = await _anios.ListarAsync();
        var anioConsulta = await _anios.ResolverAsync(anio);

        if (anioConsulta is null)
        {
            TempData["Aviso"] =
                "No hay un año escolar con matrícula activa. Secretaría o Dirección debe definirlo en Configuración → Año escolar.";

            return View(new FiltroAlumnos { Anios = anios.Select(opcion => opcion.Anio).ToList() });
        }

        var estudiantes = await _db.Estudiantes
            .AsNoTracking()
            .Include(e => e.Comuna)
            .OrderBy(e => e.ApellidoPaterno)
            .ThenBy(e => e.Nombres)
            .ToListAsync();

        var matriculas = await _db.Matriculas
            .AsNoTracking()
            .Where(m => m.Anio == anioConsulta)
            .Select(m => new
            {
                m.Id,
                m.EstudianteId,
                m.Estado,
                m.Tipo,
                Curso = m.Curso != null ? m.Curso.Nombre : null,
            })
            .ToListAsync();

        var delAnio = matriculas
            .GroupBy(m => m.EstudianteId)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.First());

        // MAT-11: se marca como pendiente la matrícula sin ficha vigente o con
        // requisitos documentales todavía en PENDIENTE. Las dos consultas se
        // hacen en bloque para no repetir una por alumno.
        var idsMatriculas = delAnio.Values.Select(m => m.Id).ToList();

        var conFicha = idsMatriculas.Count == 0
            ? new HashSet<int>()
            : (await _db.FichaVersiones
                .AsNoTracking()
                .Where(v => idsMatriculas.Contains(v.MatriculaId) && v.Estado == "VIGENTE")
                .Select(v => v.MatriculaId)
                .Distinct()
                .ToListAsync())
            .ToHashSet();

        var documentosPendientes = idsMatriculas.Count == 0
            ? new Dictionary<int, int>()
            : await _db.RequisitosDocumentales
                .AsNoTracking()
                .Where(r => idsMatriculas.Contains(r.MatriculaId) && r.Estado == "PENDIENTE")
                .GroupBy(r => r.MatriculaId)
                .Select(grupo => new { MatriculaId = grupo.Key, Total = grupo.Count() })
                .ToDictionaryAsync(fila => fila.MatriculaId, fila => fila.Total);

        var todos = estudiantes
            .Select(estudiante =>
            {
                delAnio.TryGetValue(estudiante.Id, out var matricula);

                var documentos = matricula is null ? 0 : documentosPendientes.GetValueOrDefault(matricula.Id);

                return new AlumnoListado
                {
                    Id = estudiante.Id,
                    Nombre = estudiante.NombreCompleto,
                    TipoIdentificador = estudiante.TipoIdentificador,
                    Identificador = estudiante.Identificador,
                    // Sólo el RUN se formatea: un IPE o IPA no tiene dígito verificador.
                    Rut = string.Equals(estudiante.TipoIdentificador, "RUN", StringComparison.OrdinalIgnoreCase)
                        ? RutChileno.Formatear(estudiante.Identificador)
                        : estudiante.Identificador.ToUpperInvariant(),
                    Curso = matricula?.Curso,
                    Estado = matricula?.Estado,
                    Tipo = matricula?.Tipo,
                    Comuna = estudiante.Comuna?.Nombre,
                    SinFicha = matricula is not null && !conFicha.Contains(matricula.Id),
                    DocumentosPendientes = documentos,
                };
            })
            .ToList();

        // Sólo se aceptan las columnas conocidas; cualquier otra vuelve a nombre.
        var columnas = new[] { "nombre", "rut", "curso", "estado", "tipo", "comuna", "pendientes" };
        var ordenPedido = (orden ?? string.Empty).ToLowerInvariant();
        var ordenValido = columnas.Contains(ordenPedido) ? ordenPedido : "nombre";
        var descendente = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

        var filtro = new FiltroAlumnos
        {
            Anio = anioConsulta.Value,
            Anios = anios.Select(opcion => opcion.Anio).ToList(),
            Texto = q?.Trim(),
            Curso = string.IsNullOrWhiteSpace(curso) ? null : curso,
            Estado = string.IsNullOrWhiteSpace(estado) ? null : estado,
            SoloPendientes = pendientes,
            Orden = ordenValido,
            Direccion = descendente ? "desc" : "asc",
            Cursos = todos
                .Where(a => !string.IsNullOrWhiteSpace(a.Curso))
                .Select(a => a.Curso!)
                .Distinct()
                .OrderBy(nombre => nombre, StringComparer.CurrentCulture)
                .ToList(),
            Estados = todos
                .Where(a => a.TieneMatricula)
                .Select(a => a.Estado!)
                .Distinct()
                .OrderBy(nombre => nombre)
                .ToList(),
        };

        if (todos.Any(a => !a.TieneMatricula))
        {
            filtro.Estados.Add("Sin matrícula");
        }

        var filas = todos.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            // Sin tildes y en mayúsculas: «gonzalez» encuentra «González».
            var texto = SinTildes(filtro.Texto).ToUpperInvariant();
            var soloDocumento = filtro.Texto
                .Replace(".", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToUpperInvariant();

            filas = filas.Where(a =>
                SinTildes(a.Nombre).ToUpperInvariant().Contains(texto, StringComparison.Ordinal)
                || a.Identificador
                    .Replace(".", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace(" ", string.Empty)
                    .StartsWith(soloDocumento, StringComparison.OrdinalIgnoreCase));
        }

        if (filtro.Curso is not null)
        {
            filas = filas.Where(a => a.Curso == filtro.Curso);
        }

        if (filtro.Estado is not null)
        {
            filas = filas.Where(a => a.EstadoTexto == filtro.Estado);
        }

        if (filtro.SoloPendientes)
        {
            filas = filas.Where(a => a.TienePendientes);
        }

        filtro.Alumnos = Ordenar(filas, filtro.Orden, filtro.Direccion == "desc").ToList();

        // MAT-13: una búsqueda permite barrer el padrón. Se registra el término
        // en hash (no el RUN consultado) junto con el número de resultados.
        if (!string.IsNullOrWhiteSpace(q))
        {
            var huella = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(q.Trim().ToUpperInvariant())))[..12];

            await _auditoria.RegistrarAsync(
                "BUSCAR",
                "GENERAL",
                "alumnos_busqueda",
                detalle: $"{{\"termino\":\"{huella}\",\"resultados\":{filtro.Alumnos.Count}}}");
        }

        return View(filtro);
    }

    /// <summary>Ficha de consulta del alumno: matrículas, apoderados, retiros y salud.</summary>
    [HttpGet]
    public async Task<IActionResult> Detalle(int id, int? anio)
    {
        var anioConsulta = await _anios.ResolverAsync(anio);

        if (anioConsulta is null)
        {
            TempData["Aviso"] =
                "No hay un año escolar con matrícula activa. Secretaría o Dirección debe definirlo en Configuración → Año escolar.";

            return RedirectToAction(nameof(Index));
        }

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

        // Permisos por área (S-02): la consulta de alumnos se abre con
        // GENERAL:LEER, pero cada bloque sensible exige su propio permiso y, si
        // falta, ni se consulta ni se proyecta.
        var puedeFamilia = Puede("FAMILIA");
        var puedeRetiro = Puede("RETIRO");
        var puedeSalud = Puede("SALUD");
        var puedePie = Puede("PIE");
        var puedeAutorizaciones = Puede("AUTORIZACIONES");
        var puedeDocumentos = Puede("DOCUMENTOS");
        var puedeSubirDocumentos = User.HasClaim("permiso", "DOCUMENTOS:CREAR");
        var puedeEditarAutorizaciones = User.HasClaim("permiso", "AUTORIZACIONES:EDITAR");
        var puedeCrearMatricula = User.HasClaim("permiso", "GENERAL:CREAR");
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // MAT-03 / MAT-14: se leen también los vínculos cerrados, para mostrar los
        // cambios de apoderado sin perder el historial.
        var vinculosTodos = puedeFamilia
            ? await _db.EstudiantesPersonas
                .AsNoTracking()
                .Include(v => v.Persona)
                .Where(v => v.EstudianteId == id)
                .OrderBy(v => v.Id)
                .ToListAsync()
            : new List<EstudiantePersona>();

        var vinculos = vinculosTodos.Where(v => v.VigenteHasta == null).ToList();
        var vinculosAnteriores = vinculosTodos
            .Where(v => v.VigenteHasta is not null)
            .OrderByDescending(v => v.VigenteHasta)
            .ThenBy(v => v.Id)
            .ToList();

        var idsVinculos = vinculos.Select(v => v.Id).ToList();

        var restricciones = puedeFamilia
            ? await _db.RestriccionesFamiliares
                .AsNoTracking()
                .Where(r => idsVinculos.Contains(r.EstudiantePersonaId) && r.VigenteHasta == null)
                .ToListAsync()
            : new List<RestriccionFamiliar>();

        var contactos = puedeFamilia
            ? await _db.ContactosEmergencia
                .AsNoTracking()
                .Where(c => c.EstudianteId == id && c.VigenteHasta == null)
                .OrderBy(c => c.Orden)
                .ToListAsync()
            : new List<ContactoEmergencia>();

        var retiros = puedeRetiro
            ? await _db.RetirosAutorizados
                .AsNoTracking()
                .Where(r => r.EstudianteId == id && r.VigenteHasta == null)
                .OrderBy(r => r.Id)
                .ToListAsync()
            : new List<RetiroAutorizado>();

        var salud = puedeSalud
            ? await _db.AntecedentesSalud
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.EstudianteId == id)
            : null;

        var apoyo = puedePie
            ? await _db.AntecedentesApoyo
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.EstudianteId == id)
            : null;

        var situaciones = puedeSalud
            ? await _db.SituacionesMedicas
                .AsNoTracking()
                .Where(s => s.EstudianteId == id && s.VigenteHasta == null)
                .OrderBy(s => s.Id)
                .ToListAsync()
            : new List<SituacionMedica>();

        var vigente = matriculas.FirstOrDefault(m => m.Anio == anioConsulta);

        var ficha = vigente is null
            ? null
            : await _db.FichaVersiones
                .AsNoTracking()
                .Where(v => v.MatriculaId == vigente.Id && v.Estado == "VIGENTE")
                .OrderByDescending(v => v.NumeroVersion)
                .FirstOrDefaultAsync();

        // MAT-14: historial completo de versiones de la matrícula consultada.
        var versiones = vigente is null
            ? new List<FichaVersion>()
            : await _db.FichaVersiones
                .AsNoTracking()
                .Where(v => v.MatriculaId == vigente.Id)
                .OrderByDescending(v => v.NumeroVersion)
                .ToListAsync();

        var idsGeneradores = versiones.Select(v => v.GeneradaPor).Distinct().ToList();

        var nombresFuncionarios = idsGeneradores.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Funcionarios
                .AsNoTracking()
                .Where(f => idsGeneradores.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.Nombre);

        // Última actualización del expediente: la escribe el trigger de
        // auditoría en cada cambio de la matrícula, con el funcionario de la
        // sesión. Sin historial se muestra updated_at y quien la creó.
        var ultimoCambio = vigente is null
            ? null
            : await _db.HistorialCambios
                .AsNoTracking()
                .Where(h => h.Tabla == "matricula_anual" && h.RegistroId == vigente.Id)
                .OrderByDescending(h => h.CambiadoEn)
                .FirstOrDefaultAsync();

        var idsResponsables = new List<int>();

        if (vigente?.CreadaPor is int creadaPor)
        {
            idsResponsables.Add(creadaPor);
        }

        if (vigente?.ConfirmadaPor is int confirmadaPor)
        {
            idsResponsables.Add(confirmadaPor);
        }

        if (vigente?.ValidadaPor is int validadaPor)
        {
            idsResponsables.Add(validadaPor);
        }

        if (ultimoCambio?.FuncionarioId is int responsableId)
        {
            idsResponsables.Add(responsableId);
        }

        var nombresResponsables = idsResponsables.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Funcionarios
                .AsNoTracking()
                .Where(f => idsResponsables.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.Nombre);

        // MAT-04: antes de listar, las autorizaciones otorgadas cuyo plazo ya
        // terminó pasan a VENCIDA en la base (sólo cuando hay alguna).
        if (puedeAutorizaciones
            && int.TryParse(User.FindFirstValue("funcionario_id"), out var funcionarioLectura))
        {
            var hayVencidas = await _db.Autorizaciones
                .AsNoTracking()
                .AnyAsync(a => a.EstudianteId == id
                               && a.Estado == "OTORGADA"
                               && a.VigenteHasta != null
                               && a.VigenteHasta < hoy);

            if (hayVencidas)
            {
                await _escritura.EjecutarAsync(
                    funcionarioLectura,
                    ct => _registro.MarcarVencidasAsync(id, hoy, ct),
                    HttpContext.RequestAborted);
            }
        }

        // MAT-04 / MAT-14: se listan todas las autorizaciones, no sólo las
        // vigentes, para conservar el historial de respuestas y revocaciones.
        var autorizaciones = puedeAutorizaciones
            ? await _db.Autorizaciones
                .AsNoTracking()
                .Where(a => a.EstudianteId == id)
                .OrderBy(a => a.Tipo)
                .ThenByDescending(a => a.VigenteDesde)
                .ThenByDescending(a => a.Id)
                .ToListAsync()
            : new List<Autorizacion>();

        var idsAutorizaciones = autorizaciones.Select(a => a.Id).ToList();

        var alcances = idsAutorizaciones.Count == 0
            ? new List<AutorizacionAlcance>()
            : await _db.AutorizacionesAlcance
                .AsNoTracking()
                .Where(a => idsAutorizaciones.Contains(a.AutorizacionId))
                .ToListAsync();

        // MAT-06/07/09: requisitos de la matrícula del año. Sin DOCUMENTOS:LEER no
        // se consultan ni se proyectan.
        var requisitos = vigente is not null && puedeDocumentos
            ? await _db.RequisitosDocumentales
                .AsNoTracking()
                .Where(r => r.MatriculaId == vigente.Id)
                .OrderBy(r => r.TipoDocumento)
                .ToListAsync()
            : new List<RequisitoDocumental>();

        var idsDocumentos = requisitos
            .Where(r => r.DocumentoId is not null)
            .Select(r => r.DocumentoId!.Value)
            .ToList();

        var documentos = idsDocumentos.Count == 0
            ? new List<Documento>()
            : await _db.Documentos
                .AsNoTracking()
                .Where(d => idsDocumentos.Contains(d.Id))
                .ToListAsync();

        var fichaFirmada = vigente is not null && puedeDocumentos
            ? await _db.Documentos
                .AsNoTracking()
                .Where(d => d.MatriculaId == vigente.Id && d.Tipo == "FICHA_FIRMADA" && d.Activo)
                .OrderByDescending(d => d.Id)
                .FirstOrDefaultAsync()
            : null;

        var perfil = new PerfilAlumno
        {
            Anio = anioConsulta.Value,
            Id = estudiante.Id,
            Nombre = estudiante.NombreCompleto,
            Rut = RutChileno.Formatear(estudiante.Identificador),
            FechaNacimiento = estudiante.FechaNacimiento,
            Edad = Edad(estudiante.FechaNacimiento),
            Nacionalidad = estudiante.Nacionalidad,
            Domicilio = estudiante.Domicilio,
            Comuna = estudiante.Comuna?.Nombre,
            SaleSolo = estudiante.SaleSoloFinalJornada,
            TieneSituacionMedica = salud?.TieneSituacionMedica,
            NecesidadesAtencion = salud?.NecesidadesAtencion,
            Matriculas = matriculas
                .Select(m => new PerfilAlumno.MatriculaPerfil
                {
                    Anio = m.Anio,
                    Curso = m.Curso?.Nombre,
                    Estado = m.Estado,
                    Tipo = m.Tipo,
                })
                .ToList(),
            Apoderados = vinculos
                .Where(v => v.Persona is not null)
                .Select(v => new PerfilAlumno.ApoderadoPerfil
                {
                    Nombre = NombrePersona(v.Persona!),
                    Rut = RutChileno.Formatear(v.Persona!.Identificador),
                    Vinculo = EtiquetaVinculo(v),
                    Telefono = v.Persona!.Telefono,
                    Correo = v.Persona!.Correo,
                    TieneRestriccion = v.TieneRestriccion == true,
                })
                .ToList(),
            Contactos = contactos
                .Select(c => new PerfilAlumno.ContactoPerfil
                {
                    Nombre = c.Nombre,
                    Telefono = c.Telefono,
                    Vinculo = c.Vinculo,
                })
                .ToList(),
            Retiros = retiros
                .Select(r => new PerfilAlumno.RetiroPerfil
                {
                    Nombre = r.Nombre,
                    Rut = RutChileno.Formatear(r.Identificador),
                    Vinculo = r.Vinculo,
                })
                .ToList(),
            Situaciones = situaciones
                .Select(s => new PerfilAlumno.SituacionPerfil
                {
                    Tipo = s.Tipo,
                    Descripcion = s.Descripcion,
                    EnTratamiento = s.EnTratamiento,
                })
                .ToList(),
            Restricciones = restricciones
                .Select(r => $"{r.Tipo.ToLowerInvariant()}: {r.Descripcion}")
                .ToList(),
            ApoderadosAnteriores = vinculosAnteriores
                .Where(v => v.Persona is not null)
                .Select(v => new PerfilAlumno.ApoderadoPerfil
                {
                    Nombre = NombrePersona(v.Persona!),
                    Rut = RutChileno.Formatear(v.Persona!.Identificador),
                    Vinculo = EtiquetaVinculo(v),
                    Telefono = v.Persona!.Telefono,
                    Correo = v.Persona!.Correo,
                    TieneRestriccion = v.TieneRestriccion == true,
                    Hasta = v.VigenteHasta,
                })
                .ToList(),
            PieParticipa = apoyo?.ParticipaPie,
            PieDiagnostico = apoyo?.TieneDiagnostico,
            PieApoyos = apoyo?.ApoyosInformados,
            Alergias = situaciones
                .Where(s => s.Tipo == "ALERGIA")
                .Select(s => s.Descripcion)
                .FirstOrDefault(),
            ContactoSanitarioNombre = salud?.ContactoSanitarioNombre,
            ContactoSanitarioTelefono = salud?.ContactoSanitarioTelefono,
            Versiones = versiones
                .Select(v => new PerfilAlumno.VersionPerfil
                {
                    Id = v.Id,
                    Numero = v.NumeroVersion,
                    Folio = v.Folio,
                    Estado = v.Estado,
                    GeneradaEn = v.GeneradaEn,
                    GeneradaPor = nombresFuncionarios.GetValueOrDefault(v.GeneradaPor),
                    MotivoCorreccion = v.MotivoCorreccion,
                })
                .ToList(),
            FolioFicha = ficha?.Folio,
            VersionFicha = ficha?.NumeroVersion,
            MatriculaId = vigente?.Id,
            PuedeConfirmar = vigente?.Estado is "FICHA_GENERADA" or "FIRMADA",
            PuedeAnular = vigente?.Estado is "BORRADOR" or "FICHA_GENERADA" or "FIRMADA",
            PuedeValidar = vigente?.Estado is "FIRMADA" or "CONFIRMADA"
                && vigente.ValidadaEn is null
                && User.HasClaim("permiso", "GENERAL:VALIDAR"),
            Autorizaciones = autorizaciones
                .Select(a =>
                {
                    var estado = Autorizaciones.EstadoEfectivo(a.Estado, a.VigenteHasta, hoy);

                    return new PerfilAlumno.AutorizacionPerfil
                    {
                        Id = a.Id,
                        Tipo = a.Tipo,
                        Etiqueta = EtiquetaAutorizacion(a.Tipo),
                        Estado = estado,
                        EstadoEtiqueta = Autorizaciones.EtiquetaEstado(estado),
                        FechaFirma = a.FechaFirma,
                        VigenteDesde = a.VigenteDesde,
                        VigenteHasta = a.VigenteHasta,
                        RevocadaEn = a.RevocadaEn,
                        MotivoRevocacion = a.MotivoRevocacion,
                        Alcances = alcances
                            .Where(x => x.AutorizacionId == a.Id)
                            .Select(x => $"{EtiquetaAlcance(x.Alcance)}: {(x.Otorgado ? "sí" : "no")}")
                            .ToList(),
                    };
                })
                .ToList(),
            PuedeVerFamilia = puedeFamilia,
            PuedeVerRetiro = puedeRetiro,
            PuedeVerSalud = puedeSalud,
            PuedeVerPie = puedePie,
            PuedeVerAutorizaciones = puedeAutorizaciones,
            PuedeEditarAutorizaciones = puedeEditarAutorizaciones,
            PuedeCorregirFicha = puedeCrearMatricula && ficha is not null,
            // Tras una anulación se permite iniciar una matrícula nueva: el
            // índice único de la base ignora las anuladas.
            PuedeRenovarAnio = puedeCrearMatricula && (vigente is null || vigente.Estado == "ANULADA"),
            PuedeVerDocumentos = puedeDocumentos,
            PuedeSubirDocumentos = puedeSubirDocumentos,
            FichaFirmadaDocumentoId = fichaFirmada?.Id,
            FichaFirmadaNombre = fichaFirmada?.NombreOriginal,
            FichaFirmadaSubidaEn = fichaFirmada?.SubidoEn,
            Documentos = requisitos
                .Select(r => new PerfilAlumno.DocumentoPerfil
                {
                    RequisitoId = r.Id,
                    Tipo = r.TipoDocumento,
                    Etiqueta = CatalogoDocumentos.Etiqueta(r.TipoDocumento),
                    Estado = r.Estado,
                    DocumentoId = documentos.FirstOrDefault(d => d.Id == r.DocumentoId)?.Id,
                    NombreArchivo = documentos.FirstOrDefault(d => d.Id == r.DocumentoId)?.NombreOriginal,
                    SubidoEn = documentos.FirstOrDefault(d => d.Id == r.DocumentoId)?.SubidoEn,
                    Sensibilidad = CatalogoDocumentos.Sensibilidad(r.TipoDocumento),
                    Observacion = r.Observacion,
                })
                .ToList(),
        };

        // MAT-13: la consulta del expediente deja rastro con las áreas efectivamente leídas.
        var areas = new List<string> { "GENERAL" };
        if (puedeFamilia)
        {
            areas.Add("FAMILIA");
        }

        if (puedeRetiro)
        {
            areas.Add("RETIRO");
        }

        if (puedeSalud)
        {
            areas.Add("SALUD");
        }

        if (puedeAutorizaciones)
        {
            areas.Add("AUTORIZACIONES");
        }

        if (puedeDocumentos)
        {
            areas.Add("DOCUMENTOS");
        }

        await _auditoria.RegistrarAsync(
            "LEER",
            "GENERAL",
            "alumnos_detalle",
            detalle: $"{{\"areas\":[{string.Join(",", areas.Select(area => $"\"{area}\""))}]}}",
            estudianteId: id);

        if (vigente is not null)
        {
            perfil.CursoActual = vigente.Curso?.Nombre;
            perfil.EstadoActual = vigente.Estado;
            perfil.ActualizadaEn = ultimoCambio?.CambiadoEn ?? vigente.UpdatedAt;

            var responsableActual = ultimoCambio?.FuncionarioId ?? vigente.CreadaPor;

            perfil.ActualizadaPor = responsableActual is int responsable
                ? nombresResponsables.GetValueOrDefault(responsable)
                : null;
            perfil.ValidadaEn = vigente.ValidadaEn;
            perfil.ValidadaPor = vigente.ValidadaPor is int validador
                ? nombresResponsables.GetValueOrDefault(validador)
                : null;
        }

        return View(perfil);
    }

    /// <summary>
    /// Confirma la matrícula según el procedimiento oficial (MAT-04): sólo desde
    /// FICHA_GENERADA o FIRMADA y con una ficha vigente.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:CREAR")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(int id)
    {
        var matricula = await _db.Matriculas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (matricula is null)
        {
            return NotFound();
        }

        var funcionarioId = int.TryParse(User.FindFirstValue("funcionario_id"), out var valor)
            ? valor
            : (int?)null;

        if (funcionarioId is null)
        {
            TempData["Aviso"] = "Inicie sesión como funcionario para confirmar la matrícula.";
            return RedirectToAction(nameof(Detalle), new { id = matricula.EstudianteId });
        }

        string? error = null;

        await _escritura.EjecutarAsync(funcionarioId.Value, async ct =>
        {
            error = await _registro.ConfirmarAsync(id, funcionarioId.Value, ct);
        }, HttpContext.RequestAborted);

        if (error is null)
        {
            TempData["Notificacion"] = "Matrícula confirmada según el procedimiento oficial.";
        }
        else
        {
            TempData["Aviso"] = error;
        }

        return RedirectToAction(nameof(Detalle), new { id = matricula.EstudianteId });
    }

    /// <summary>
    /// Anula la matrícula del año (BORRADOR, FICHA_GENERADA o FIRMADA →
    /// ANULADA). Exige motivo y permite iniciar una matrícula nueva.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:CREAR")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(int id, string? motivo)
    {
        var matricula = await _db.Matriculas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (matricula is null)
        {
            return NotFound();
        }

        var funcionarioId = int.TryParse(User.FindFirstValue("funcionario_id"), out var valor)
            ? valor
            : (int?)null;

        if (funcionarioId is null)
        {
            TempData["Aviso"] = "Inicie sesión como funcionario para anular la matrícula.";
            return RedirectToAction(nameof(Detalle), new { id = matricula.EstudianteId });
        }

        string? error = null;

        await _escritura.EjecutarAsync(funcionarioId.Value, async ct =>
        {
            error = await _registro.AnularAsync(id, motivo, ct);
        }, HttpContext.RequestAborted);

        if (error is null)
        {
            TempData["Notificacion"] = "Matrícula anulada. Puede iniciar una matrícula nueva para el año.";
        }
        else
        {
            TempData["Aviso"] = error;
        }

        return RedirectToAction(nameof(Detalle), new { id = matricula.EstudianteId });
    }

    /// <summary>
    /// Validación institucional de Dirección (MAT-12): acto distinto de
    /// confirmar, sobre matrícula firmada o confirmada. Deja fecha y
    /// responsable, auditados por trigger.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:VALIDAR")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Validar(int id)
    {
        var matricula = await _db.Matriculas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (matricula is null)
        {
            return NotFound();
        }

        var funcionarioId = int.TryParse(User.FindFirstValue("funcionario_id"), out var valor)
            ? valor
            : (int?)null;

        if (funcionarioId is null)
        {
            TempData["Aviso"] = "Inicie sesión como funcionario para validar la matrícula.";
            return RedirectToAction(nameof(Detalle), new { id = matricula.EstudianteId });
        }

        string? error = null;

        await _escritura.EjecutarAsync(funcionarioId.Value, async ct =>
        {
            error = await _registro.ValidarAsync(id, funcionarioId.Value, ct);
        }, HttpContext.RequestAborted);

        if (error is null)
        {
            TempData["Notificacion"] = "Matrícula validada por Dirección.";
        }
        else
        {
            TempData["Aviso"] = error;
        }

        return RedirectToAction(nameof(Detalle), new { id = matricula.EstudianteId });
    }

    /// <summary>
    /// Registra la recepción de la ficha firmada por el apoderado (transición
    /// FICHA_GENERADA → FIRMADA). El escaneo se incorpora en la Etapa II.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:CREAR")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarFirma(int id, string? fecha)
    {
        var matricula = await _db.Matriculas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (matricula is null)
        {
            return NotFound();
        }

        var dia = DateOnly.TryParse(fecha, out var valor)
            ? valor
            : DateOnly.FromDateTime(DateTime.Today);

        var funcionarioId = int.TryParse(User.FindFirstValue("funcionario_id"), out var idFuncionario)
            ? idFuncionario
            : 0;

        string? error = null;

        await _escritura.EjecutarAsync(funcionarioId, async ct =>
        {
            error = await _registro.RegistrarFirmaAsync(id, dia, ct);
        }, HttpContext.RequestAborted);

        TempData[error is null ? "Notificacion" : "Aviso"] =
            error ?? "Ficha firmada registrada. Queda pendiente la confirmación de Secretaría.";

        return RedirectToAction(nameof(Detalle), new { id = matricula.EstudianteId });
    }

    /// <summary>Sube o reemplaza el archivo de un requisito documental (MAT-06/07).</summary>
    [HttpPost]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "DOCUMENTOS:CREAR")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> SubirDocumento(int id, int requisitoId, IFormFile? archivo)
    {
        var requisito = await _db.RequisitosDocumentales.FirstOrDefaultAsync(r => r.Id == requisitoId);

        if (requisito is null)
        {
            return NotFound();
        }

        var matricula = await _db.Matriculas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == requisito.MatriculaId);

        if (matricula is null || matricula.EstudianteId != id)
        {
            return NotFound();
        }

        var funcionarioId = int.TryParse(User.FindFirstValue("funcionario_id"), out var idFuncionario)
            ? idFuncionario
            : (int?)null;

        if (funcionarioId is null)
        {
            TempData["Aviso"] = "Inicie sesión como funcionario para subir documentos.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var error = ExpedienteDocumentos.Validar(archivo);

        if (error is not null)
        {
            TempData["Aviso"] = error;
            return RedirectToAction(nameof(Detalle), new { id });
        }

        ResultadoArchivo? guardado = null;
        var documentoId = 0;

        try
        {
            await _escritura.EjecutarAsync(funcionarioId.Value, async ct =>
            {
                guardado = await _documentos.GuardarAsync(archivo!, id, ct);

                var documento = new Documento
                {
                    EstudianteId = id,
                    MatriculaId = requisito.MatriculaId,
                    Tipo = requisito.TipoDocumento,
                    Sensibilidad = CatalogoDocumentos.Sensibilidad(requisito.TipoDocumento),
                    NombreOriginal = guardado.Nombre,
                    MimeType = guardado.Mime,
                    TamanoBytes = guardado.Tamano,
                    RutaAlmacenamiento = guardado.Ruta,
                    HashSha256 = guardado.Hash,
                    SubidoPor = funcionarioId.Value,
                    SubidoEn = DateTimeOffset.UtcNow,
                    ReemplazaId = requisito.DocumentoId,
                };

                if (requisito.DocumentoId is int anterior)
                {
                    var previo = await _db.Documentos.FirstOrDefaultAsync(d => d.Id == anterior, ct);

                    if (previo is not null)
                    {
                        previo.Activo = false;
                    }
                }

                _db.Documentos.Add(documento);
                await _db.SaveChangesAsync(ct);

                documentoId = documento.Id;
                requisito.DocumentoId = documento.Id;
                requisito.Estado = "RECIBIDO";
                requisito.RegistradoPor = funcionarioId.Value;
                requisito.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }, HttpContext.RequestAborted);
        }
        catch (ArchivoInvalidoException ex)
        {
            if (guardado is not null)
            {
                _documentos.Eliminar(guardado.Ruta);
            }

            TempData["Aviso"] = ex.Message;
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (Exception ex) when (ex is DbUpdateException or IOException)
        {
            if (guardado is not null)
            {
                _documentos.Eliminar(guardado.Ruta);
            }

            TempData["Aviso"] = "No se pudo guardar el documento. Intente nuevamente.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        await _auditoria.RegistrarAsync(
            "REEMPLAZAR",
            "DOCUMENTOS",
            "documento",
            detalle: $"{{\"evento\":\"subir_documento\",\"tipo\":\"{requisito.TipoDocumento}\"}}",
            estudianteId: id,
            recursoId: documentoId);

        TempData["Notificacion"] = $"Documento «{CatalogoDocumentos.Etiqueta(requisito.TipoDocumento)}» recibido.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    /// <summary>Marca un requisito como no aplica o lo devuelve a pendiente (MAT-07).</summary>
    [HttpPost]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "DOCUMENTOS:EDITAR")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarDocumento(int id, int requisitoId, string estado)
    {
        if (estado is not ("NO_APLICA" or "PENDIENTE"))
        {
            TempData["Aviso"] = "Estado documental no válido.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var requisito = await _db.RequisitosDocumentales.FirstOrDefaultAsync(r => r.Id == requisitoId);

        if (requisito is null)
        {
            return NotFound();
        }

        var matricula = await _db.Matriculas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == requisito.MatriculaId);

        if (matricula is null || matricula.EstudianteId != id)
        {
            return NotFound();
        }

        var funcionarioId = int.TryParse(User.FindFirstValue("funcionario_id"), out var idFuncionario)
            ? idFuncionario
            : (int?)null;

        if (funcionarioId is null)
        {
            TempData["Aviso"] = "Inicie sesión como funcionario para actualizar el expediente.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        await _escritura.EjecutarAsync(funcionarioId.Value, async ct =>
        {
            requisito.Estado = estado;
            requisito.DocumentoId = null;
            requisito.RegistradoPor = funcionarioId.Value;
            requisito.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }, HttpContext.RequestAborted);

        await _auditoria.RegistrarAsync(
            "REEMPLAZAR",
            "DOCUMENTOS",
            "requisito_documental",
            detalle: $"{{\"evento\":\"estado_documento\",\"estado\":\"{estado}\"}}",
            estudianteId: id,
            recursoId: requisito.Id);

        TempData["Notificacion"] = $"«{CatalogoDocumentos.Etiqueta(requisito.TipoDocumento)}» quedó {(estado == "NO_APLICA" ? "como no aplica" : "pendiente")}.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    /// <summary>Descarga un documento del expediente sólo si la sesión puede ver su área.</summary>
    [HttpGet]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "DOCUMENTOS:LEER")]
    public async Task<IActionResult> DescargarDocumento(int id, int documentoId)
    {
        var documento = await _db.Documentos
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentoId && d.EstudianteId == id && d.Activo);

        if (documento is null)
        {
            return NotFound();
        }

        if (!PuedeVerSensibilidad(documento.Sensibilidad))
        {
            await _auditoria.RegistrarAsync(
                "DENEGADO",
                documento.Sensibilidad,
                "documento",
                estudianteId: id,
                recursoId: documento.Id);

            return Forbid();
        }

        var ruta = _documentos.RutaFisica(documento.RutaAlmacenamiento);

        if (ruta is null)
        {
            TempData["Aviso"] = "El archivo no está disponible en el servidor.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        await _auditoria.RegistrarAsync(
            "DESCARGAR",
            "DOCUMENTOS",
            "documento",
            detalle: $"{{\"tipo\":\"{documento.Tipo}\",\"sensibilidad\":\"{documento.Sensibilidad}\"}}",
            estudianteId: id,
            recursoId: documento.Id);

        return PhysicalFile(ruta, documento.MimeType, documento.NombreOriginal);
    }

    /// <summary>Adjunta el escaneo de la ficha firmada a la versión vigente (MAT-09).</summary>
    [HttpPost]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "DOCUMENTOS:CREAR")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> SubirFichaFirmada(int id, int matriculaId, IFormFile? archivo)
    {
        var matricula = await _db.Matriculas
            .FirstOrDefaultAsync(m => m.Id == matriculaId && m.EstudianteId == id);

        if (matricula is null)
        {
            return NotFound();
        }

        var ficha = await _db.FichaVersiones
            .FirstOrDefaultAsync(v => v.MatriculaId == matriculaId && v.Estado == "VIGENTE");

        if (ficha is null)
        {
            TempData["Aviso"] = "Genere la ficha antes de subir el escaneo firmado.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var funcionarioId = int.TryParse(User.FindFirstValue("funcionario_id"), out var idFuncionario)
            ? idFuncionario
            : (int?)null;

        if (funcionarioId is null)
        {
            TempData["Aviso"] = "Inicie sesión como funcionario para subir el escaneo.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var error = ExpedienteDocumentos.Validar(archivo);

        if (error is not null)
        {
            TempData["Aviso"] = error;
            return RedirectToAction(nameof(Detalle), new { id });
        }

        ResultadoArchivo? guardado = null;
        var documentoId = 0;

        try
        {
            await _escritura.EjecutarAsync(funcionarioId.Value, async ct =>
            {
                guardado = await _documentos.GuardarAsync(archivo!, id, ct);

                // La ficha cerrada no se edita (trigger del DDL): el escaneo queda
                // como documento de la matrícula y reemplaza al anterior.
                var anterior = await _db.Documentos
                    .Where(d => d.MatriculaId == matriculaId && d.Tipo == "FICHA_FIRMADA" && d.Activo)
                    .OrderByDescending(d => d.Id)
                    .FirstOrDefaultAsync(ct);

                var documento = new Documento
                {
                    EstudianteId = id,
                    MatriculaId = matriculaId,
                    Tipo = "FICHA_FIRMADA",
                    Sensibilidad = "GENERAL",
                    NombreOriginal = guardado.Nombre,
                    MimeType = guardado.Mime,
                    TamanoBytes = guardado.Tamano,
                    RutaAlmacenamiento = guardado.Ruta,
                    HashSha256 = guardado.Hash,
                    SubidoPor = funcionarioId.Value,
                    SubidoEn = DateTimeOffset.UtcNow,
                    ReemplazaId = anterior?.Id,
                };

                if (anterior is not null)
                {
                    anterior.Activo = false;
                }

                _db.Documentos.Add(documento);
                await _db.SaveChangesAsync(ct);

                documentoId = documento.Id;

                if (matricula.Estado == "FICHA_GENERADA")
                {
                    matricula.Estado = "FIRMADA";
                }

                matricula.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }, HttpContext.RequestAborted);
        }
        catch (ArchivoInvalidoException ex)
        {
            if (guardado is not null)
            {
                _documentos.Eliminar(guardado.Ruta);
            }

            TempData["Aviso"] = ex.Message;
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (Exception ex) when (ex is DbUpdateException or IOException)
        {
            if (guardado is not null)
            {
                _documentos.Eliminar(guardado.Ruta);
            }

            TempData["Aviso"] = "No se pudo guardar el escaneo. Intente nuevamente.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        await _auditoria.RegistrarAsync(
            "REEMPLAZAR",
            "DOCUMENTOS",
            "documento",
            detalle: "{\"evento\":\"subir_ficha_firmada\"}",
            estudianteId: id,
            recursoId: documentoId);

        TempData["Notificacion"] = "Escaneo de la ficha firmada adjuntado a la versión vigente.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    /// <summary>
    /// MAT-04: revoca una autorización otorgada. Exige motivo y conserva la firma
    /// y la fecha originales; la difusión queda bloqueada desde ese momento.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = PermisoPolicyProvider.Prefijo + "AUTORIZACIONES:EDITAR")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevocarAutorizacion(int id, int autorizacionId, string? motivo)
    {
        var funcionarioId = int.TryParse(User.FindFirstValue("funcionario_id"), out var valor)
            ? valor
            : (int?)null;

        if (funcionarioId is null)
        {
            TempData["Aviso"] = "Inicie sesión como funcionario para revocar una autorización.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        string? error = null;

        await _escritura.EjecutarAsync(funcionarioId.Value, async ct =>
        {
            error = await _registro.RevocarAutorizacionAsync(autorizacionId, id, motivo, funcionarioId.Value, ct);
        }, HttpContext.RequestAborted);

        if (error is null)
        {
            await _auditoria.RegistrarAsync(
                "REEMPLAZAR",
                "AUTORIZACIONES",
                "autorizacion",
                detalle: "{\"evento\":\"revocar_autorizacion\"}",
                estudianteId: id,
                recursoId: autorizacionId);

            TempData["Notificacion"] = "Autorización revocada. Se conserva la firma y la fecha originales.";
        }
        else
        {
            TempData["Aviso"] = error;
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    private bool PuedeVerSensibilidad(string sensibilidad) =>
        CatalogoDocumentos.VisiblePara(sensibilidad, area => Puede(area));

    private static string EtiquetaAutorizacion(string tipo) => tipo switch
    {
        "IMAGEN" => "Uso de imagen",
        "CONVIVENCIA" => "Convivencia Escolar",
        "MEDICACION" => "Medicación",
        _ => tipo,
    };

    private static string EtiquetaAlcance(string alcance) => alcance switch
    {
        "CAPTACION_INTERNA" => "Captación interna",
        "SITIO_INSTITUCIONAL" => "Sitio institucional",
        "REDES_SOCIALES" => "Redes sociales",
        "OTROS_MEDIOS" => "Otros medios",
        _ => alcance,
    };

    /// <summary>
    /// Ordena el listado por la columna elegida. Los valores vacíos se mandan al
    /// final para que no se mezclen con los datos reales.
    /// </summary>
    private static IEnumerable<AlumnoListado> Ordenar(
        IEnumerable<AlumnoListado> filas,
        string orden,
        bool descendente)
    {
        static string ConValor(string? valor) => string.IsNullOrWhiteSpace(valor) ? "\uffff" : valor;

        return orden switch
        {
            "rut" => descendente
                ? filas.OrderByDescending(a => NumeroRut(a.Rut))
                : filas.OrderBy(a => NumeroRut(a.Rut)),
            "curso" => descendente
                ? filas.OrderByDescending(a => ConValor(a.Curso), StringComparer.CurrentCulture)
                : filas.OrderBy(a => ConValor(a.Curso), StringComparer.CurrentCulture),
            "estado" => descendente
                ? filas.OrderByDescending(a => a.EstadoTexto, StringComparer.CurrentCulture)
                : filas.OrderBy(a => a.EstadoTexto, StringComparer.CurrentCulture),
            "tipo" => descendente
                ? filas.OrderByDescending(a => a.TipoTexto, StringComparer.CurrentCulture)
                : filas.OrderBy(a => a.TipoTexto, StringComparer.CurrentCulture),
            "comuna" => descendente
                ? filas.OrderByDescending(a => ConValor(a.Comuna), StringComparer.CurrentCulture)
                : filas.OrderBy(a => ConValor(a.Comuna), StringComparer.CurrentCulture),
            "pendientes" => descendente
                ? filas.OrderByDescending(a => a.Pendientes)
                : filas.OrderBy(a => a.Pendientes),
            _ => descendente
                ? filas.OrderByDescending(a => a.Nombre, StringComparer.CurrentCulture)
                : filas.OrderBy(a => a.Nombre, StringComparer.CurrentCulture),
        };
    }

    /// <summary>
    /// Cuerpo numérico del RUN (sin dígito verificador), para ordenar
    /// 9.111.111 antes que 24.455.667 y no por el texto con puntos.
    /// </summary>
    private static long NumeroRut(string rut)
    {
        var cuerpo = rut.Split('-')[0];
        var digitos = new string(cuerpo.Where(char.IsDigit).ToArray());

        return long.TryParse(digitos, out var numero) ? numero : 0;
    }

    private static string NombrePersona(Persona persona) =>
        string.Join(' ', new[] { persona.Nombres, persona.Apellidos }
            .Where(parte => !string.IsNullOrWhiteSpace(parte)));

    private static string EtiquetaVinculo(EstudiantePersona vinculo) => vinculo.TipoApoderado switch
    {
        "PRINCIPAL" => ConVinculo("Apoderado principal", vinculo.Vinculo),
        "SECUNDARIO" => ConVinculo("Apoderado secundario", vinculo.Vinculo),
        _ => vinculo.Parentesco switch
        {
            "MADRE" => "Madre",
            "PADRE" => "Padre",
            _ => vinculo.Vinculo ?? "Vínculo no declarado",
        },
    };

    private static string ConVinculo(string etiqueta, string? vinculo) =>
        string.IsNullOrWhiteSpace(vinculo) ? etiqueta : $"{etiqueta} · {vinculo}";

    private static int? Edad(DateOnly nacimiento)
    {
        if (nacimiento == default)
        {
            return null;
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var edad = hoy.Year - nacimiento.Year;

        if (nacimiento > hoy.AddYears(-edad))
        {
            edad--;
        }

        return edad;
    }

    /// <summary>Quita las tildes para comparar nombres como los escribe el usuario.</summary>
    private static string SinTildes(string valor)
    {
        var descompuesto = valor.Normalize(System.Text.NormalizationForm.FormD);
        var limpio = new System.Text.StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(caracter);

            if (categoria != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                limpio.Append(caracter);
            }
        }

        return limpio.ToString();
    }
}
