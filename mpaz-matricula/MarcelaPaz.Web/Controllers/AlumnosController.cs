using MarcelaPaz.Domain.Identificacion;
using MarcelaPaz.Infrastructure.Persistence;
using MarcelaPaz.Web.Models;
using MarcelaPaz.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarcelaPaz.Web.Controllers;

/// <summary>
/// Listado de alumnos del registro único con su matrícula del año, para consultar
/// y filtrar. La extracción de documentos queda pendiente: el botón se muestra
/// deshabilitado hasta acordar con el colegio qué documentos se pueden obtener.
/// </summary>
[Authorize(Policy = PermisoPolicyProvider.Prefijo + "GENERAL:LEER")]
public class AlumnosController : Controller
{
    private const int Anio = 2027;

    private readonly MatriculaDbContext _db;

    public AlumnosController(MatriculaDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? curso, string? estado)
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

        var filtro = new FiltroAlumnos
        {
            Anio = Anio,
            Texto = q?.Trim(),
            Curso = string.IsNullOrWhiteSpace(curso) ? null : curso,
            Estado = string.IsNullOrWhiteSpace(estado) ? null : estado,
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

        filtro.Alumnos = filas.ToList();

        return View(filtro);
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
