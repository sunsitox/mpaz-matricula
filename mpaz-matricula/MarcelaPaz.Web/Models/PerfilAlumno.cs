namespace MarcelaPaz.Web.Models;

/// <summary>
/// Ficha de consulta de un alumno: datos del registro único, historial de
/// matrículas, apoderados, contactos, retiros autorizados y salud declarada.
/// Sólo se muestran los vínculos y registros vigentes.
/// </summary>
public class PerfilAlumno
{
    /// <summary>Año escolar consultado: el activo o el elegido en el selector.</summary>
    public int Anio { get; set; }

    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>RUN con puntos, como se muestra en pantalla.</summary>
    public string Rut { get; set; } = string.Empty;

    public DateOnly? FechaNacimiento { get; set; }

    public int? Edad { get; set; }

    public string? Nacionalidad { get; set; }

    public string? Domicilio { get; set; }

    public string? Comuna { get; set; }

    public bool? SaleSolo { get; set; }

    public bool? TieneSituacionMedica { get; set; }

    public string? NecesidadesAtencion { get; set; }

    public List<MatriculaPerfil> Matriculas { get; set; } = new();

    public List<ApoderadoPerfil> Apoderados { get; set; } = new();

    public List<ContactoPerfil> Contactos { get; set; } = new();

    public List<RetiroPerfil> Retiros { get; set; } = new();

    public List<SituacionPerfil> Situaciones { get; set; } = new();

    /// <summary>Restricciones vigentes que afectan a algún vínculo familiar.</summary>
    public List<string> Restricciones { get; set; } = new();

    public string? CursoActual { get; set; }

    public string? EstadoActual { get; set; }

    /// <summary>Folio y versión de la ficha vigente del año (MAT-08).</summary>
    public string? FolioFicha { get; set; }

    public short? VersionFicha { get; set; }

    public int? MatriculaId { get; set; }

    /// <summary>La matrícula tiene ficha generada y puede confirmarse (MAT-04).</summary>
    public bool PuedeConfirmar { get; set; }

    /// <summary>Autorizaciones vigentes, ya formateadas para mostrar.</summary>
    public List<string> Autorizaciones { get; set; } = new();

    // Qué bloques puede ver esta sesión. El servidor no consulta ni proyecta los
    // datos de un área sin su permiso; la vista usa estas banderas para explicar
    // la ausencia en lugar de mostrar una lista vacía (S-02).
    public bool PuedeVerFamilia { get; set; }

    public bool PuedeVerRetiro { get; set; }

    public bool PuedeVerSalud { get; set; }

    public bool PuedeVerAutorizaciones { get; set; }

    public class MatriculaPerfil
    {
        public short Anio { get; set; }

        public string? Curso { get; set; }

        public string Estado { get; set; } = string.Empty;

        public string Tipo { get; set; } = string.Empty;
    }

    public class ApoderadoPerfil
    {
        public string Nombre { get; set; } = string.Empty;

        public string Rut { get; set; } = string.Empty;

        public string Vinculo { get; set; } = string.Empty;

        public string? Telefono { get; set; }

        public string? Correo { get; set; }

        public bool TieneRestriccion { get; set; }
    }

    public class ContactoPerfil
    {
        public string Nombre { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;

        public string Vinculo { get; set; } = string.Empty;
    }

    public class RetiroPerfil
    {
        public string Nombre { get; set; } = string.Empty;

        public string Rut { get; set; } = string.Empty;

        public string Vinculo { get; set; } = string.Empty;
    }

    public class SituacionPerfil
    {
        public string Tipo { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        public bool? EnTratamiento { get; set; }
    }
}
