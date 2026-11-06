using MarcelaPaz.Domain.Entidades;
using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Seguridad;
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
    private const int Anio = 2027;

    private readonly MatriculaDbContext _db;

    public AlumnosController(MatriculaDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? curso, string? estado, string? orden, string? dir)
    {
        var estudiantes = await _db.Estudiantes
            .AsNoTracking()
            .Include(e => e.Comuna)
            .OrderBy(e => e.ApellidoPaterno)
            .ThenBy(e => e.Nombres)
            .ToListAsync();

        var matriculas = await _db.Matriculas
            .AsNoTracking()
            .Where(m => m.Anio == Anio)
            .Select(m => new
            {
                m.EstudianteId,
                m.Estado,
                m.Tipo,
                Curso = m.Curso != null ? m.Curso.Nombre : null,
            })
            .ToListAsync();

        var delAnio = matriculas
            .GroupBy(m => m.EstudianteId)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.First());

        var todos = estudiantes
            .Select(estudiante =>
            {
                delAnio.TryGetValue(estudiante.Id, out var matricula);

                return new AlumnoListado
                {
                    Id = estudiante.Id,
                    Nombre = estudiante.NombreCompleto,
                    Rut = RutChileno.Formatear(estudiante.Identificador),
                    Curso = matricula?.Curso,
                    Estado = matricula?.Estado,
                    Tipo = matricula?.Tipo,
                    Comuna = estudiante.Comuna?.Nombre,
                };
            })
            .ToList();

        // Sólo se aceptan las columnas conocidas; cualquier otra vuelve a nombre.
        var columnas = new[] { "nombre", "rut", "curso", "estado", "tipo", "comuna" };
        var ordenPedido = (orden ?? string.Empty).ToLowerInvariant();
        var ordenValido = columnas.Contains(ordenPedido) ? ordenPedido : "nombre";
        var descendente = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

        var filtro = new FiltroAlumnos
        {
            Anio = Anio,
            Texto = q?.Trim(),
            Curso = string.IsNullOrWhiteSpace(curso) ? null : curso,
            Estado = string.IsNullOrWhiteSpace(estado) ? null : estado,
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
            var soloRut = filtro.Texto.Replace(".", string.Empty).Replace("-", string.Empty).ToUpperInvariant();

            filas = filas.Where(a =>
                SinTildes(a.Nombre).ToUpperInvariant().Contains(texto, StringComparison.Ordinal)
                || a.Rut.Replace(".", string.Empty).Replace("-", string.Empty)
                    .StartsWith(soloRut, StringComparison.OrdinalIgnoreCase));
        }

        if (filtro.Curso is not null)
        {
            filas = filas.Where(a => a.Curso == filtro.Curso);
        }

        if (filtro.Estado is not null)
        {
            filas = filas.Where(a => a.EstadoTexto == filtro.Estado);
        }

        filtro.Alumnos = Ordenar(filas, filtro.Orden, filtro.Direccion == "desc").ToList();

        return View(filtro);
    }

    /// <summary>Ficha de consulta del alumno: matrículas, apoderados, retiros y salud.</summary>
    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
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

        var vinculos = await _db.EstudiantesPersonas
            .AsNoTracking()
            .Include(v => v.Persona)
            .Where(v => v.EstudianteId == id && v.VigenteHasta == null)
            .OrderBy(v => v.Id)
            .ToListAsync();

        var idsVinculos = vinculos.Select(v => v.Id).ToList();

        var restricciones = await _db.RestriccionesFamiliares
            .AsNoTracking()
            .Where(r => idsVinculos.Contains(r.EstudiantePersonaId) && r.VigenteHasta == null)
            .ToListAsync();

        var contactos = await _db.ContactosEmergencia
            .AsNoTracking()
            .Where(c => c.EstudianteId == id && c.VigenteHasta == null)
            .OrderBy(c => c.Orden)
            .ToListAsync();

        var retiros = await _db.RetirosAutorizados
            .AsNoTracking()
            .Where(r => r.EstudianteId == id && r.VigenteHasta == null)
            .OrderBy(r => r.Id)
            .ToListAsync();

        var salud = await _db.AntecedentesSalud
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EstudianteId == id);

        var situaciones = await _db.SituacionesMedicas
            .AsNoTracking()
            .Where(s => s.EstudianteId == id && s.VigenteHasta == null)
            .OrderBy(s => s.Id)
            .ToListAsync();

        var perfil = new PerfilAlumno
        {
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
        };

        var vigente = matriculas.FirstOrDefault(m => m.Anio == Anio);

        if (vigente is not null)
        {
            perfil.CursoActual = vigente.Curso?.Nombre;
            perfil.EstadoActual = vigente.Estado;
        }

        return View(perfil);
    }

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
